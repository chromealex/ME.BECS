namespace ME.BECS.Editor {
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Reflection;
    using System.Reflection.Emit;
    using ME.BECS.Mono.Reflection;

    // Counterexamples for migration reports, NOT a completeness certificate.
    // An empty list does not certify native code, callbacks in libraries, aliasing,
    // exception flow or all possible runtime receiver types.
    internal sealed class ILDispatchDiagnostics {
        private readonly HashSet<string> issues = new HashSet<string>(StringComparer.Ordinal);

        internal void Read(MethodBase body, Instruction[] instructions, bool unknownFormatting, ILDelegateTargets delegates = null) {
            if (unknownFormatting) this.Add("UnresolvedFormatting", body, -1, null);
            delegates ??= ILDelegateTargets.Read(body, instructions);
            for (var index = 0; index < instructions.Length; ++index) {
                var instruction = instructions[index];
                if (instruction.OpCode == OpCodes.Calli) { this.Add("IndirectCall", body, instruction.Offset, null); continue; }
                if (instruction.OpCode != OpCodes.Call && instruction.OpCode != OpCodes.Callvirt && instruction.OpCode != OpCodes.Jmp) continue;
                if (!(instruction.Operand is MethodBase raw)) { this.Add("MissingCallTarget", body, instruction.Offset, null); continue; }
                if (raw.IsDefined(typeof(CodeGeneratorIgnoreAttribute), false) || Jobs.ILJobEntityCounts.IsBurstStorageLeaf(raw) ||
                    SourceGeneratorScheduledJobsValidation.IsUnityAddressIntrinsic(raw)) continue;
                if (!(ILCallTargets.Resolve(instructions, index, out var exactDispatch) is MethodInfo target)) continue;
                if (ILDelegateTargets.IsInvoke(target)) {
                    if (!delegates.At(instruction.Offset).complete) this.Add("UnresolvedDelegateCall", body, instruction.Offset, target);
                }
                else if (instruction.OpCode == OpCodes.Callvirt && !exactDispatch)
                    this.Add("UnresolvedVirtualCall", body, instruction.Offset, target);
            }
        }

        private void Add(string kind, MethodBase body, int offset, MethodBase target) => this.issues.Add(kind + ": " +
            body.DeclaringType?.AssemblyQualifiedName + " | " + body +
            (offset < 0 ? "" : " @ IL_" + offset.ToString("x4", CultureInfo.InvariantCulture)) +
            (target == null ? "" : " -> " + target.DeclaringType + "." + target));

        internal string[] GetIssues() => this.issues.OrderBy(issue => issue, StringComparer.Ordinal).ToArray();
    }
}
