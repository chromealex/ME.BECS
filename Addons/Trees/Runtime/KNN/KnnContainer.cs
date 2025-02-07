//MIT License
//
//Copyright(c) 2018 Vili Volčini / viliwonka
//
//Permission is hereby granted, free of charge, to any person obtaining a copy
//of this software and associated documentation files (the "Software"), to deal
//in the Software without restriction, including without limitation the rights
//to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
//copies of the Software, and to permit persons to whom the Software is
//furnished to do so, subject to the following conditions:
//
//The above copyright notice and this permission notice shall be included in all
//copies or substantial portions of the Software.
//
//THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
//IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
//FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
//AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
//LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
//OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
//SOFTWARE.
//
// Modifed 2019 Arthur Brussee
#if FIXED_POINT
using tfloat = sfloat;
using ME.BECS.FixedPoint;
using Bounds = ME.BECS.FixedPoint.AABB;
#else
using tfloat = System.Single;
using Unity.Mathematics;
using Bounds = UnityEngine.Bounds;
#endif

using System;
using KNN.Internal;
using KNN.Jobs;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using ME.BECS.NativeCollections;

namespace KNN {
	[NativeContainerSupportsDeallocateOnJobCompletion, NativeContainer, System.Diagnostics.DebuggerDisplay("Length = {Points.Length}")]
	public unsafe struct KnnContainer<T> : IDisposable where T : unmanaged, IComparable<T> {

		public struct Node : IComparable<Node> {

			public float3 position;
			public tfloat radiusSqr;
			public T data;

			public int CompareTo(Node other) {
				return this.data.CompareTo(other.data);
			}

		}
		
		// We manage safety by our own sentinel. Disable unity's safety system for internal caches / arrays
		[NativeDisableContainerSafetyRestriction]
		public UnsafeList<Node> Points;

		[NativeDisableContainerSafetyRestriction]
		public ME.BECS.NativeCollections.NativeParallelList<Node> PointsHashWriter;

		[NativeDisableContainerSafetyRestriction]
		NativeArray<int> m_permutation;

		[NativeDisableContainerSafetyRestriction]
		UnsafeList<KdNode> m_nodes;

		[NativeDisableContainerSafetyRestriction]
		NativeReference<int> m_rootNodeIndex;

		[NativeDisableContainerSafetyRestriction]
		NativeQueue<int> m_buildQueue;

		KdNode RootNode => this.m_nodes[this.m_rootNodeIndex.Value];

		const int c_maxPointsPerLeafNode = 64;

		public struct KnnQueryTemp : IDisposable {
			public MinMaxHeap<int> MaxHeap;
			public MinMaxHeap<QueryNode> MinHeap;

			public static KnnQueryTemp Create(uint kCapacity) {
				KnnQueryTemp temp;
				temp.MaxHeap = new MinMaxHeap<int>(kCapacity, Allocator.Temp);
				
				// Min heap keeps track of current stack.
				// The max stack depth is the tree depth
				// The tree depth is log_c(nodes)
				// Let's assume people have a tree at most 32 deep (which equals 2^32 * c_maxPointsPerLeafNode ~ 2^39 nodes)
				// There are left/right nodes -> 64 max on stack at any given time
				temp.MinHeap = new MinMaxHeap<QueryNode>(64, Allocator.Temp);
				return temp;
			}

			public void PushQueryNode(int index, float3 closestPoint, float3 queryPosition, tfloat radiusSqr) {
				var lengthsq = math.lengthsq(closestPoint - queryPosition);

				this.MinHeap.PushObjMin(new QueryNode {
					NodeIndex = index,
					TempClosestPoint = closestPoint,
					Distance = lengthsq,
					RadiusSqr = radiusSqr,
				}, lengthsq);
			}

			public void Dispose() {
				this.MaxHeap.Dispose();
				this.MinHeap.Dispose();
			}
		}

		public KnnContainer(NativeArray<Node> points, Allocator allocator) {
			if (points.IsCreated == false) {
				this = default;
				return;
			}

			this = default;
			int nodeCountEstimate = 4 * (int) math.ceil(points.Length / (tfloat) c_maxPointsPerLeafNode + 1) + 1;
			this.Points = new UnsafeList<Node>((Node*)points.GetUnsafePtr(), points.Length);

			// Both arrays are filled in as we go, so start with uninitialized mem
			this.m_nodes = new UnsafeList<KdNode>(nodeCountEstimate, allocator);

			// Dumb way to create an int* essentially..
			this.m_permutation = CollectionHelper.CreateNativeArray<int>(points.Length, allocator, NativeArrayOptions.UninitializedMemory);
			this.m_rootNodeIndex = new NativeReference<int>(-1, allocator);//CollectionHelper.CreateNativeArray<int>(1, allocator);
			this.m_buildQueue = new NativeQueue<int>(allocator);
		}

		[System.Runtime.CompilerServices.MethodImplAttribute(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
		public void Clear() {
			this.PointsHashWriter.Clear();
		}

		[System.Runtime.CompilerServices.MethodImplAttribute(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
		public void AddPoint(float3 point, in T data, tfloat radius) {
			//Points.Add(point);
			this.PointsHashWriter.Add(new Node() { position = point, data = data, radiusSqr = radius * radius });
		}

		public KnnContainer<T> Initialize(int capacity, Allocator allocator) {
			this = default;
			this.PointsHashWriter = new ME.BECS.NativeCollections.NativeParallelList<Node>(capacity, allocator);
			this.m_nodes = new UnsafeList<KdNode>(1, allocator);
			this.m_rootNodeIndex = new NativeReference<int>(-1, allocator);//CollectionHelper.CreateNativeArray<int>(1, allocator);
			return this;
		}

		[System.Runtime.CompilerServices.MethodImplAttribute(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
		public void SetPoints(Allocator allocator) {
			if (this.PointsHashWriter.Count == 0) return;
			if (this.Points.IsCreated == true) this.Points.Dispose();
			this.Points = this.PointsHashWriter.ToList(allocator);
			this.Points.Sort();
			int nodeCountEstimate = 4 * (int) math.ceil(this.Points.Length / (tfloat) c_maxPointsPerLeafNode + 1) + 1;
			
			// Both arrays are filled in as we go, so start with uninitialized mem
			if (this.m_nodes.IsCreated == false || this.m_nodes.Capacity != this.Points.Length) {
				if (this.m_nodes.IsCreated == true) this.m_nodes.Dispose();
				this.m_nodes = new UnsafeList<KdNode>(nodeCountEstimate, allocator);
			}

			this.m_nodes.Clear();

			// Dumb way to create an int* essentially..
			if (this.m_permutation.Length != this.Points.Length) {
				if (this.m_permutation.IsCreated == true) this.m_permutation.Dispose();
				this.m_permutation = CollectionHelper.CreateNativeArray<int>(this.Points.Length, allocator, NativeArrayOptions.UninitializedMemory);
				for (int i = 0; i < this.m_permutation.Length; ++i) {
					this.m_permutation[i] = i;
				}
			}

			//if (this.m_rootNodeIndex.IsCreated == false) this.m_rootNodeIndex = CollectionHelper.CreateNativeArray<int>(1, allocator);
			this.m_rootNodeIndex.Value = -1;
			if (this.m_buildQueue.IsCreated == false) this.m_buildQueue = new NativeQueue<int>(allocator);
			this.m_buildQueue.Clear();
		}

		public void Rebuild() {

			if (this.Points.Length == 0) return;

			this.m_nodes.Clear();

			int rootNode = this.GetKdNode(this.MakeBounds(), 0, this.Points.Length);

			this.m_rootNodeIndex.Value = rootNode;
			this.m_buildQueue.Enqueue(rootNode);

			while (this.m_buildQueue.Count > 0) {
				int index = this.m_buildQueue.Dequeue();
				this.SplitNode(index, out int posNodeIndex, out int negNodeIndex);

				if (this.m_nodes[negNodeIndex].Count > c_maxPointsPerLeafNode) {
					this.m_buildQueue.Enqueue(posNodeIndex);
				}

				if (this.m_nodes[posNodeIndex].Count > c_maxPointsPerLeafNode) {
					this.m_buildQueue.Enqueue(negNodeIndex);
				}
			}
		}

		public void Dispose() {

			this.Points.Dispose();
			this.m_permutation.Dispose();
			this.m_nodes.Dispose();
			this.m_rootNodeIndex.Dispose();
			this.m_buildQueue.Dispose();
			
			this.PointsHashWriter.Dispose();
			
		}

		int GetKdNode(KdNodeBounds bounds, int start, int end) {
			this.m_nodes.Add(new KdNode {
				Bounds = bounds,
				Start = start,
				End = end,
				PartitionAxis = -1,
				PartitionCoordinate = 0.0f,
				PositiveChildIndex = -1,
				NegativeChildIndex = -1
			});

			return this.m_nodes.Length - 1;
		}

		/// <summary>
		/// For calculating root node bounds
		/// </summary>
		/// <returns>Boundary of all Vector3 points</returns>
		KdNodeBounds MakeBounds() {
			var max = new float3(tfloat.MinValue, tfloat.MinValue, tfloat.MinValue);
			var min = new float3(tfloat.MaxValue, tfloat.MaxValue, tfloat.MaxValue);
			int even = this.Points.Length & ~1; // calculate even Length

			// min, max calculations
			// 3n/2 calculations instead of 2n
			for (int i0 = 0; i0 < even; i0 += 2) {
				int i1 = i0 + 1;

				// X Coords
				if (this.Points[i0].position.x > this.Points[i1].position.x) {
					// i0 is bigger, i1 is smaller
					if (this.Points[i1].position.x < min.x) {
						min.x = this.Points[i1].position.x;
					}

					if (this.Points[i0].position.x > max.x) {
						max.x = this.Points[i0].position.x;
					}
				} else {
					// i1 is smaller, i0 is bigger
					if (this.Points[i0].position.x < min.x) {
						min.x = this.Points[i0].position.x;
					}

					if (this.Points[i1].position.x > max.x) {
						max.x = this.Points[i1].position.x;
					}
				}

				// Y Coords
				if (this.Points[i0].position.y > this.Points[i1].position.y) {
					// i0 is bigger, i1 is smaller
					if (this.Points[i1].position.y < min.y) {
						min.y = this.Points[i1].position.y;
					}

					if (this.Points[i0].position.y > max.y) {
						max.y = this.Points[i0].position.y;
					}
				} else {
					// i1 is smaller, i0 is bigger
					if (this.Points[i0].position.y < min.y) {
						min.y = this.Points[i0].position.y;
					}

					if (this.Points[i1].position.y > max.y) {
						max.y = this.Points[i1].position.y;
					}
				}

				// Z Coords
				if (this.Points[i0].position.z > this.Points[i1].position.z) {
					// i0 is bigger, i1 is smaller
					if (this.Points[i1].position.z < min.z) {
						min.z = this.Points[i1].position.z;
					}

					if (this.Points[i0].position.z > max.z) {
						max.z = this.Points[i0].position.z;
					}
				} else {
					// i1 is smaller, i0 is bigger
					if (this.Points[i0].position.z < min.z) {
						min.z = this.Points[i0].position.z;
					}

					if (this.Points[i1].position.z > max.z) {
						max.z = this.Points[i1].position.z;
					}
				}
			}

			// if array was odd, calculate also min/max for the last element
			if (even != this.Points.Length) {
				// X
				if (min.x > this.Points[even].position.x) {
					min.x = this.Points[even].position.x;
				}

				if (max.x < this.Points[even].position.x) {
					max.x = this.Points[even].position.x;
				}

				// Y
				if (min.y > this.Points[even].position.y) {
					min.y = this.Points[even].position.y;
				}

				if (max.y < this.Points[even].position.y) {
					max.y = this.Points[even].position.y;
				}

				// Z
				if (min.z > this.Points[even].position.z) {
					min.z = this.Points[even].position.z;
				}

				if (max.z < this.Points[even].position.z) {
					max.z = this.Points[even].position.z;
				}
			}

			var b = new KdNodeBounds();
			b.Min = min;
			b.Max = max;
			return b;
		}

		// TODO: When multiple points overlap exactly this function breaks.
		/// <summary>
		/// Recursive splitting procedure
		/// </summary>
		void SplitNode(int parentIndex, out int posNodeIndex, out int negNodeIndex) {
			KdNode parent = this.m_nodes[parentIndex];

			// center of bounding box
			KdNodeBounds parentBounds = parent.Bounds;
			float3 parentBoundsSize = parentBounds.Size;

			// Find axis where bounds are largest
			int splitAxis = 0;
			var axisSize = parentBoundsSize.x;

			if (axisSize < parentBoundsSize.y) {
				splitAxis = 1;
				axisSize = parentBoundsSize.y;
			}

			if (axisSize < parentBoundsSize.z) {
				splitAxis = 2;
			}

			// Our axis min-max bounds
			var boundsStart = parentBounds.Min[splitAxis];
			var boundsEnd = parentBounds.Max[splitAxis];

			// Calculate the spiting coords
			var splitPivot = this.CalculatePivot(parent.Start, parent.End, boundsStart, boundsEnd, splitAxis);

			// 'Spiting' array to two sub arrays
			int splittingIndex = this.Partition(parent.Start, parent.End, splitPivot, splitAxis);

			// Negative / Left node
			float3 negMax = parentBounds.Max;
			negMax[splitAxis] = splitPivot;

			var bounds = parentBounds;
			bounds.Max = negMax;
			negNodeIndex = this.GetKdNode(bounds, parent.Start, splittingIndex);

			parent.PartitionAxis = splitAxis;
			parent.PartitionCoordinate = splitPivot;
			
			// Positive / Right node
			float3 posMin = parentBounds.Min;
			posMin[splitAxis] = splitPivot;

			bounds = parentBounds;
			bounds.Min = posMin;
			posNodeIndex = this.GetKdNode(bounds, splittingIndex, parent.End);

			parent.NegativeChildIndex = negNodeIndex;
			parent.PositiveChildIndex = posNodeIndex;

			// Write back node to array to update those values
			this.m_nodes[parentIndex] = parent;
		}

		/// <summary>
		/// Sliding midpoint splitting pivot calculation
		/// 1. First splits node to two equal parts (midPoint)
		/// 2. Checks if elements are in both sides of splitted bounds
		/// 3a. If they are, just return midPoint
		/// 3b. If they are not, then points are only on left or right bound.
		/// 4. Move the splitting pivot so that it shrinks part with points completely (calculate min or max dependent) and return.
		/// </summary>
        tfloat CalculatePivot(int start, int end, tfloat boundsStart, tfloat boundsEnd, int axis) {
			//! sliding midpoint rule
			var midPoint = (boundsStart + boundsEnd) / 2.0f;

			bool negative = false;
			bool positive = false;

			var negMax = tfloat.MinValue;
			var posMin = tfloat.MaxValue;

			var permutationPtr = (int*)this.m_permutation.GetUnsafeReadOnlyPtr();
			// this for loop section is used both for sorted and unsorted data
			for (int i = start; i < end; i++) {
				var val = this.Points[*(permutationPtr + i)].position[axis];

				if (val < midPoint) {
					negative = true;
				} else {
					positive = true;
				}

				if (negative && positive) {
					return midPoint;
				}
			}

			if (negative) {
				for (int i = start; i < end; i++) {
					var val = this.Points[*(permutationPtr + i)].position[axis];

					if (negMax < val) {
						negMax = val;
					}
				}

				return negMax;
			}

			for (int i = start; i < end; i++) {
				var val = this.Points[*(permutationPtr + i)].position[axis];

				if (posMin > val) {
					posMin = val;
				}
			}

			return posMin;
		}

		/// <summary>
		/// Similar to Hoare partitioning algorithm (used in Quick Sort)
		/// Modification: pivot is not left-most element but is instead argument of function
		/// Calculates splitting index and partially sorts elements (swaps them until they are on correct side - depending on pivot)
		/// Complexity: O(n)
		/// </summary>
		/// <param name="start">Start index</param>
		/// <param name="end">End index</param>
		/// <param name="partitionPivot">Pivot that decides boundary between left and right</param>
		/// <param name="axis">Axis of this pivoting</param>
		/// <returns>
		/// Returns splitting index that subdivides array into 2 smaller arrays
		/// left = [start, pivot),
		/// right = [pivot, end)
		/// </returns>
		int Partition(int start, int end, tfloat partitionPivot, int axis) {
			// note: increasing right pointer is actually decreasing!
			int lp = start - 1; // left pointer (negative side)
			int rp = end; // right pointer (positive side)

			var permutationPtr = (int*)this.m_permutation.GetUnsafeReadOnlyPtr();
			while (true) {
				do {
					// move from left to the right until "out of bounds" value is found
					lp++;
				} while (lp < rp && this.Points[*(permutationPtr + lp)].position[axis] < partitionPivot);

				do {
					// move from right to the left until "out of bounds" value found
					rp--;
				} while (lp < rp && this.Points[*(permutationPtr + rp)].position[axis] >= partitionPivot);

				if (lp < rp) {
					// swap
					int temp = *(permutationPtr + lp);
					*(permutationPtr + lp) = *(permutationPtr + rp);
					*(permutationPtr + rp) = temp;
				} else {
					return lp;
				}
			}
		}
		
		public void QueryRange(float3 queryPosition, float radius, NativeList<int> result) {

			// Start with a temp of some size. This will be resized dynamically
			var temp = KnnQueryTemp.Create(32);
			
			// Biggest Smallest Squared Radius
			float bssr = radius * radius;
			float3 rootClosestPoint = this.RootNode.Bounds.ClosestPoint(queryPosition);
			
			temp.PushQueryNode(this.m_rootNodeIndex.Value, rootClosestPoint, queryPosition, 0f);
			
			while (temp.MinHeap.Count > 0) {
				QueryNode queryNode = temp.MinHeap.PopObjMin();

				if (queryNode.Distance > bssr + queryNode.RadiusSqr) {
					continue;
				}

				KdNode node = this.m_nodes[queryNode.NodeIndex];

				if (!node.Leaf) {
					int partitionAxis = node.PartitionAxis;
					var partitionCoord = node.PartitionCoordinate;
					float3 tempClosestPoint = queryNode.TempClosestPoint;

					if (tempClosestPoint[partitionAxis] - partitionCoord < 0) {
						// we already know we are on the side of negative bound/node,
						// so we don't need to test for distance
						// push to stack for later querying
						temp.PushQueryNode(node.NegativeChildIndex, tempClosestPoint, queryPosition, queryNode.RadiusSqr);

						// project the tempClosestPoint to other bound
						tempClosestPoint[partitionAxis] = partitionCoord;

						if (node.Count != 0) {
							temp.PushQueryNode(node.PositiveChildIndex, tempClosestPoint, queryPosition, queryNode.RadiusSqr);
						}
					}
					else {
						// we already know we are on the side of positive bound/node,
						// so we don't need to test for distance
						// push to stack for later querying
						temp.PushQueryNode(node.PositiveChildIndex, tempClosestPoint, queryPosition, queryNode.RadiusSqr);

						// project the tempClosestPoint to other bound
						tempClosestPoint[partitionAxis] = partitionCoord;

						if (node.Count != 0) {
							temp.PushQueryNode(node.NegativeChildIndex, tempClosestPoint, queryPosition, queryNode.RadiusSqr);
						}
					}
				} else {
					for (int i = node.Start; i < node.End; i++) {
						int index = this.m_permutation[i];
						var sqrDist = math.lengthsq(this.Points[index].position - queryPosition) - this.Points[index].radiusSqr;

						if (sqrDist <= bssr) {
							// Unlike the k-query we want to keep _all_ objects in range
							// So resize the heap when pushing this node
							if (temp.MaxHeap.IsFull) {
								temp.MaxHeap.Resize(temp.MaxHeap.Count * 2u);
							}
	
							temp.MaxHeap.PushObjMax(index, sqrDist);
						}
					}
				}
			}

			while (temp.MaxHeap.Count > 0) {
				result.Add(temp.MaxHeap.PopObjMax());
			}

			temp.Dispose();
		}

		public void QueryRange(float3 queryPosition, float radius, ref UnsafeList<T> result) {

			if (this.m_rootNodeIndex.Value == -1) return;
			
			// Start with a temp of some size. This will be resized dynamically
			var temp = KnnQueryTemp.Create(32);
			
			// Biggest Smallest Squared Radius
			float bssr = radius * radius;
			//UnityEngine.Debug.Assert(this.m_nodes.IsCreated);
			//UnityEngine.Debug.Assert(this.m_rootNodeIndex.IsCreated);
			float3 rootClosestPoint = this.RootNode.Bounds.ClosestPoint(queryPosition);
			
			temp.PushQueryNode(this.m_rootNodeIndex.Value, rootClosestPoint, queryPosition, 0f);
			
			while (temp.MinHeap.Count > 0) {
				QueryNode queryNode = temp.MinHeap.PopObjMin();

				if (queryNode.Distance > bssr + queryNode.RadiusSqr) {
					continue;
				}

				KdNode node = this.m_nodes[queryNode.NodeIndex];

				if (!node.Leaf) {
					int partitionAxis = node.PartitionAxis;
					var partitionCoord = node.PartitionCoordinate;
					float3 tempClosestPoint = queryNode.TempClosestPoint;

					if (tempClosestPoint[partitionAxis] - partitionCoord < 0) {
						// we already know we are on the side of negative bound/node,
						// so we don't need to test for distance
						// push to stack for later querying
						temp.PushQueryNode(node.NegativeChildIndex, tempClosestPoint, queryPosition, queryNode.RadiusSqr);

						// project the tempClosestPoint to other bound
						tempClosestPoint[partitionAxis] = partitionCoord;

						if (node.Count != 0) {
							temp.PushQueryNode(node.PositiveChildIndex, tempClosestPoint, queryPosition, queryNode.RadiusSqr);
						}
					}
					else {
						// we already know we are on the side of positive bound/node,
						// so we don't need to test for distance
						// push to stack for later querying
						temp.PushQueryNode(node.PositiveChildIndex, tempClosestPoint, queryPosition, queryNode.RadiusSqr);

						// project the tempClosestPoint to other bound
						tempClosestPoint[partitionAxis] = partitionCoord;

						if (node.Count != 0) {
							temp.PushQueryNode(node.NegativeChildIndex, tempClosestPoint, queryPosition, queryNode.RadiusSqr);
						}
					}
				} else {
					for (int i = node.Start; i < node.End; i++) {
						int index = this.m_permutation[i];
						var sqrDist = math.lengthsq(this.Points[index].position - queryPosition) - this.Points[index].radiusSqr;

						if (sqrDist <= bssr) {
							// Unlike the k-query we want to keep _all_ objects in range
							// So resize the heap when pushing this node
							if (temp.MaxHeap.IsFull) {
								temp.MaxHeap.Resize(temp.MaxHeap.Count * 2);
							}
	
							temp.MaxHeap.PushObjMax(index, sqrDist);
						}
					}
				}
			}

			while (temp.MaxHeap.Count > 0) {
				result.Add(this.Points[temp.MaxHeap.PopObjMax()].data);
			}

			temp.Dispose();
		}

		public uint QueryKNearest(float3 queryPosition, NativeSlice<T> result) {
			return QueryKNearest(queryPosition, tfloat.PositiveInfinity, result);
		}

		public uint QueryKNearest(float3 queryPosition, tfloat range, NativeSlice<T> result) {

			if (this.m_rootNodeIndex.Value == -1) return 0;

			var temp = KnnQueryTemp.Create((uint)result.Length);
			uint k = (uint)result.Length;
			
			// Biggest Smallest Squared Radius
			var bssr = range * range;
			float3 rootClosestPoint = this.RootNode.Bounds.ClosestPoint(queryPosition);
			
			temp.PushQueryNode(this.m_rootNodeIndex.Value, rootClosestPoint, queryPosition, 0f);
			
			while (temp.MinHeap.Count > 0) {
				QueryNode queryNode = temp.MinHeap.PopObjMin();

				if (queryNode.Distance > bssr + queryNode.RadiusSqr) {
					continue;
				}

				KdNode node = this.m_nodes[queryNode.NodeIndex];

				if (!node.Leaf) {
					int partitionAxis = node.PartitionAxis;
					var partitionCoord = node.PartitionCoordinate;
					float3 tempClosestPoint = queryNode.TempClosestPoint;

					if (tempClosestPoint[partitionAxis] - partitionCoord < 0) {
						// we already know we are on the side of negative bound/node,
						// so we don't need to test for distance
						// push to stack for later querying
						temp.PushQueryNode(node.NegativeChildIndex, tempClosestPoint, queryPosition, queryNode.RadiusSqr);

						// project the tempClosestPoint to other bound
						tempClosestPoint[partitionAxis] = partitionCoord;

						if (node.Count != 0) {
							temp.PushQueryNode(node.PositiveChildIndex, tempClosestPoint, queryPosition, queryNode.RadiusSqr);
						}
					} else {
						// we already know we are on the side of positive bound/node,
						// so we don't need to test for distance
						// push to stack for later querying
						temp.PushQueryNode(node.PositiveChildIndex, tempClosestPoint, queryPosition, queryNode.RadiusSqr);

						// project the tempClosestPoint to other bound
						tempClosestPoint[partitionAxis] = partitionCoord;

						if (node.Count != 0) {
							temp.PushQueryNode(node.NegativeChildIndex, tempClosestPoint, queryPosition, queryNode.RadiusSqr);
						}
					}
				} else {
					for (int i = node.Start; i < node.End; i++) {
						int index = this.m_permutation[i];
						var sqrDist = math.lengthsq(this.Points[index].position - queryPosition) - this.Points[index].radiusSqr;

						if (sqrDist <= bssr) {
							temp.MaxHeap.PushObjMax(index, sqrDist);

							if (temp.MaxHeap.Count == k) {
								bssr = temp.MaxHeap.HeadValue;
							}
						}
					}
				}
			}

			k = temp.MaxHeap.Count;
			for (int i = 0; i < k; i++) {
				result[i] = this.Points[temp.MaxHeap.PopObjMax()].data;
			}
			
			temp.Dispose();

			return k;
		}
	}
}
