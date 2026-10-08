namespace ME.BECS.Units.Editor {

    /// <summary>
    /// Provides helper operations for layer alias.
    /// </summary>
    public static class LayerAliasUtils {
        
        private static System.Collections.Generic.Dictionary<uint, string> layerAliasMap;
        private static System.Collections.Generic.Dictionary<string, uint> aliasLayerMap;
        private static System.Collections.Generic.List<string> aliases;
        private static ILayerAliasProvider customLayerAliasProvider;
        private static System.Text.StringBuilder sb;

        /// <summary>
        /// Formats a unit layer mask using the configured layer aliases.
        /// </summary>
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

        /// <summary>
        /// Parses configured unit layer aliases into a layer mask.
        /// </summary>
        public static LayerMask StringToLayerMask(string value) {
            var layers = value.Split(',', System.StringSplitOptions.RemoveEmptyEntries);
            var attackLayerMask = new ME.BECS.Units.LayerMask();
            
            foreach (var alias in layers) {
                var layerIndex = LayerAliasUtils.GetIndexOf(LayerAliasUtils.GetLayerByAlias(alias.Trim()).value);
                attackLayerMask.mask |= 1u << layerIndex;
            }

            return attackLayerMask;
        }
        
        /// <summary>
        /// Returns alias of.
        /// </summary>
        public static string GetAliasOf(Layer layer) {
            return layerAliasMap[layer.value];
        }

        /// <summary>
        /// Returns alias of.
        /// </summary>
        public static string GetAliasOf(uint layer) {
            Cache();
            return layerAliasMap[layer];
        }

        /// <summary>
        /// Returns layer by alias.
        /// </summary>
        public static Layer GetLayerByAlias(string alias) {
            Cache();
            return new Layer {value = aliasLayerMap[alias]};
        }

        /// <summary>
        /// Returns aliases.
        /// </summary>
        public static System.Collections.Generic.List<string> GetAliases() {
            Cache();
            return aliases;
        }

        /// <summary>
        /// Returns index of.
        /// </summary>
        public static int GetIndexOf(uint layer) {
            Cache();
            return aliases.IndexOf(GetAliasOf(layer));
        }

        private static void Cache() {
            
            if (customLayerAliasProvider != null) return;
            
            var derivedTypes = UnityEditor.TypeCache.GetTypesDerivedFrom<ILayerAliasProvider>();
            System.Type type = null;
            foreach (var derivedType in derivedTypes) {
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

    /// <summary>
    /// Defines the operations required by layer alias provider.
    /// </summary>
    public interface ILayerAliasProvider {

        /// <summary>
        /// Returns custom aliases.
        /// </summary>
        System.Collections.Generic.Dictionary<uint, string> GetCustomAliases();
        
    }

    /// <summary>
    /// Defines default layer alias provider state and operations.
    /// </summary>
    public class DefaultLayerAliasProvider : ILayerAliasProvider {

        private readonly System.Collections.Generic.Dictionary<uint, string> aliases = new ();
        
        /// <summary>
        /// Returns custom aliases.
        /// </summary>
        public System.Collections.Generic.Dictionary<uint, string> GetCustomAliases() {
            return this.aliases;
        }

    }

}