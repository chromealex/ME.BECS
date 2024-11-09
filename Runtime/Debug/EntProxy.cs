namespace ME.BECS {
    
    public unsafe class EntProxy {

        private Ent ent;
        
        public EntProxy(Ent ent) {

            this.ent = ent;

        }

        public bool alive => this.ent.IsAlive();
        public override string ToString() => this.ent.ToString();
        
        public object[] components {
            get {

                if (this.alive == false) return null;
                
                var world = this.ent.World;
                var methodHas = typeof(Components).GetMethod(nameof(Components.HasDirect));
                var methodRead = typeof(Components).GetMethod(nameof(Components.ReadDirect));

                var result = new System.Collections.Generic.List<object>();
                foreach (var item in StaticTypesLoadedManaged.loadedTypes) {

                    var type = item.Value;
                    var gHasMethod = methodHas.MakeGenericMethod(type);
                    var has = (bool)gHasMethod.Invoke(null, new object[] { this.ent });
                    if (has == true) {
                        var gReadMethod = methodRead.MakeGenericMethod(type);
                        var data = gReadMethod.Invoke(null, new object[] { this.ent });
                        result.Add(data);
                    }

                }
                
                return result.ToArray();
            }
        }

        public object[] sharedComponents {
            get {
                
                if (this.alive == false) return null;

                var world = this.ent.World;
                var result = new System.Collections.Generic.List<object>();
                var methodHas = typeof(Components).GetMethod(nameof(Components.HasSharedDirect));
                var methodRead = typeof(Components).GetMethod(nameof(Components.ReadSharedDirect));
                foreach (var item in StaticTypesLoadedManaged.loadedSharedTypes) {
                    
                    var type = item.Value;
                    var gHasMethod = methodHas.MakeGenericMethod(type);
                    var has = (bool)gHasMethod.Invoke(null, new object[] { this.ent });
                    if (has == true) {
                        var gReadMethod = methodRead.MakeGenericMethod(type);
                        var data = gReadMethod.Invoke(null, new object[] { this.ent });
                        result.Add(data);
                    }
                    
                }

                return result.ToArray();
            }
        }

    }

}