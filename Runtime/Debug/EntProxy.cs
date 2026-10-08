namespace ME.BECS {
    
    /// <summary>
    /// Exposes ent data for debugger inspection.
    /// </summary>
    public class EntProxy {

        private static readonly System.Reflection.MethodInfo isTagDirect = typeof(Components).GetMethod(nameof(Components.IsTagDirect), System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
        private static readonly System.Reflection.MethodInfo isEnabledDirect = typeof(Components).GetMethod(nameof(Components.IsEnabledDirect), System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
        private static readonly System.Reflection.MethodInfo hasDirect = typeof(Components).GetMethod(nameof(Components.HasDirect), System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
        private static readonly System.Reflection.MethodInfo readDirect = typeof(Components).GetMethod(nameof(Components.ReadDirect), System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);

        private static readonly System.Reflection.MethodInfo hasSharedDirect = typeof(Components).GetMethod(nameof(Components.HasSharedDirect), System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
        private static readonly System.Reflection.MethodInfo readSharedDirect = typeof(Components).GetMethod(nameof(Components.ReadSharedDirect), System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);

        private static readonly System.Reflection.MethodInfo hasStaticDirect = typeof(Components).GetMethod(nameof(Components.HasStaticDirect), System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
        private static readonly System.Reflection.MethodInfo readStaticDirect = typeof(Components).GetMethod(nameof(Components.ReadStaticDirect), System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);

        /// <summary>
        /// Defines component data used by entity processing.
        /// </summary>
        [System.Diagnostics.DebuggerDisplayAttribute("{GetString()}")]
        public struct Component {

            /// <summary>
            /// Data consumed or produced by the containing operation.
            /// </summary>
            [System.Diagnostics.DebuggerBrowsableAttribute(System.Diagnostics.DebuggerBrowsableState.RootHidden)]
            public object data;
            /// <summary>
            /// Entity whose components or lifetime are associated with this value.
            /// </summary>
            [System.Diagnostics.DebuggerBrowsableAttribute(System.Diagnostics.DebuggerBrowsableState.Never)]
            public Ent ent;
            /// <summary>
            /// Indicates is tag.
            /// </summary>
            [System.Diagnostics.DebuggerBrowsableAttribute(System.Diagnostics.DebuggerBrowsableState.Never)]
            public bool isTag;
            /// <summary>
            /// Whether may enable behavior or state is selected.
            /// </summary>
            [System.Diagnostics.DebuggerBrowsableAttribute(System.Diagnostics.DebuggerBrowsableState.Never)]
            public bool mayEnable;

            /// <summary>
            /// Initializes <c>Component</c> from the supplied data, ent, may enable, is tag.
            /// </summary>
            public Component(object data, Ent ent, bool mayEnable, bool isTag) {
                this.data = data;
                this.ent = ent;
                this.mayEnable = mayEnable;
                this.isTag = isTag;
            }

            /// <summary>
            /// Returns string.
            /// </summary>
            public string GetString() {
                var tag = string.Empty;
                if (this.isTag == true) {
                    tag = "[ TAG ] ";
                }

                if (this.mayEnable == true) {
                    var gIsEnabledDirect = isEnabledDirect.MakeGenericMethod(this.data.GetType());
                    var enabled = (bool)gIsEnabledDirect.Invoke(null, new object[] { this.ent });
                    if (enabled == false) {
                        return $"{tag}[ DISABLED ] {this.data.GetType().Name}";
                    }
                }

                return $"{tag}{this.data.GetType().Name}";
            }

        }

        private readonly Ent ent;
        
        /// <summary>
        /// Initializes <c>EntProxy</c> from the supplied ent.
        /// </summary>
        public EntProxy(Ent ent) {
            this.ent = ent;
        }

        /// <summary>
        /// Whether alive behavior or state is selected.
        /// </summary>
        public bool alive => this.ent.IsAlive();
        /// <summary>
        /// Whether active behavior or state is selected.
        /// </summary>
        public bool active => this.ent.IsAlive() && this.ent.IsActive();
        /// <summary>
        /// Formats this value for display or diagnostics.
        /// </summary>
        public override string ToString() => this.ent.ToString();
        
        /// <summary>
        /// Component storage or descriptors used by this operation.
        /// </summary>
        public Component[] components {
            get {

                if (this.alive == false) return null;
                
                var result = new System.Collections.Generic.List<Component>();
                foreach (var item in StaticTypesLoadedManaged.loadedTypes) {

                    var type = item.Value;
                    if (typeof(IComponent).IsAssignableFrom(type) == false) continue;
                    if (typeof(IComponentShared).IsAssignableFrom(type) == true) continue;

                    var gHasMethod = hasDirect.MakeGenericMethod(type);
                    var has = (bool)gHasMethod.Invoke(null, new object[] { this.ent });
                    if (has == true) {
                        var gReadMethod = readDirect.MakeGenericMethod(type);
                        var data = gReadMethod.Invoke(null, new object[] { this.ent });
                        result.Add(new Component(data, this.ent, true, IsTag(type)));
                    }

                }
                
                return result.ToArray();
            }
        }

        /// <summary>
        /// Shared components used by <c>EntProxy</c>.
        /// </summary>
        public Component[] sharedComponents {
            get {
                
                if (this.alive == false) return null;

                var result = new System.Collections.Generic.List<Component>();
                foreach (var item in StaticTypesLoadedManaged.loadedSharedTypes) {
                    
                    var type = item.Value;
                    if (typeof(IComponentShared).IsAssignableFrom(type) == false) continue;

                    var gHasMethod = hasSharedDirect.MakeGenericMethod(type);
                    var has = (bool)gHasMethod.Invoke(null, new object[] { this.ent });
                    if (has == true) {
                        var gReadMethod = readSharedDirect.MakeGenericMethod(type);
                        var data = gReadMethod.Invoke(null, new object[] { this.ent });
                        result.Add(new Component(data, this.ent, false, IsTag(type)));
                    }
                    
                }

                return result.ToArray();
            }
        }

        /// <summary>
        /// Static components used by <c>EntProxy</c>.
        /// </summary>
        public Component[] staticComponents {
            get {
                
                if (this.alive == false) return null;
                if (this.ent.Has<EntityConfigComponent>() == false) return null;

                var result = new System.Collections.Generic.List<Component>();
                foreach (var item in StaticTypesLoadedManaged.loadedStaticTypes) {
                    
                    var type = item.Value;
                    if (typeof(IConfigComponentStatic).IsAssignableFrom(type) == false) continue;

                    var gHasMethod = hasStaticDirect.MakeGenericMethod(type);
                    var has = (bool)gHasMethod.Invoke(null, new object[] { this.ent });
                    if (has == true) {
                        var gReadMethod = readStaticDirect.MakeGenericMethod(type);
                        var data = gReadMethod.Invoke(null, new object[] { this.ent });
                        result.Add(new Component(data, this.ent, false, IsTag(type)));
                    }
                    
                }

                return result.ToArray();
            }
        }

        private static bool IsTag(System.Type type) {
            var gMethod = isTagDirect.MakeGenericMethod(type);
            return (bool)gMethod.Invoke(null, null);
        }

    }

}