//
// ILPattern.cs
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
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace ME.BECS.Mono.Reflection {

    /// <summary>
    /// Defines IL pattern state and operations.
    /// </summary>
    public abstract class ILPattern {

        /// <summary>
        /// Configures a value or dependency that may be absent.
        /// </summary>
        public static ILPattern Optional(OpCode opcode) {
            return Optional(OpCode(opcode));
        }

        /// <summary>
        /// Configures a value or dependency that may be absent.
        /// </summary>
        public static ILPattern Optional(params OpCode[] opcodes) {
            return Optional(Sequence(opcodes.Select(opcode => OpCode(opcode)).ToArray()));
        }

        /// <summary>
        /// Configures a value or dependency that may be absent.
        /// </summary>
        public static ILPattern Optional(ILPattern pattern) {
            return new OptionalPattern(pattern);
        }

        private class OptionalPattern : ILPattern {

            private ILPattern pattern;

            public OptionalPattern(ILPattern optional) {
                this.pattern = optional;
            }

            public override void Match(MatchContext context) {
                this.pattern.TryMatch(context);
            }

        }

        /// <summary>
        /// Builds or matches an ordered sequence of operations.
        /// </summary>
        public static ILPattern Sequence(params ILPattern[] patterns) {
            return new SequencePattern(patterns);
        }

        private class SequencePattern : ILPattern {

            private ILPattern[] patterns;

            public SequencePattern(ILPattern[] patterns) {
                this.patterns = patterns;
            }

            public override void Match(MatchContext context) {
                foreach (var pattern in this.patterns) {
                    pattern.Match(context);

                    if (!context.success) {
                        break;
                    }
                }
            }

        }

        /// <summary>
        /// Matches the requested intermediate-language operation code.
        /// </summary>
        public static ILPattern OpCode(OpCode opcode) {
            return new OpCodePattern(opcode);
        }

        private class OpCodePattern : ILPattern {

            private OpCode opcode;

            public OpCodePattern(OpCode opcode) {
                this.opcode = opcode;
            }

            public override void Match(MatchContext context) {
                if (context.instruction == null) {
                    context.success = false;
                    return;
                }

                context.success = context.instruction.OpCode == this.opcode;
                context.Advance();
            }

        }

        /// <summary>
        /// Combines alternatives that may satisfy the matching rule.
        /// </summary>
        public static ILPattern Either(ILPattern a, ILPattern b) {
            return new EitherPattern(a, b);
        }

        private class EitherPattern : ILPattern {

            private ILPattern a;
            private ILPattern b;

            public EitherPattern(ILPattern a, ILPattern b) {
                this.a = a;
                this.b = b;
            }

            public override void Match(MatchContext context) {
                if (!this.a.TryMatch(context)) {
                    this.b.Match(context);
                }
            }

        }

        /// <summary>
        /// Tests the supplied value against the configured matching rule.
        /// </summary>
        public abstract void Match(MatchContext context);

        /// <summary>
        /// Returns last matching instruction.
        /// </summary>
        protected static Instruction GetLastMatchingInstruction(MatchContext context) {
            if (context.instruction == null) {
                return null;
            }

            return context.instruction.Previous;
        }

        /// <summary>
        /// Attempts to match and reports whether the operation succeeded.
        /// </summary>
        public bool TryMatch(MatchContext context) {
            var instruction = context.instruction;
            this.Match(context);

            if (context.success) {
                return true;
            }

            context.Reset(instruction);
            return false;
        }

        /// <summary>
        /// Tests the supplied value against the configured matching rule.
        /// </summary>
        public static MatchContext Match(MethodBase method, ILPattern pattern) {
            if (method == null) {
                throw new ArgumentNullException("method");
            }

            if (pattern == null) {
                throw new ArgumentNullException("pattern");
            }

            var instructions = method.GetInstructions();
            if (instructions.Count == 0) {
                throw new ArgumentException();
            }

            var context = new MatchContext(instructions[0]);
            pattern.Match(context);
            return context;
        }

    }

    /// <summary>
    /// Defines match context state and operations.
    /// </summary>
    public sealed class MatchContext {

        internal Instruction instruction;
        internal bool success;

        private Dictionary<object, object> data = new();

        /// <summary>
        /// Indicates is match.
        /// </summary>
        public bool IsMatch {
            get => this.success;
            set => this.success = true;
        }

        internal MatchContext(Instruction instruction) {
            this.Reset(instruction);
        }

        /// <summary>
        /// Attempts to get data and reports whether the operation succeeded.
        /// </summary>
        public bool TryGetData(object key, out object value) {
            return this.data.TryGetValue(key, out value);
        }

        /// <summary>
        /// Adds data.
        /// </summary>
        public void AddData(object key, object value) {
            this.data.Add(key, value);
        }

        internal void Reset(Instruction instruction) {
            this.instruction = instruction;
            this.success = true;
        }

        internal void Advance() {
            this.instruction = this.instruction.Next;
        }

    }

}