namespace ME.BECS.Editor {
    using System;
    using System.Reflection;
    using System.Text;

    // ECMA-335 II.23.2 standalone method signatures. Unsupported encodings keep
    // the caller's MVID fallback; never hash unresolved metadata tokens as types.
    internal sealed class ILInlineSignature {
        private readonly byte[] data;
        private readonly Func<int, Type> resolve;
        private readonly Type[] typeArguments, methodArguments;
        private readonly StringBuilder text = new StringBuilder("inline-signature-v1;");
        private int position;

        private ILInlineSignature(byte[] data, Func<int, Type> resolve, Type[] typeArguments, Type[] methodArguments) {
            this.data = data;
            this.resolve = resolve;
            this.typeArguments = typeArguments;
            this.methodArguments = methodArguments;
        }

        internal static bool TryNormalize(MethodBase method, byte[] data, out string value) {
            var owner = method.DeclaringType?.GetGenericArguments() ?? System.Type.EmptyTypes;
            var arguments = method is MethodInfo info ? info.GetGenericArguments() : System.Type.EmptyTypes;
            // Tokens are TypeDef/TypeRef definitions, never TypeSpec. Passing
            // enclosing arguments here makes Mono incorrectly instantiate an
            // unrelated generic definition; VAR/MVAR are bound by the reader.
            return TryRead(data, token => method.Module.ResolveType(token), owner, arguments, out value);
        }

        internal static void CaptureTypes(MethodBase method, byte[] data) {
            var owner = method.DeclaringType?.GetGenericArguments() ?? System.Type.EmptyTypes;
            var arguments = method is MethodInfo info ? info.GetGenericArguments() : System.Type.EmptyTypes;
            // Enclosing VAR/MVAR arguments are already observed with the method.
            // Explicit signature tokens also contribute declaration dependencies.
            TryRead(data, token => {
                var type = method.Module.ResolveType(token);
                ILDependencyCapture.ObserveType(type);
                return type;
            }, owner, arguments, out _);
        }

        internal static bool TryRead(byte[] data, Func<int, Type> resolve, Type[] typeArguments, Type[] methodArguments, out string value) {
            value = null;
            if (data == null || resolve == null || typeArguments == null || methodArguments == null) { return false; }
            try {
                var reader = new ILInlineSignature(data, resolve, typeArguments, methodArguments);
                reader.Method(0);
                if (reader.position != data.Length) { return false; }
                value = reader.text.ToString();
                return true;
            } catch (Exception exception) when (exception is OperationCanceledException == false) {
                return false;
            }
        }

        private int Byte() {
            if (this.position >= this.data.Length) { throw new FormatException("Truncated signature."); }
            return this.data[this.position++];
        }

        private int Unsigned() {
            var first = this.Byte();
            if ((first & 0x80) == 0) { return first; }
            if ((first & 0xc0) == 0x80) { return ((first & 0x3f) << 8) | this.Byte(); }
            if ((first & 0xe0) == 0xc0) {
                return ((first & 0x1f) << 24) | (this.Byte() << 16) | (this.Byte() << 8) | this.Byte();
            }
            throw new FormatException("Invalid compressed integer.");
        }

        private int Count() {
            var count = this.Unsigned();
            if (count > this.data.Length - this.position) { throw new FormatException("Invalid signature count."); }
            this.text.Append(count).Append(';');
            return count;
        }

        private void Named(Type type) {
            if (type == null) { throw new FormatException("Unresolved signature type."); }
            var name = ILContentFingerprint.TypeName(type);
            this.text.Append(name.Length).Append(':').Append(name).Append(';');
        }

        private void Token() {
            var coded = this.Unsigned();
            var row = coded >> 2;
            // CLASS/VALUETYPE cannot name TypeSpec. Modifiers may, but resolving
            // those as System.Type could erase nested modifiers: keep fallback.
            if (row == 0 || row > 0xffffff || (coded & 3) >= 2) { throw new FormatException("Unsupported type token."); }
            var table = (coded & 3) == 0 ? 0x02000000 : 0x01000000;
            this.Named(this.resolve(table | row));
        }

        private void Method(int depth) {
            if (depth > 64) { throw new FormatException("Signature nesting limit."); }
            var header = this.Byte();
            var convention = header & 0x0f;
            // Generic function-pointer declarations are not handled here. VAR /
            // MVAR references to the enclosing method are supported below.
            if ((header & 0x90) != 0 || (convention > 5 && convention != 9 && convention != 11)) {
                throw new NotSupportedException("Unsupported calling convention.");
            }
            this.text.Append('M').Append(header).Append(';');
            var count = this.Count();
            this.Type(depth + 1);
            var sentinel = false;
            for (var index = 0; index < count; ++index) {
                if (this.position < this.data.Length && this.data[this.position] == 0x41) {
                    if (sentinel == true || (convention != 5 && convention != 11)) { throw new FormatException("Invalid vararg sentinel."); }
                    sentinel = true;
                    ++this.position;
                    this.text.Append("sentinel;");
                }
                this.Type(depth + 1);
            }
        }

        private void Type(int depth) {
            if (depth > 64) { throw new FormatException("Signature nesting limit."); }
            var code = this.Byte();
            this.text.Append('T').Append(code).Append(';');
            if ((code >= 0x01 && code <= 0x0e) || code == 0x16 || code == 0x18 || code == 0x19 || code == 0x1c) { return; }
            switch (code) {
                case 0x0f: case 0x10: case 0x1d:
                    this.Type(depth + 1);
                    return;
                case 0x11: case 0x12:
                    this.Token();
                    return;
                case 0x13: case 0x1e:
                    var arguments = code == 0x13 ? this.typeArguments : this.methodArguments;
                    var index = this.Unsigned();
                    if (index >= arguments.Length) { throw new FormatException("Unbound generic parameter."); }
                    this.Named(arguments[index]);
                    return;
                case 0x14:
                    this.Type(depth + 1);
                    var rank = this.Unsigned();
                    this.text.Append(rank).Append(';');
                    var sizes = this.Count();
                    if (sizes > rank) { throw new FormatException("Invalid array shape."); }
                    for (var i = 0; i < sizes; ++i) { this.text.Append(this.Unsigned()).Append(';'); }
                    var bounds = this.Count();
                    if (bounds > rank) { throw new FormatException("Invalid array bounds."); }
                    for (var i = 0; i < bounds; ++i) {
                        var start = this.position;
                        var encoded = this.Unsigned();
                        var bits = this.position - start == 1 ? 7 : this.position - start == 2 ? 14 : 29;
                        var signed = (encoded >> 1) | ((encoded & 1) == 0 ? 0 : -(1 << (bits - 1)));
                        this.text.Append(signed).Append(';');
                    }
                    return;
                case 0x15:
                    var kind = this.Byte();
                    if (kind != 0x11 && kind != 0x12) { throw new FormatException("Invalid generic instance."); }
                    this.text.Append(kind).Append(';');
                    this.Token();
                    var count = this.Count();
                    if (count == 0) { throw new FormatException("Empty generic instance."); }
                    for (var i = 0; i < count; ++i) { this.Type(depth + 1); }
                    return;
                case 0x1b:
                    this.Method(depth + 1);
                    return;
                case 0x1f: case 0x20:
                    this.Token();
                    this.Type(depth + 1);
                    return;
                default:
                    throw new NotSupportedException("Unsupported signature element.");
            }
        }
    }
}
