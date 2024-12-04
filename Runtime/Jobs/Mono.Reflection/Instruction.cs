//
// Instruction.cs
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
using System.Reflection.Emit;
using System.Text;

namespace ME.BECS.Mono.Reflection {

    public sealed class Instruction {

        private int offset;
        private OpCode opcode;
        private object operand;

        private Instruction previous;
        private Instruction next;

        public int Offset => this.offset;

        public OpCode OpCode => this.opcode;

        public object Operand {
            get => this.operand;
            internal set => this.operand = value;
        }

        public Instruction Previous {
            get => this.previous;
            internal set => this.previous = value;
        }

        public Instruction Next {
            get => this.next;
            internal set => this.next = value;
        }

        public int Size {
            get {
                var size = this.opcode.Size;

                switch (this.opcode.OperandType) {
                    case OperandType.InlineSwitch:
                        size += (1 + ((Instruction[])this.operand).Length) * 4;
                        break;

                    case OperandType.InlineI8:
                    case OperandType.InlineR:
                        size += 8;
                        break;

                    case OperandType.InlineBrTarget:
                    case OperandType.InlineField:
                    case OperandType.InlineI:
                    case OperandType.InlineMethod:
                    case OperandType.InlineString:
                    case OperandType.InlineTok:
                    case OperandType.InlineType:
                    case OperandType.ShortInlineR:
                        size += 4;
                        break;

                    case OperandType.InlineVar:
                        size += 2;
                        break;

                    case OperandType.ShortInlineBrTarget:
                    case OperandType.ShortInlineI:
                    case OperandType.ShortInlineVar:
                        size += 1;
                        break;
                }

                return size;
            }
        }

        internal Instruction(int offset, OpCode opcode) {
            this.offset = offset;
            this.opcode = opcode;
        }

        public override string ToString() {
            var instruction = new StringBuilder();

            AppendLabel(instruction, this);
            instruction.Append(':');
            instruction.Append(' ');
            instruction.Append(this.opcode.Name);

            if (this.operand == null) {
                return instruction.ToString();
            }

            instruction.Append(' ');

            switch (this.opcode.OperandType) {
                case OperandType.ShortInlineBrTarget:
                case OperandType.InlineBrTarget:
                    AppendLabel(instruction, (Instruction)this.operand);
                    break;

                case OperandType.InlineSwitch:
                    var labels = (Instruction[])this.operand;
                    for (var i = 0; i < labels.Length; i++) {
                        if (i > 0) {
                            instruction.Append(',');
                        }

                        AppendLabel(instruction, labels[i]);
                    }

                    break;

                case OperandType.InlineString:
                    instruction.Append('\"');
                    instruction.Append(this.operand);
                    instruction.Append('\"');
                    break;

                default:
                    instruction.Append(this.operand);
                    break;
            }

            return instruction.ToString();
        }

        private static void AppendLabel(StringBuilder builder, Instruction instruction) {
            builder.Append("IL_");
            builder.Append(instruction.offset.ToString("x4"));
        }

    }

}