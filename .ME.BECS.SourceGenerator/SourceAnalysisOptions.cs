using System;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace ME.BECS.SourceGenerator;

// Behavioral source analysis is an opt-in comparison tool. Production emission
// uses Editor-exported IL snapshots and must not pay for walking method bodies.
internal static class SourceAnalysisOptions {
    internal const string Define = "BECS_SOURCE_ANALYSIS_DIAGNOSTICS";

    internal static bool Enabled(ParseOptions options) => options.PreprocessorSymbolNames.Contains(Define, StringComparer.Ordinal);

    internal static bool Enabled(Compilation compilation) => compilation.SyntaxTrees.Any(tree => Enabled(tree.Options));
}
