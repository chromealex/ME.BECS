namespace ME.BECS.Units.Editor {

    public class LayerAliasUtils {
        
        private static System.Collections.Generic.Dictionary<uint, string> layerAliasMap;
        private static System.Collections.Generic.Dictionary<string, uint> aliasLayerMap;
        private static System.Collections.Generic.List<string> aliases;
        private static ILayerAliasProvider customLayerAliasProvider;
        private static System.Text.StringBuilder sb;

        public static string LayerMaskToString(LayerMask mask) {
            sb ??= new System.Text.StringBuilder();
            sb.Clear();
            for (uint i = 0u; i < 32u; ++i) {
                uint layer = 1u << (int)i;
                if ((mask.mask & layer) != 0) {
                    if (sb.Length > 0) sb.Append(", ");
                    sb.Append(LayerAliasUtils.GetAliasOf(layer));
                }
            }

            return sb.ToString();
        }

        public static LayerMask StringToLayerMask(string value) {
            var layers = value.Split(',', System.StringSplitOptions.RemoveEmptyEntries);
            var attackLayerMask = new ME.BECS.Units.LayerMask();
            
            foreach (var alias in layers) {
                var layerIndex = LayerAliasUtils.GetIndexOf(LayerAliasUtils.GetLayerByAlias(alias.Trim()).value);
                attackLayerMask.mask |= 1u << layerIndex;
            }

            return attackLayerMask;
        }
        
        public static string GetAliasOf(Layer layer) {
            return layerAliasMap[layer.value];
        }

        public static string GetAliasOf(uint layer) {
            Cache();
            return layerAliasMap[layer];
        }

        public static Layer GetLayerByAlias(string alias) {
            Cache();
            return new Layer {value = aliasLayerMap[alias]};
        }

        public static System.Collections.Generic.List<string> GetAliases() {
            Cache();
            return aliases;
        }

        public static int GetIndexOf(uint layer) {
            Cache();
            return aliases.IndexOf(GetAliasOf(layer));
        }

        private static void Cache() {
            if (customLayerAliasProvider != null)
                return;
            var derivedTypes = UnityEditor.TypeCache.GetTypesDerivedFrom<ILayerAliasProvider>();
            System.Type type = null;
            foreach (var derivedType in derivedTypes)
            {
                if (typeof(DefaultLayerAliasProvider).IsAssignableFrom(derivedType) == false) {
                    type = derivedType;
                    break;
                }
            }
            customLayerAliasProvider = (ILayerAliasProvider)System.Activator.CreateInstance(type ?? typeof(DefaultLayerAliasProvider));
            
            layerAliasMap = new System.Collections.Generic.Dictionary<uint, string>(32);
            aliasLayerMap = new System.Collections.Generic.Dictionary<string, uint>(32);
            aliases = new System.Collections.Generic.List<string>(32);
            var customAliases = customLayerAliasProvider.GetCustomAliases();
            for (int i = 0; i < 32; ++i) {
                uint value = 1u << i;
                string alias = customAliases.TryGetValue(value, out string customAlias) ? customAlias : $"Layer{i + 1}";
                layerAliasMap.Add(value, alias);
                aliasLayerMap.Add(alias, value);
                aliases.Add(alias);
            }
            
        }

    }

    public interface ILayerAliasProvider {

        System.Collections.Generic.Dictionary<uint, string> GetCustomAliases();
        
    }

    public class DefaultLayerAliasProvider : ILayerAliasProvider
    {

        private System.Collections.Generic.Dictionary<uint, string> aliases = new ();
        
        public System.Collections.Generic.Dictionary<uint, string> GetCustomAliases() {
            return aliases;
        }

    }

}