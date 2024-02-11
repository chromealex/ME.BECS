using System;
using System.Diagnostics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Unity.Entities
{
    /// <summary>
    /// RefRW stores a reference (with write access) to native memory
    /// </summary>
    /// <typeparam name="T">The type of object referenced.</typeparam>
    public readonly struct RefRW<T> : IQueryTypeParameter where T : struct, IComponentData
    {
        /// <summary>
        /// Convert into a read-only version RefRO of this RefRW
        /// </summary>
        /// <param name="refRW">The read-write reference to convert to read-only</param>
        /// <returns>Returns a RefRO</returns>
        public static unsafe explicit operator RefRO<T>(RefRW<T> refRW)
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            => new RefRO<T>(refRW._Data, refRW._Safety);
#else
            => new RefRO<T>(refRW._Data);
#endif

        readonly private unsafe byte*      _Data;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        readonly AtomicSafetyHandle _Safety;
#endif

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS"), Conditional("UNITY_DOTS_DEBUG")]
        void OutOfBoundsArrayConstructor(int index, int length)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            if (index < 0 || index >= length)
                throw new ArgumentOutOfRangeException("index is out of bounds of NativeArray<>.Length.");
#endif
        }

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        /// <summary>
        /// Stores a safe reference a pointer to T
        /// </summary>
        /// <param name="ptr">Pointer pointing to an instance of T</param>
        /// <param name="safety">AtomicSafetyHandle protecting access to 'ptr'</param>
        public unsafe RefRW(byte* ptr, AtomicSafetyHandle safety)
#else
        /// <summary>
        /// Stores a safe reference a pointer to T
        /// </summary>
        /// <param name="ptr">Pointer pointing to an instance of T</param>
        public unsafe RefRW(byte* ptr)
#endif
        {
            _Data = ptr;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            _Safety = safety;
#endif
        }

        /// <summary>
        /// Stores a safe reference to a component from an array of components at the index.
        /// </summary>
        /// <param name="componentDataNativeArray">The array of components.</param>
        /// <param name="index">The index of the array.</param>
        public unsafe RefRW(NativeArray<T> componentDataNativeArray, int index)
        {
            _Data = (byte*) NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(componentDataNativeArray);
            _Data += UnsafeUtility.SizeOf<T>() * index;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            _Safety = NativeArrayUnsafeUtility.GetAtomicSafetyHandle(componentDataNativeArray);
#endif
            OutOfBoundsArrayConstructor(index, componentDataNativeArray.Length);
        }

        /// <summary>
        /// Stores a safe reference to a component from an array of components at the index.
        /// If the array is empty stores a null reference.
        /// </summary>
        /// <param name="componentDataNativeArray">The array of components.</param>
        /// <param name="index">The index of the array.</param>
        /// <returns>Read-write optional reference to component</returns>
        public static RefRW<T> Optional(NativeArray<T> componentDataNativeArray, int index)
        {
            return componentDataNativeArray.Length == 0 ? default : new RefRW<T>(componentDataNativeArray, index);
        }

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        internal unsafe RefRW(void* ptr, AtomicSafetyHandle safety)
        {
            _Data = (byte*)ptr;
            _Safety = safety;
        }
#else
        internal unsafe RefRW(void* ptr)
        {
            _Data = (byte*)ptr;
        }
#endif
        /// <summary>
        /// Checks if the component exists on this entity.
        /// </summary>
        /// <remarks>
        /// This doesn't take into account if the component is enabled or not.
        /// </remarks>
        public unsafe bool IsValid => _Data != null;

        /// <summary>
        /// Returns a writable reference to the component value itself.
        /// </summary>
        /// <remarks>
        /// This value is a reference to the actual component data.  It is safe to use this field directly, e.g.
        /// "Data.ValueRW.SomeField = 123".  It is also safe to make a copy of this value, e.g. "var myComponent = Data.ValueRW".
        /// Keeping a ref ("ref var myref = Data.ValueRW" is inherently unsafe as any structural change may invalidate this
        /// reference, and there is no way to detect this. It is safe to use this reference locally if you can guarantee
        /// that no structural changes will occur in between acquiring it and using it. Do not hold on to such a reference
        /// for any extended amount of time.
        /// </remarks>
        public unsafe ref T ValueRW
        {
            get
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckWriteAndThrow(_Safety);
#endif
                return ref UnsafeUtility.AsRef<T>(_Data);
            }
        }

        /// <summary>
        /// Returns a read-only reference to the component value itself.
        /// </summary>
        /// <remarks>
        /// This value is a reference to the actual component data.  It is safe to use this field directly, e.g.
        /// "Data.ValueRO.SomeField".  It is also safe to make a copy of this value, e.g. "var myComponent = Data.ValueRO".
        /// Keeping a ref ("ref var myref = Data.ValueRO" is inherently unsafe as any structural change may invalidate this
        /// reference, and there is no way to detect this. It is safe to use this reference locally if you can guarantee
        /// that no structural changes will occur in between acquiring it and using it. Do not hold on to such a reference
        /// for any extended amount of time.
        /// </remarks>
        public unsafe ref readonly T ValueRO
        {
            get
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckReadAndThrow(_Safety);
#endif
                return ref UnsafeUtility.AsRef<T>(_Data);
            }
        }
    }
}
