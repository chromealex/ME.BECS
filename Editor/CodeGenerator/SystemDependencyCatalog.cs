namespace ME.BECS.Editor {
    using System;
    using System.Linq;
    using System.Reflection;
    using Components = System.Collections.Generic.List<ComponentDependencyGraphInfo>;
    using Dependencies = System.Collections.Generic.HashSet<System.Type>;
    using Errors = System.Collections.Generic.List<Systems.SystemDependenciesCodeGenerator.MethodInfoDependencies.Error>;

    // Graph UI consumes advisory compiler tables through a framework API. No
    // assembly names, generated implementation types or reflection per node draw.
    /// <summary>
    /// Registers and resolves system dependency entries.
    /// </summary>
    public static class SystemDependencyCatalog {
        private static bool searched;
        private static Type publisher;
        private static Func<Type, Components> components;
        private static Func<Type, Dependencies> dependencies;
        private static Func<Type, Errors> errors;
        private static Func<Type, bool> contains;

        static SystemDependencyCatalog() {
            AppDomain.CurrentDomain.AssemblyLoad += (_, args) => {
                if (!SourceGeneratorPublicationBridges.IsBridge(args.LoadedAssembly.GetName().Name)) return;
                searched = false;
                publisher = null;
            };
        }

        /// <summary>
        /// Indicates is available.
        /// </summary>
        public static bool IsAvailable {
            get { Find(); return publisher != null; }
        }

        private static void Find() {
            if (searched) return;
            searched = true;
            var candidates = AppDomain.CurrentDomain.GetAssemblies().Where(assembly => !assembly.IsDynamic &&
                assembly.GetCustomAttributes(typeof(AssemblyMetadataAttribute), false).Cast<AssemblyMetadataAttribute>().Any(attribute =>
                    attribute.Key == CodeGeneration.SourceGeneratorSystemDependencyFragmentFormat.MetadataKey &&
                    attribute.Value.StartsWith("editor\t" + assembly.GetName().Name + "\t", StringComparison.Ordinal)))
                .Select(assembly => assembly.GetType("ME.BECS.SourceGenerated.EditorSystemDependencies", false)).Where(type => type != null).ToArray();
            if (candidates.Length != 1) return;
            var type = candidates[0];
            components = (Func<Type, Components>)Delegate.CreateDelegate(typeof(Func<Type, Components>), type.GetMethod("GetSystemComponentsDependencies"));
            dependencies = (Func<Type, Dependencies>)Delegate.CreateDelegate(typeof(Func<Type, Dependencies>), type.GetMethod("GetSystemDependencies"));
            errors = (Func<Type, Errors>)Delegate.CreateDelegate(typeof(Func<Type, Errors>), type.GetMethod("GetSystemDependenciesErrors"));
            contains = (Func<Type, bool>)Delegate.CreateDelegate(typeof(Func<Type, bool>), type.GetMethod("ContainsSystem"));
            publisher = type;
        }

        // Explicit queries report unavailable diagnostics rather than pretending
        // to prove an empty dependency set. UI callers may check IsAvailable.
        /// <summary>
        /// Returns publisher type.
        /// </summary>
        public static Type GetPublisherType() {
            Find();
            return publisher ?? throw new InvalidOperationException("System dependency hints are unavailable. Wait for source input export/compilation; runtime scheduling is unaffected.");
        }
        /// <summary>
        /// Returns system components dependencies.
        /// </summary>
        public static Components GetSystemComponentsDependencies(Type system) { GetPublisherType(); return components(system); }
        /// <summary>
        /// Tests whether the dependency catalog contains the specified system type.
        /// </summary>
        public static bool ContainsSystem(Type system) => system != null && IsAvailable && contains(system);
        /// <summary>
        /// Returns system dependencies.
        /// </summary>
        public static Dependencies GetSystemDependencies(Type system) { GetPublisherType(); return dependencies(system); }
        /// <summary>
        /// Returns system dependencies errors.
        /// </summary>
        public static Errors GetSystemDependenciesErrors(Type system) { GetPublisherType(); return errors(system); }
    }
}
