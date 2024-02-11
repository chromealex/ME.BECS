using System;
using System.IO;
using UnityEngine.Assertions;
using Unity.IO.LowLevel.Unsafe;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Unity.Entities.Serialization
{

    /// <summary>
    /// An interface that writes primitive types to a binary buffer.
    /// </summary>
    /// <seealso cref="MemoryBinaryWriter"/>
    public interface BinaryWriter : IDisposable
    {
        /// <summary>
        /// Writes the specified number of bytes.
        /// </summary>
        /// <param name="data">The data to be written.</param>
        /// <param name="bytes">The number of bytes to write.</param>
        unsafe void WriteBytes(void* data, int bytes);

        /// <summary>
        /// Gets or sets the current write position of the BinaryWriter.
        /// </summary>
        long Position { get; set; }
    }

    /// <summary>
    /// Provides write methods for a BinaryWriter.
    /// </summary>
    public static unsafe class BinaryWriterExtensions
    {
        /// <summary>
        /// Writes a single byte.
        /// </summary>
        /// <param name="writer">The BinaryReader to write to.</param>
        /// <param name="value">The data to write.</param>
        public static void Write(this BinaryWriter writer, byte value)
        {
            writer.WriteBytes(&value, 1);
        }

        /// <summary>
        /// Writes a single int.
        /// </summary>
        /// <param name="writer">The BinaryReader to write to.</param>
        /// <param name="value">The data to write.</param>
        public static void Write(this BinaryWriter writer, int value)
        {
            writer.WriteBytes(&value, sizeof(int));
        }

        /// <summary>
        /// Writes a single ulong.
        /// </summary>
        /// <param name="writer">The BinaryReader to write to.</param>
        /// <param name="value">The data to write.</param>
        public static void Write(this BinaryWriter writer, ulong value)
        {
            writer.WriteBytes(&value, sizeof(ulong));
        }

        /// <summary>
        /// Writes a byte array.
        /// </summary>
        /// <param name="writer">The BinaryReader to write to.</param>
        /// <param name="bytes">The data to write.</param>
        public static void Write(this BinaryWriter writer, byte[] bytes)
        {
            fixed(byte* p = bytes)
            {
                writer.WriteBytes(p, bytes.Length);
            }
        }

        /// <summary>
        /// Writes data from a native array.
        /// </summary>
        /// <param name="writer">The BinaryReader to write to.</param>
        /// <param name="data">The data to write.</param>
        /// <typeparam name="T">The type of data to write from the native array.</typeparam>
        public static void WriteArray<T>(this BinaryWriter writer, NativeArray<T> data) where T: struct
        {
            writer.WriteBytes(data.GetUnsafeReadOnlyPtr(), data.Length * UnsafeUtility.SizeOf<T>());
        }

        /// <summary>
        /// Writes data from a native list.
        /// </summary>
        /// <param name="writer">The BinaryReader to write to.</param>
        /// <param name="data">The data to write.</param>
        /// <typeparam name="T">The type of data to write from the native list.</typeparam>
        public static void WriteList<T>(this BinaryWriter writer, NativeList<T> data) where T: unmanaged
        {
            writer.WriteBytes(data.GetUnsafePtr(), data.Length * UnsafeUtility.SizeOf<T>());
        }

        /// <summary>
        /// Writes the specified number of elements from a native list.
        /// </summary>
        /// <param name="writer">The BinaryReader to write to.</param>
        /// <param name="data">The data to write.</param>
        /// <param name="index">The index at which to start writing from.</param>
        /// <param name="count">The number of elements to write.</param>
        /// <typeparam name="T">The type of data to write from the native list.</typeparam>
        /// <exception cref="ArgumentException">Throws if the index is outside of the buffer range.</exception>
        public static void WriteList<T>(this BinaryWriter writer, NativeList<T> data, int index, int count) where T: unmanaged
        {
            if (index + count > data.Length)
            {
                throw new ArgumentException("index + count must not go beyond the end of the list");
            }
            var size = UnsafeUtility.SizeOf<T>();
            writer.WriteBytes((byte*)data.GetUnsafePtr() + size*index, count * size);
        }
    }

    /// <summary>
    /// An interface that reads primitive types from a binary buffer.
    /// </summary>
    /// <seealso cref="MemoryBinaryReader"/>
    public interface BinaryReader : IDisposable
    {
        /// <summary>
        /// Reads the specified number of bytes.
        /// </summary>
        /// <param name="data">The read data.</param>
        /// <param name="bytes">The number of bytes to read.</param>
        unsafe void ReadBytes(void* data, int bytes);

        /// <summary>
        /// Gets or sets the current read position of the BinaryReader.
        /// </summary>
        long Position { get; set; }
    }

    /// <summary>
    /// Provides additional read methods for the BinaryReader.
    /// </summary>
    public static unsafe class BinaryReaderExtensions
    {
        /// <summary>
        /// Reads a single byte.
        /// </summary>
        /// <param name="reader">The BinaryReader to read from.</param>
        /// <returns>The read data</returns>
        public static byte ReadByte(this BinaryReader reader)
        {
            byte value;
            reader.ReadBytes(&value, 1);
            return value;
        }

        /// <summary>
        /// Reads a single int.
        /// </summary>
        /// <param name="reader">The BinaryReader to read from.</param>
        /// <returns>The read data.</returns>
        public static int ReadInt(this BinaryReader reader)
        {
            int value;
            reader.ReadBytes(&value, sizeof(int));
            return value;
        }

        /// <summary>
        /// Reads a single ulong.
        /// </summary>
        /// <param name="reader">The BinaryReader to read from.</param>
        /// <returns>The read data.</returns>
        public static ulong ReadULong(this BinaryReader reader)
        {
            ulong value;
            reader.ReadBytes(&value, sizeof(ulong));
            return value;
        }

        /// <summary>
        /// Reads the specified number of elements from a native byte array.
        /// </summary>
        /// <param name="reader">The BinaryReader to read from.</param>
        /// <param name="elements">The native array to read of.</param>
        /// <param name="count">The number of elements to read.</param>
        /// <param name="offset">The offset at which to start reading.</param>
        public static void ReadBytes(this BinaryReader reader, NativeArray<byte> elements, int count, int offset = 0)
        {
            byte* destination = (byte*)elements.GetUnsafePtr() + offset;
            reader.ReadBytes(destination, count);
        }

        /// <summary>
        /// Reads the specified number of elements from a native array.
        /// </summary>
        /// <param name="reader">The BinaryReader to read from.</param>
        /// <param name="elements">The native array to read of.</param>
        /// <param name="count">The number of elements to read.</param>
        /// <typeparam name="T">The type of the elements in the native array.</typeparam>
        public static void ReadArray<T>(this BinaryReader reader, NativeArray<T> elements, int count) where T: struct
        {
            reader.ReadBytes((byte*)elements.GetUnsafeReadOnlyPtr(), count * UnsafeUtility.SizeOf<T>());
        }
    }

    internal unsafe class StreamBinaryReader : BinaryReader
    {
        internal string FilePath { get; }
#if UNITY_EDITOR
        private Stream stream;
        private byte[] buffer;
        public long Position
        {
            get => stream.Position;
            set => stream.Position = value;
        }
#else
        public long Position { get; set; }
#endif

        public StreamBinaryReader(string filePath, long bufferSize = 65536)
        {
            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentException("The filepath can neither be null nor empty", nameof(filePath));

            FilePath = filePath;
            #if UNITY_EDITOR
            stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            buffer = new byte[bufferSize];
            #else
            Position = 0;
            #endif
        }

        public void Dispose()
        {
            #if UNITY_EDITOR
            stream.Dispose();
            #endif
        }

        public void ReadBytes(void* data, int bytes)
        {
            #if UNITY_EDITOR
            int remaining = bytes;
            int bufferSize = buffer.Length;

            fixed(byte* fixedBuffer = buffer)
            {
                while (remaining != 0)
                {
                    int read = stream.Read(buffer, 0, Math.Min(remaining, bufferSize));
                    remaining -= read;
                    UnsafeUtility.MemCpy(data, fixedBuffer, read);
                    data = (byte*)data + read;
                }
            }
            #else
            var readCmd = new ReadCommand
            {
                Size = bytes, Offset = Position, Buffer = data
            };
            Assert.IsFalse(string.IsNullOrEmpty(FilePath));
#if ENABLE_PROFILER
            // When AsyncReadManagerMetrics are available, mark up the file read for more informative IO metrics.
            // Metrics can be retrieved by AsyncReadManagerMetrics.GetMetrics
            var readHandle = AsyncReadManager.Read(FilePath, &readCmd, 1, subsystem: AssetLoadingSubsystem.EntitiesStreamBinaryReader);
#else
            var readHandle = AsyncReadManager.Read(FilePath, &readCmd, 1);
#endif
            readHandle.JobHandle.Complete();

            if (readHandle.Status != ReadStatus.Complete)
            {
                throw new IOException($"Failed to read from {FilePath}!");
            }
            Position += bytes;
            #endif
        }
    }

    internal unsafe class StreamBinaryWriter : BinaryWriter
    {
        private Stream stream;
        private byte[] buffer;
        public long Position
        {
            get => stream.Position;
            set => stream.Position = value;
        }

        public StreamBinaryWriter(string fileName, int bufferSize = 65536)
        {
            stream = File.Open(fileName, FileMode.Create, FileAccess.Write);
            buffer = new byte[bufferSize];
        }

        public void Dispose()
        {
            stream.Dispose();
        }

        public void WriteBytes(void* data, int bytes)
        {
            int remaining = bytes;
            int bufferSize = buffer.Length;

            fixed (byte* fixedBuffer = buffer)
            {
                while (remaining != 0)
                {
                    int bytesToWrite = Math.Min(remaining, bufferSize);
                    UnsafeUtility.MemCpy(fixedBuffer, data, bytesToWrite);
                    stream.Write(buffer, 0, bytesToWrite);
                    data = (byte*) data + bytesToWrite;
                    remaining -= bytesToWrite;
                }
            }
        }

        public long Length => stream.Length;
    }

    /// <summary>
    /// Provides a writer to write primitive types to a binary buffer in memory.
    /// </summary>
    /// <remarks>
    /// This class can be used to serialize, for example, blob assets.
    /// The resulting binary buffer is stored in memory.
    /// </remarks>
    /// <example><code>
    /// struct MyData
    /// {
    ///     public float embeddedFloat;
    ///     public BlobString str;
    /// }
    /// public void WriteBlobAsset()
    /// {
    ///     unsafe
    ///     {
    ///         var writer = new MemoryBinaryWriter();
    ///         var blobBuilder = new BlobBuilder(Allocator.Temp);
    ///         ref var root = ref blobBuilder.ConstructRoot&lt;MyData&gt;();
    ///         builder.AllocateString(ref root.str, "Hello World!");
    ///         root.embeddedFloat = 4;
    ///         BlobAssetReference&lt;MyData>.Write(writer, blobBuilder, kVersion);
    ///     }
    /// }
    /// </code></example>
    public unsafe class MemoryBinaryWriter : BinaryWriter
    {
        NativeList<byte> content = new NativeList<byte>(Allocator.Temp);

        /// <summary>
        /// A pointer to the data that has been written to memory.
        /// </summary>
        public byte* Data => (byte*)content.GetUnsafePtr();

        /// <summary>
        /// The total length of the all written data.
        /// </summary>
        public int Length => content.Length;

        /// <summary>
        /// Gets or sets the current write position of the MemoryBinaryWriter.
        /// </summary>
        public long Position { get; set; }

        /// <summary>
        /// Disposes the MemoryBinaryWriter.
        /// </summary>
        public void Dispose()
        {
            content.Dispose();
        }

        internal NativeArray<byte> GetContentAsNativeArray() => content.AsArray();

        /// <summary>
        /// Writes the specified number of bytes and advances the current write position by that number of bytes.
        /// </summary>
        /// <param name="data">The data to write.</param>
        /// <param name="bytes">The number of bytes to write.</param>
        public void WriteBytes(void* data, int bytes)
        {
            content.ResizeUninitialized((int)Position + bytes);
            UnsafeUtility.MemCpy((byte*)content.GetUnsafePtr() + (int)Position, data, bytes);
            Position += bytes;
        }
    }

    /// <summary>
    /// Provides a reader to read primitive types from a binary buffer in memory.
    /// </summary>
    /// <remarks>
    /// This class can be used to deserialize, for example, blob assets.
    ///
    /// By default, the MemoryBinaryReader can't be Burst-compiled. It can be cast into
    /// <see cref="BurstableMemoryBinaryReader"/> in order to be used in a burst compiled context.
    /// </remarks>
    /// <example><code>
    /// struct MyData
    /// {
    ///     public float embeddedFloat;
    ///     public BlobString str;
    /// }
    /// public void ReadBlobAsset(void* buffer, int length, int version)
    /// {
    ///     unsafe
    ///     {
    ///         var reader = new MemoryBinaryReader(writer.Data, writer.Length);
    ///         var result = BlobAssetReference&lt;MyData>.TryRead(reader, version, out var blobResult);
    ///         ref MyData value = ref blobResult.Value;
    ///         Debug.Log($"blob float = {value.embeddedFloat}");
    ///         blobResult.Dispose();
    ///     }
    /// }
    /// </code></example>
    public unsafe class MemoryBinaryReader : BinaryReader
    {
        readonly byte* content;
        readonly long length;

        /// <summary>
        /// Gets or sets the current read position of the MemoryBinaryReader.
        /// </summary>
        public long Position { get; set; }

        /// <summary>
        /// Initializes and returns an instance of MemoryBinaryReader.
        /// </summary>
        /// <param name="content">A pointer to the data to read.</param>
        /// <param name="length">The length of the data to read.</param>
        public MemoryBinaryReader(byte* content, long length)
        {
            this.content = content;
            this.length = length;
            Position = 0L;
        }

        /// <summary>
        /// Disposes the MemoryBinaryReader.
        /// </summary>
        public void Dispose()
        {
        }

        /// <summary>
        /// Reads the specified number of bytes and advances the current read position by that number of bytes.
        /// </summary>
        /// <param name="data">The read data.</param>
        /// <param name="bytes">The number of bytes to read.</param>
        /// <exception cref="ArgumentException">Thrown if attempting read beyond the end of the memory block.</exception>
        public void ReadBytes(void* data, int bytes)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            if (Position + bytes > length)
                throw new ArgumentException("ReadBytes reads beyond end of memory block");
#endif
            UnsafeUtility.MemCpy(data, content + Position, bytes);
            Position += bytes;
        }

        /// <summary>
        /// Converts this MemoryBinaryReader into a BurstableMemoryBinaryReader.
        /// </summary>
        /// <param name="src">The source MemoryBinaryReader.</param>
        /// <returns>The BurstableMemoryBinaryReader.</returns>
        public static explicit operator BurstableMemoryBinaryReader(MemoryBinaryReader src)
        {
            return new BurstableMemoryBinaryReader {content = src.content, length = src.length, Position = src.Position};
        }
    }

    /// <summary>
    /// Provides a reader compatible with the Burst compiler that can read primitive types from a binary buffer in memory.
    /// </summary>
    /// <seealso cref="MemoryBinaryReader"/>
    [GenerateTestsForBurstCompatibility]
    public unsafe struct BurstableMemoryBinaryReader : BinaryReader
    {
        internal byte* content;
        internal long length;

        /// <summary>
        /// Gets or sets the current read position of the BurstableMemoryBinaryReader.
        /// </summary>
        public long Position { get; set; }

        /// <summary>
        /// Initializes and returns an instance of BurstableMemoryBinaryReader.
        /// </summary>
        /// <param name="content">A pointer to the data to read.</param>
        /// <param name="length">The length of the data to read.</param>
        public BurstableMemoryBinaryReader(byte* content, long length)
        {
            this.content = content;
            this.length = length;
            Position = 0L;
        }

        /// <summary>
        /// Disposes the MemoryBinaryReader.
        /// </summary>
        public void Dispose()
        {
        }

        /// <summary>
        /// Reads a byte and advances the current read position by a byte.
        /// </summary>
        /// <returns>The read data.</returns>
        /// <exception cref="ArgumentException">Thrown if attempting read beyond the end of the memory block.</exception>
        public byte ReadByte()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            if (Position + sizeof(byte) > length)
                throw new ArgumentException("ReadByte reads beyond end of memory block");
#endif
            var res = *(content + Position);
            Position += sizeof(byte);
            return res;
        }

        /// <summary>
        /// Reads an int and advances the current read position by the number of bytes in an int.
        /// </summary>
        /// <returns>The read data.</returns>
        /// <exception cref="ArgumentException">Thrown if attempting read beyond the end of the memory block.</exception>
        public int ReadInt()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            if (Position + sizeof(int) > length)
                throw new ArgumentException("ReadInt reads beyond end of memory block");
#endif
            var res = *(int*) (content + Position);
            Position += sizeof(int);
            return res;
        }

        /// <summary>
        /// Reads an ulong and advances the current read position by the number of bytes in an ulong.
        /// </summary>
        /// <returns>The read data.</returns>
        /// <exception cref="ArgumentException">Thrown if attempting read beyond the end of the memory block.</exception>
        public ulong ReadULong()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            if (Position + sizeof(ulong) > length)
                throw new ArgumentException("ReadULong reads beyond end of memory block");
#endif
            var res = *(ulong*) (content + Position);
            Position += sizeof(ulong);
            return res;
        }

        /// <summary>
        /// Reads the specified number of bytes and advances the current read position by that number of bytes.
        /// </summary>
        /// <param name="data">The read data.</param>
        /// <param name="bytes">The number of bytes to read.</param>
        /// <exception cref="ArgumentException">Thrown if attempting read beyond the end of the memory block.</exception>
        public void ReadBytes(void* data, int bytes)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            if (Position + bytes > length)
                throw new ArgumentException("ReadBytes reads beyond end of memory block");
#endif
            UnsafeUtility.MemCpy(data, content + Position, bytes);
            Position += bytes;
        }
    }
}
