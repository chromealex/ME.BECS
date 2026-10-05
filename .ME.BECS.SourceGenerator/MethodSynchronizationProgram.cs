using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis.FlowAnalysis;

namespace ME.BECS.SourceGenerator;

// Validate serialized input before interpreting it. Gapped programs remain diagnostic
// input; successful parsing alone never certifies synchronization.
internal sealed class MethodSynchronizationProgram {
    internal sealed class Slot {
        internal string Name = "", Role = "", Passing = "value", Kind = "";
        internal MethodSummaryType Type = null!;
    }
    internal sealed class Block {
        internal int Id;
        internal string Kind = "";
        internal readonly List<(int? Target, string Semantics, string Unwind)> Edges = new();
        internal int[] Handlers = Array.Empty<int>();
        internal readonly List<(int Target, string Unwind)> Catches = new();
        internal readonly List<string[]> Events = new();
    }
    internal readonly Dictionary<string, Slot> Slots = new(StringComparer.Ordinal);
    internal readonly Dictionary<string, string[]> Symbols = new(StringComparer.Ordinal);
    internal readonly Dictionary<int, Block> Blocks = new();
    internal readonly Dictionary<int, int> FinallyRegions = new();
    internal readonly Dictionary<int, int> CatchRegions = new();
    internal readonly List<string> Gaps = new();
    internal Slot Return = null!;
    internal int Entry;
    internal bool SupportsExceptions;
    internal bool SupportsCatches;

    internal static int[] RegionIds(string value) => value.Length == 0 ? Array.Empty<int>() :
        value.Split(',').Select(part => int.Parse(part, CultureInfo.InvariantCulture)).ToArray();
    private static bool RegionList(string value) => value.Length != 0 && value.Split(',').All(part => Number(part, out _)) &&
        value.Split(',').Distinct(StringComparer.Ordinal).Count() == value.Split(',').Length;

    private static bool Number(string value, out int number) => int.TryParse(value, NumberStyles.None,
        CultureInfo.InvariantCulture, out number) && number.ToString(CultureInfo.InvariantCulture) == value;
    private static bool Passing(string value) => value is "value" or "ref" or "in" or "out";
    private static bool Role(string value) => value is "H" or "C" or "Q" or "T" or "U";
    private static bool EnumName<T>(string value) where T : struct => Enum.TryParse<T>(value, out var parsed) && parsed.ToString() == value;

    internal static bool TryParse(string[] flags, out MethodSynchronizationProgram? program, out string reason) {
        program = null;
        reason = "MissingSynchronizationFlow";
        var payloads = flags.Where(flag => flag.StartsWith(MethodSynchronizationFlow.FlagPrefix, StringComparison.Ordinal)).ToArray();
        if (flags.Count(flag => flag == MethodSynchronizationFlow.Schema) != 1 || payloads.Length != 1) return false;
        reason = "MalformedSynchronizationFlow";
        if (payloads[0].Length > 16777216) { reason = "SynchronizationFlowSizeLimit"; return false; }
        string text;
        try { text = new UTF8Encoding(false, true).GetString(Convert.FromBase64String(payloads[0].Substring(MethodSynchronizationFlow.FlagPrefix.Length))); }
        catch (FormatException) { return false; }
        catch (DecoderFallbackException) { return false; }
        var rows = text.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries).Select(row => row.Split('\t')).ToArray();
        if (rows.Length == 0 || rows[0].Length != 1 || rows[0][0] != "v3") return false;
        var result = new MethodSynchronizationProgram();
        var exceptionFlags = flags.Where(flag => flag.StartsWith("sync-exception-schema=", StringComparison.Ordinal)).ToArray();
        if (exceptionFlags.Length > 1 || exceptionFlags.Length == 1 && exceptionFlags[0] != MethodSynchronizationFlow.ExceptionSchema) {
            reason = "UnsupportedSynchronizationExceptions"; return false;
        }
        result.SupportsExceptions = exceptionFlags.Length == 1;
        var catchFlags = flags.Where(flag => flag.StartsWith("sync-catch-schema=", StringComparison.Ordinal)).ToArray();
        if (catchFlags.Length > 1 || catchFlags.Length == 1 && catchFlags[0] != MethodSynchronizationFlow.CatchSchema) {
            reason = "UnsupportedSynchronizationCatches"; return false;
        }
        result.SupportsCatches = catchFlags.Length == 1;
        Block? block = null;
        foreach (var row in rows.Skip(1)) {
            switch (row[0]) {
                case "M":
                    if (block != null || result.Return != null || row.Length != 4 || !Role(row[1]) || row[2] is not ("value" or "ref" or "in") ||
                        !MethodSummaryType.TryDecode(row[3], out var returns)) return false;
                    result.Return = new Slot { Role = row[1], Passing = row[2], Type = returns! };
                    break;
                case "I": case "P": case "V":
                    if (block != null || row.Length != (row[0] == "V" ? 4 : 5) || !Role(row[2]) ||
                        (row[0] != "V" && !Passing(row[3])) || result.Slots.ContainsKey(row[1]) ||
                        !MethodSummaryType.TryDecode(row[row.Length - 1], out var type)) return false;
                    if (row[0] == "I" ? row[1] != "this" : row[1].Length < 2 ||
                        row[1][0] != (row[0] == "P" ? 'p' : 'v') || !Number(row[1].Substring(1), out _)) return false;
                    result.Slots.Add(row[1], new Slot { Kind = row[0], Name = row[1], Role = row[2],
                        Passing = row[0] == "V" ? "value" : row[3], Type = type! });
                    break;
                case "S":
                    if (block != null || row.Length != 3 || row[1].Length < 2 || row[1][0] != 's' ||
                        !Number(row[1].Substring(1), out _) || result.Symbols.ContainsKey(row[1])) return false;
                    string[] contract;
                    try { contract = new UTF8Encoding(false, true).GetString(Convert.FromBase64String(row[2])).Split('\t'); }
                    catch (FormatException) { return false; }
                    catch (DecoderFallbackException) { return false; }
                    if (contract.Length < 5 || contract[0] is not ("call" or "new" or "operator" or "conversion" or "field") ||
                        contract[1] != "0" || contract.Any(token => token.Contains('\n')) ||
                        !contract[3].StartsWith(contract[0] == "field" ? "F:" : "M:", StringComparison.Ordinal) ||
                        !MethodSummaryType.TryDecode(contract[4], out _)) return false;
                    result.Symbols.Add(row[1], contract);
                    break;
                case "G":
                    if (block != null || row.Length != 2 || row[1].Length == 0 || result.Gaps.Contains(row[1])) return false;
                    result.Gaps.Add(row[1]);
                    break;
                case "Y": case "K":
                    if (!result.SupportsExceptions || block != null || row.Length != 3 || !Number(row[1], out var first) ||
                        !Number(row[2], out var last) || last < first || result.FinallyRegions.ContainsKey(first) || result.CatchRegions.ContainsKey(first) ||
                        row[0] == "K" && !result.SupportsCatches) return false;
                    (row[0] == "Y" ? result.FinallyRegions : result.CatchRegions).Add(first, last);
                    break;
                case "B":
                    if (row.Length != 6 || !Number(row[1], out var id) || result.Blocks.ContainsKey(id) ||
                        !EnumName<BasicBlockKind>(row[2]) || !EnumName<ControlFlowConditionKind>(row[3])) return false;
                    block = new Block { Id = id, Kind = row[2] };
                    foreach (var encoded in row.Skip(4)) {
                        if (encoded == "-") continue;
                        var edge = encoded.Split(':');
                        if (edge.Length is not (2 or 3) || !EnumName<ControlFlowBranchSemantics>(edge[1]) ||
                            edge.Length == 3 && (!result.SupportsExceptions || !RegionList(edge[2]))) return false;
                        int? target = null;
                        if (edge[0] != "-") {
                            if (!Number(edge[0], out var destination)) return false;
                            target = destination;
                        }
                        block.Edges.Add((target, edge[1], edge.Length == 3 ? edge[2] : ""));
                    }
                    result.Blocks.Add(id, block);
                    break;
                default:
                    if (block == null) return false;
                    bool Operand(string value) => value == "-" || result.Slots.ContainsKey(value);
                    if (row[0] == "H") {
                        if (!result.SupportsExceptions || row.Length != 2 || block.Events.Count != 0 || block.Handlers.Length != 0 || !RegionList(row[1])) return false;
                        block.Handlers = RegionIds(row[1]);
                        break;
                    } else if (row[0] == "J") {
                        if (!result.SupportsCatches || !result.SupportsExceptions || row.Length != 3 || block.Events.Count != 0 ||
                            !Number(row[1], out var handler) || row[2] != "-" && !RegionList(row[2])) return false;
                        var catchEdge = (handler, row[2] == "-" ? "" : row[2]);
                        if (block.Catches.Contains(catchEdge)) return false;
                        block.Catches.Add(catchEdge);
                        break;
                    } else if (row[0] == "X") {
                        if (!result.SupportsExceptions || row.Length != 1) return false;
                    } else if (row[0] == "=") {
                        if (row.Length != 3 || row[1] == "-" || !Operand(row[1]) || !Operand(row[2])) return false;
                    } else if (row[0] is "Z" or "?") {
                        if (row.Length != 2 || row[1] == "-" || !Operand(row[1]) || (row[0] == "?" && result.Gaps.Count == 0)) return false;
                    } else if (row[0] == "R") {
                        if (row.Length != 2 || !Operand(row[1])) return false;
                    } else if (row[0] == "F") {
                        if (row.Length != 2 || !result.Symbols.TryGetValue(row[1], out var field) || field[0] != "field") return false;
                    } else if (row[0] == "T") {
                        if (row.Length != 3 || !Operand(row[2]) || !result.Symbols.TryGetValue(row[1], out var formatter) ||
                            formatter[0] != "call" || formatter[3] != "M:__ImplicitFormatting" ||
                            MethodSummaryContracts.Value(formatter, "implicit-formatting") == null) return false;
                    } else if (row[0] is "A" or "O") {
                        if (row.Length != 2 || !MethodSummaryType.TryDecode(row[1], out _)) return false;
                    } else if (row[0] == "C") {
                        if (row.Length < 5 || !Operand(row[1]) || !Operand(row[4]) || !Passing(row[3]) ||
                            !result.Symbols.TryGetValue(row[2], out var call) || call[0] == "field") return false;
                        var ordinals = new HashSet<int>();
                        foreach (var encoded in row.Skip(5)) {
                            var argument = encoded.Split(':');
                            if (argument.Length != 3 || !Number(argument[0], out var ordinal) || !ordinals.Add(ordinal) ||
                                !Passing(argument[1]) || !Operand(argument[2])) return false;
                        }
                        if (!Enumerable.Range(0, ordinals.Count).All(ordinals.Contains)) return false;
                    } else return false;
                    block.Events.Add(row);
                    break;
            }
        }
        if (result.Blocks.Count == 0 && result.Gaps.Count > 0 && result.Return == null && result.Slots.Count == 0 && result.Symbols.Count == 0) {
            program = result; reason = ""; return true; // Explicit unavailable-body record.
        }
        if (result.Return == null || result.Blocks.Values.Count(value => value.Kind == "Entry") != 1 ||
            result.Blocks.Values.Count(value => value.Kind == "Exit") > 1 || result.Blocks.Values.Any(value =>
                value.Edges.Any(edge => edge.Target.HasValue && !result.Blocks.ContainsKey(edge.Target.Value)))) return false;
        foreach (var region in result.FinallyRegions.Concat(result.CatchRegions)) {
            if (!result.Blocks.TryGetValue(region.Key, out var start) || !result.Blocks.TryGetValue(region.Value, out var end) ||
                start.Kind != "Block" || end.Kind != "Block") return false;
        }
        foreach (var value in result.Blocks.Values) {
            foreach (var region in value.Handlers.Concat(value.Edges.SelectMany(edge => RegionIds(edge.Unwind)))
                         .Concat(value.Catches.SelectMany(edge => RegionIds(edge.Unwind))))
                if (!result.FinallyRegions.TryGetValue(region, out var end) || value.Id >= region && value.Id <= end) return false;
            foreach (var edge in value.Catches) {
                if (!result.CatchRegions.TryGetValue(edge.Target, out var end) || value.Id >= edge.Target && value.Id <= end) return false;
                foreach (var region in RegionIds(edge.Unwind))
                    if (edge.Target >= region && edge.Target <= result.FinallyRegions[region]) return false;
            }
        }
        // The interpreter retains a return snapshot in a real cell while finally
        // executes, so helper normalization cannot rename its tokens independently.
        foreach (var value in result.Blocks.Values) {
            var returns = value.Events.Where(operation => operation[0] == "R").ToArray();
            if (returns.Length == 0) continue;
            if (returns.Length != 1 || value.Events[value.Events.Count - 1][0] != "R" || value.Edges.Count != 1 ||
                value.Edges[0].Semantics != "Return" || !value.Edges[0].Target.HasValue ||
                result.Blocks[value.Edges[0].Target!.Value].Kind != "Exit") return false;
        }
        var parameterCount = result.Slots.Values.Count(value => value.Kind == "P");
        if (!Enumerable.Range(0, parameterCount).All(index => result.Slots.ContainsKey("p" + index.ToString(CultureInfo.InvariantCulture)))) return false;
        result.Entry = result.Blocks.Values.Single(value => value.Kind == "Entry").Id;
        program = result;
        reason = "";
        return true;
    }
}
