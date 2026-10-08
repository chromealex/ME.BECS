namespace ME.BECS.Editor {
    using System;
    using System.Linq;
    using System.Reflection;

    // The only Unity-thread part of the persistent IL cache's environment.
    // Copy arrays owned by CompilationPipeline; no UnityEngine.Object or live
    // compilation descriptor is passed to an analyzer worker.
    internal sealed class ILAnalysisEnvironment {
        internal sealed class Script {
            internal readonly string name;
            internal readonly string[] defines, references, sources, libraries;
            internal Script(UnityEditor.Compilation.Assembly assembly) {
                this.name = assembly.name;
                this.defines = (string[])assembly.defines.Clone();
                this.references = assembly.assemblyReferences.Select(reference => reference.name).ToArray();
                this.sources = (string[])assembly.sourceFiles.Clone();
                this.libraries = (string[])assembly.compiledAssemblyReferences.Clone();
            }
        }

        internal readonly string cachePath, target;
        internal readonly Script[] scripts;
        internal readonly Assembly[] loaded;

        private ILAnalysisEnvironment(string cachePath, string target, Script[] scripts, Assembly[] loaded) {
            this.cachePath = cachePath;
            this.target = target;
            this.scripts = scripts;
            this.loaded = loaded;
        }

        internal static ILAnalysisEnvironment Capture(string cachePath = null) => new ILAnalysisEnvironment(
            System.IO.Path.GetFullPath(cachePath ?? System.IO.Path.Combine(UnityEngine.Application.dataPath,
                "../Library/ME.BECS.SourceGenerator/IncrementalIL.v2.json")),
            UnityEditor.EditorUserBuildSettings.activeBuildTarget.ToString(),
            UnityEditor.Compilation.CompilationPipeline.GetAssemblies(UnityEditor.Compilation.AssembliesType.Editor)
                .Where(assembly => !SourceGeneratorCodeIdentity.IsConsumer(assembly.name)).Select(assembly => new Script(assembly)).ToArray(),
            AppDomain.CurrentDomain.GetAssemblies());
    }
}
