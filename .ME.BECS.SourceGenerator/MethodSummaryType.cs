using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;

namespace ME.BECS.SourceGenerator;

// A portable type expression, not a display string. Variables retain the identity of their
// declaring type/method so equally named T parameters from different scopes cannot collide.
internal sealed class MethodSummaryType {
    internal readonly char Kind;
    internal readonly string Identity;
    internal readonly MethodSummaryType[] Arguments;
    internal readonly int Depth;
    private const int MaxEncodedSize = 262144;
    private readonly long encodedSize;
    private string? encoded;

    private MethodSummaryType(char kind, string identity, MethodSummaryType[] arguments) {
        this.Kind = kind;
        this.Identity = identity;
        this.Arguments = arguments;
        this.Depth = arguments.Length == 0 ? 1 : 1 + arguments.Max(static a => a.Depth);
        this.encodedSize = Math.Min(MaxEncodedSize + 1L, 32L + ((Encoding.UTF8.GetByteCount(identity) + 2L) / 3L) * 4L + arguments.Sum(static a => a.encodedSize));
    }

    internal static MethodSummaryType From(ITypeSymbol type) {
        if (type is ITypeParameterSymbol parameter) {
            var owner = parameter.ContainingSymbol;
            var ownerId = MethodSummaryIdentity.Get(owner);
            if (ownerId == null) return new MethodSummaryType('?', "TypeParameterWithoutOwnerId", Array.Empty<MethodSummaryType>());
            return new MethodSummaryType('p', owner.ContainingAssembly.Identity + "\n" + ownerId + "\n" +
                parameter.Ordinal.ToString(CultureInfo.InvariantCulture), Array.Empty<MethodSummaryType>());
        }
        if (type is IArrayTypeSymbol array)
            return new MethodSummaryType('a', array.Rank.ToString(CultureInfo.InvariantCulture), new[] { From(array.ElementType) });
        if (type is IPointerTypeSymbol pointer)
            return new MethodSummaryType('*', "", new[] { From(pointer.PointedAtType) });
        if (type is INamedTypeSymbol named && type.TypeKind != TypeKind.Error) {
            named = named.TupleUnderlyingType ?? named;
            var arguments = new List<MethodSummaryType>();
            foreach (var part in TypeOwners(named)) arguments.AddRange(part.TypeArguments.Select(From));
            return new MethodSummaryType('n', named.ContainingAssembly.Identity + "\n" + named.OriginalDefinition.GetDocumentationCommentId(), arguments.ToArray());
        }
        return new MethodSummaryType('?', type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), Array.Empty<MethodSummaryType>());
    }

    internal static IEnumerable<INamedTypeSymbol> TypeOwners(INamedTypeSymbol type) {
        var stack = new Stack<INamedTypeSymbol>();
        for (var owner = type; owner != null; owner = owner.ContainingType) stack.Push(owner);
        return stack;
    }

    internal static IEnumerable<IMethodSymbol> MethodOwners(IMethodSymbol method) {
        var stack = new Stack<IMethodSymbol>();
        for (var owner = method; owner != null; owner = owner.ContainingSymbol as IMethodSymbol) stack.Push(owner);
        return stack;
    }

    internal static MethodSummaryType[] Environment(IMethodSymbol method) =>
        TypeOwners(method.ContainingType).SelectMany(static t => t.TypeParameters).Select(From)
            .Concat(MethodOwners(method).SelectMany(static m => m.TypeParameters).Select(From)).ToArray();

    internal bool IsOpen => this.Kind == 'p' || this.Arguments.Any(static a => a.IsOpen);
    internal bool IsUnsupported => this.Kind == '?' || this.encodedSize > MaxEncodedSize || this.Arguments.Any(static a => a.IsUnsupported);

    internal MethodSummaryType Substitute(IReadOnlyDictionary<string, MethodSummaryType> environment) {
        // Substitute once: replacement values already belong to the caller's instantiated context.
        // Recursing into a value T -> T would never terminate for open generic roots.
        if (this.Kind == 'p' && environment.TryGetValue(this.Identity, out var replacement)) return replacement;
        if (this.Arguments.Length == 0) return this;
        var arguments = this.Arguments.Select(a => a.Substitute(environment)).ToArray();
        if (arguments.Any(static a => a.Depth >= 64)) return new MethodSummaryType('?', "TypeSubstitutionDepthLimit", Array.Empty<MethodSummaryType>());
        return new MethodSummaryType(this.Kind, this.Identity, arguments);
    }

    internal string Encode() {
        if (this.encoded != null) return this.encoded;
        if (this.encodedSize > MaxEncodedSize) return this.encoded = new MethodSummaryType('?', "TypeExpressionSizeLimit", Array.Empty<MethodSummaryType>()).Encode();
        var identity = Convert.ToBase64String(Encoding.UTF8.GetBytes(this.Identity));
        var builder = new StringBuilder().Append(this.Kind).Append(identity.Length.ToString(CultureInfo.InvariantCulture)).Append(':')
            .Append(identity).Append(this.Arguments.Length.ToString(CultureInfo.InvariantCulture)).Append(':');
        foreach (var argument in this.Arguments) builder.Append(argument.Encode());
        return this.encoded = builder.ToString();
    }

    internal static bool TryDecode(string value, out MethodSummaryType? type) {
        type = null;
        if (value.Length > 1048576) return false;
        try {
            var position = 0;
            type = Read(value, ref position, 0);
            return position == value.Length;
        } catch (FormatException) { return false; }
    }

    private static MethodSummaryType Read(string value, ref int position, int depth) {
        if (depth > 128 || position >= value.Length) throw new FormatException();
        var kind = value[position++];
        if (kind is not ('p' or 'a' or '*' or 'n' or '?')) throw new FormatException();
        var length = ReadNumber(value, ref position);
        if (length > value.Length - position) throw new FormatException();
        var identity = Encoding.UTF8.GetString(Convert.FromBase64String(value.Substring(position, length)));
        position += length;
        var count = ReadNumber(value, ref position);
        if (count > value.Length - position || count > 1024) throw new FormatException();
        if ((kind is 'p' or '?' && count != 0) || (kind is 'a' or '*' && count != 1)) throw new FormatException();
        var arguments = new MethodSummaryType[count];
        for (var i = 0; i < count; ++i) arguments[i] = Read(value, ref position, depth + 1);
        return new MethodSummaryType(kind, identity, arguments);
    }

    private static int ReadNumber(string value, ref int position) {
        var end = value.IndexOf(':', position);
        if (end < 0 || !int.TryParse(value.Substring(position, end - position), NumberStyles.None, CultureInfo.InvariantCulture, out var number))
            throw new FormatException();
        position = end + 1;
        return number;
    }
}
