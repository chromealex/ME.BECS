//
// Image.cs
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
using System.IO;

namespace ME.BECS.Mono.Reflection {

    public sealed class Image : IDisposable {

        private long position;
        private Stream stream;

        private Image(Stream stream) {
            this.stream = stream;
            this.position = stream.Position;
            this.stream.Position = 0;
        }

        private bool Advance(int length) {
            if (this.stream.Position + length >= this.stream.Length) {
                return false;
            }

            this.stream.Seek(length, SeekOrigin.Current);
            return true;
        }

        private bool MoveTo(uint position) {
            if (position >= this.stream.Length) {
                return false;
            }

            this.stream.Position = position;
            return true;
        }

        void IDisposable.Dispose() {
            this.stream.Position = this.position;
        }

        private ushort ReadUInt16() {
            return (ushort)(this.stream.ReadByte()
                            | (this.stream.ReadByte() << 8));
        }

        private uint ReadUInt32() {
            return (uint)(this.stream.ReadByte()
                          | (this.stream.ReadByte() << 8)
                          | (this.stream.ReadByte() << 16)
                          | (this.stream.ReadByte() << 24));
        }

        private bool IsManagedAssembly() {
            if (this.stream.Length < 318) {
                return false;
            }

            if (this.ReadUInt16() != 0x5a4d) {
                return false;
            }

            if (!this.Advance(58)) {
                return false;
            }

            if (!this.MoveTo(this.ReadUInt32())) {
                return false;
            }

            if (this.ReadUInt32() != 0x00004550) {
                return false;
            }

            if (!this.Advance(20)) {
                return false;
            }

            if (!this.Advance(this.ReadUInt16() == 0x20b ? 222 : 206)) {
                return false;
            }

            return this.ReadUInt32() != 0;
        }

        public static bool IsAssembly(string file) {
            if (file == null) {
                throw new ArgumentNullException("file");
            }

            using (var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read)) {
                return IsAssembly(stream);
            }
        }

        public static bool IsAssembly(Stream stream) {
            if (stream == null) {
                throw new ArgumentNullException("stream");
            }

            if (!stream.CanRead) {
                throw new ArgumentException("Can not read from stream");
            }

            if (!stream.CanSeek) {
                throw new ArgumentException("Can not seek in stream");
            }

            using (var image = new Image(stream)) {
                return image.IsManagedAssembly();
            }
        }

    }

}