//
// ByteBuffer.cs
//
// Author:
//   Jb Evain (jbevain@novell.com)
//
// (C) 2009 - 2010 Novell, Inc. (http://www.novell.com)
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
//
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

using System;

namespace ME.BECS.Mono.Reflection {

    internal class ByteBuffer {

        internal byte[] buffer;
        internal int position;

        public ByteBuffer(byte[] buffer) {
            this.buffer = buffer;
        }

        public byte ReadByte() {
            this.CheckCanRead(1);
            return this.buffer[this.position++];
        }

        public byte[] ReadBytes(int length) {
            this.CheckCanRead(length);
            var value = new byte [length];
            Buffer.BlockCopy(this.buffer, this.position, value, 0, length);
            this.position += length;
            return value;
        }

        public short ReadInt16() {
            this.CheckCanRead(2);
            var value = (short)(this.buffer[this.position]
                                | (this.buffer[this.position + 1] << 8));
            this.position += 2;
            return value;
        }

        public int ReadInt32() {
            this.CheckCanRead(4);
            var value = this.buffer[this.position]
                        | (this.buffer[this.position + 1] << 8)
                        | (this.buffer[this.position + 2] << 16)
                        | (this.buffer[this.position + 3] << 24);
            this.position += 4;
            return value;
        }

        public long ReadInt64() {
            this.CheckCanRead(8);
            var low = (uint)(this.buffer[this.position]
                             | (this.buffer[this.position + 1] << 8)
                             | (this.buffer[this.position + 2] << 16)
                             | (this.buffer[this.position + 3] << 24));

            var high = (uint)(this.buffer[this.position + 4]
                              | (this.buffer[this.position + 5] << 8)
                              | (this.buffer[this.position + 6] << 16)
                              | (this.buffer[this.position + 7] << 24));

            var value = ((long)high << 32) | low;
            this.position += 8;
            return value;
        }

        public float ReadSingle() {
            if (!BitConverter.IsLittleEndian) {
                var bytes = this.ReadBytes(4);
                Array.Reverse(bytes);
                return BitConverter.ToSingle(bytes, 0);
            }

            this.CheckCanRead(4);
            var value = BitConverter.ToSingle(this.buffer, this.position);
            this.position += 4;
            return value;
        }

        public double ReadDouble() {
            if (!BitConverter.IsLittleEndian) {
                var bytes = this.ReadBytes(8);
                Array.Reverse(bytes);
                return BitConverter.ToDouble(bytes, 0);
            }

            this.CheckCanRead(8);
            var value = BitConverter.ToDouble(this.buffer, this.position);
            this.position += 8;
            return value;
        }

        private void CheckCanRead(int count) {
            if (this.position + count > this.buffer.Length) {
                throw new ArgumentOutOfRangeException();
            }
        }

    }

}