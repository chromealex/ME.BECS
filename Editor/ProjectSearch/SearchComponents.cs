using UnityEngine;
using UnityEditor;
using UnityEditor.Search;

namespace ME.BECS.Editor {

    public static class SearchComponents {

        [SearchItemProvider]
        internal static SearchProvider CreateProvider() {
            return new SearchProvider("bc:", "Components") {
                filterId = "bc:",
                priority = 0,
                showDetailsOptions = ShowDetailsOptions.Inspector | ShowDetailsOptions.Actions | ShowDetailsOptions.Default,
                fetchItems = (context, items, provider) => FetchItems(context, provider),
                fetchThumbnail = (item, context) => AssetDatabase.GetCachedIcon(item.id) as Texture2D,
                fetchPreview = (item, context, size, options) => AssetDatabase.GetCachedIcon(item.id) as Texture2D,
                fetchLabel = (item, context) => AssetDatabase.LoadMainAssetAtPath(item.id).name,
                toObject = (item, type) => AssetDatabase.LoadMainAssetAtPath(item.id),
                trackSelection = TrackSelection,
            };
        }

        private static System.Collections.Generic.IEnumerable<SearchItem> FetchItems(SearchContext context, SearchProvider provider) {
            if (context.empty == true) yield break;

            var query = context.searchQuery;
            bool searchNulls = (query == "null");
            foreach (var guid in AssetDatabase.FindAssets("t:EntityConfig")) {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var config = AssetDatabase.LoadAssetAtPath<ME.BECS.EntityConfig>(path);
                if (config != null) {
                    if (Has(config.data.components, query, searchNulls) == true ||
                        Has(config.sharedData.components, query, searchNulls) == true ||
                        Has(config.staticData.components, query, searchNulls) == true) {
                        yield return provider.CreateItem(context, path, config.name, path, null, config);
                    }
                }
            }
        }

        private static void TrackSelection(SearchItem searchItem, SearchContext searchContext) {
            EditorGUIUtility.PingObject(AssetDatabase.LoadMainAssetAtPath(searchItem.id));
        }

        private static bool Has<T>(T[] dataComponents, string query, bool searchNulls) where T : IComponentBase {
            foreach (var item in dataComponents) {
                if (searchNulls == true && item == null) return true;
                if (item?.GetType().FullName?.Contains(query, System.StringComparison.InvariantCultureIgnoreCase) == true) return true;
            }

            return false;
        }

    }

}