using Unity.Collections.LowLevel.Unsafe;

namespace Unity.Physics
{
    /// <summary>
    /// Non-generic temporary stand-in for Unity BlobArray. This is to work around C# wanting to
    /// treat any struct containing the generic Unity.BlobArray&lt;T&gt; as a managed struct.
    /// </summary>
    public struct BlobArray
    {
        internal int Offset;
        internal int Length;    // number of T, not number of bytes

        /// <summary>   Generic accessor. </summary>
        ///
        /// <typeparam name="T">    Generic type parameter. </typeparam>
        public unsafe struct Accessor<T> where T : struct
        {
            private readonly int* m_OffsetPtr;

            /// <summary>   Gets the length. </summary>
            ///
            /// <value> The length. </value>
            public int Length { get; private set; }

            /// <summary>   Constructor. </summary>
            ///
            /// <param name="blobArray">    [in,out] Array of blobs. </param>
            public Accessor(ref BlobArray blobArray)
            {
                fixed(BlobArray* ptr = &blobArray)
                {
                    m_OffsetPtr = &ptr->Offset;
                    Length = ptr->Length;
                }
            }

            /// <summary>   Indexer to get items within this collection using array index syntax. </summary>
            ///
            /// <param name="index">    Zero-based index of the entry to access. </param>
            ///
            /// <returns>   The indexed item. </returns>
            public ref T this[int index]
            {
                get
                {
                    SafetyChecks.CheckIndexAndThrow(index, Length);
                    return ref UnsafeUtility.ArrayElementAsRef<T>((byte*)m_OffsetPtr + *m_OffsetPtr, index);
                }
            }

            /// <summary>   Gets the enumerator. </summary>
            ///
            /// <returns>   The enumerator. </returns>
            public Enumerator GetEnumerator() => new Enumerator(m_OffsetPtr, Length);


            /// <summary>   An enumerator. </summary>
            public struct Enumerator
            {
                private readonly int* m_OffsetPtr;
                private readonly int m_Length;
                private int m_Index;

                /// <summary>   Gets the current. </summary>
                ///
                /// <value> The current. </value>
                public T Current => UnsafeUtility.ArrayElementAsRef<T>((byte*)m_OffsetPtr + *m_OffsetPtr, m_Index);

                /// <summary>   Constructor. </summary>
                ///
                /// <param name="offsetPtr">    [in,out] If non-null, the offset pointer. </param>
                /// <param name="length">       The length. </param>
                public Enumerator(int* offsetPtr, int length)
                {
                    m_OffsetPtr = offsetPtr;
                    m_Length = length;
                    m_Index = -1;
                }

                /// <summary>   Determines if we can move to the next element. </summary>
                ///
                /// <returns>   True if it is possible, false otherwise. </returns>
                public bool MoveNext()
                {
                    return ++m_Index < m_Length;
                }
            }
        }
    }
}
