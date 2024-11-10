namespace ME.BECS {
    
    public class EntProxy {

        private static readonly System.Reflection.MethodInfo isTagDirect = typeof(Components).GetMethod(nameof(Components.IsTagDirect), System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
        private static readonly System.Reflection.MethodInfo isEnabledDirect = typeof(Components).GetMethod(nameof(Components.IsEnabledDirect), System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
        private static readonly System.Reflection.MethodInfo hasDirect = typeof(Components).GetMethod(nameof(Components.HasDirect), System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
        private static readonly System.Reflection.MethodInfo readDirect = typeof(Components).GetMethod(nameof(Components.ReadDirect), System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);

        private static readonly System.Reflection.MethodInfo hasSharedDirect = typeof(Components).GetMethod(nameof(Components.HasSharedDirect), System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
        private static readonly System.Reflection.MethodInfo readSharedDirect = typeof(Components).GetMethod(nameof(Components.ReadSharedDirect), System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);

        private static readonly System.Reflection.MethodInfo hasStaticDirect = typeof(Components).GetMethod(nameof(Components.HasStaticDirect), System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
        private static readonly System.Reflection.MethodInfo readStaticDirect = typeof(Components).GetMethod(nameof(Components.ReadStaticDirect), System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);

        [System.Diagnostics.DebuggerDisplayAttribute("{GetString()}")]
        public struct Component {

            [System.Diagnostics.DebuggerBrowsableAttribute(System.Diagnostics.DebuggerBrowsableState.RootHidden)]
            public object data;
            [System.Diagnostics.DebuggerBrowsableAttribute(System.Diagnostics.DebuggerBrowsableState.Never)]
            public Ent ent;
            [System.Diagnostics.DebuggerBrowsableAttribute(System.Diagnostics.DebuggerBrowsableState.Never)]
            public bool isTag;
            [System.Diagnostics.DebuggerBrowsableAttribute(System.Diagnostics.DebuggerBrowsableState.Never)]
            public bool mayEnable;

            public Component(object data, Ent ent, bool mayEnable, bool isTag) {
                this.data = data;
                this.ent = ent;
                this.mayEnable = mayEnable;
                this.isTag = isTag;
            }

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
        
        public EntProxy(Ent ent) {
            this.ent = ent;
        }

        public bool alive => this.ent.IsAlive();
        public bool active => this.ent.IsAlive() && this.ent.IsActive();
        public override string ToString() => this.ent.ToString();
        
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