namespace ME.BECS.Editor {
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using TypeInfo = Jobs.JobsEarlyInitCodeGenerator.TypeInfo;

    // Persist symbolic component identities, never world IDs or registration indices.
    internal static class ILSummaryData {
        [Serializable] internal sealed class Access {
            public string type;
            public int operation;
            public bool argument;
        }
        [Serializable] internal sealed class Accesses { public Access[] values; }
        [Serializable] internal sealed class Types { public string[] values; }
        [Serializable] internal sealed class Number { public uint value; }

        internal static Accesses Encode(IEnumerable<TypeInfo> values) => new Accesses {
            values = values.OrderBy(value => value.type.AssemblyQualifiedName, StringComparer.Ordinal).ThenBy(value => value.op).ThenBy(value => value.isArg)
                .Select(value => new Access { type = value.type.AssemblyQualifiedName, operation = (int)value.op, argument = value.isArg }).ToArray(),
        };

        internal static HashSet<TypeInfo> Decode(Accesses data) {
            if (data?.values == null) throw new FormatException("Missing cached component accesses.");
            return new HashSet<TypeInfo>(data.values.Select(value => {
                if (!Enum.IsDefined(typeof(RefOp), value.operation)) throw new FormatException("Invalid cached component access.");
                return new TypeInfo { type = Resolve(value.type), op = (RefOp)value.operation, isArg = value.argument };
            }));
        }

        internal static Types EncodeTypes(IEnumerable<Type> values) => new Types {
            values = values.Select(type => type.AssemblyQualifiedName).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
        };
        internal static HashSet<Type> DecodeTypes(Types data) => new HashSet<Type>(
            (data?.values ?? throw new FormatException("Missing cached types.")).Select(Resolve));
        internal static Type Resolve(string name) => Type.GetType(name, false) ?? throw new TypeLoadException("Cached type no longer exists: " + name);
    }
}
