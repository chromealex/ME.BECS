using ME.BECS.Views;
using UnityEditor;
using UnityEditor.Search;
using UnityEngine;

namespace ME.BECS.Editor {
    public static class SearchModules {
        
        [SearchItemProvider]
        internal static SearchProvider CreateProvider() {
            return new SearchProvider("bm:", "Modules") {
                filterId = "bm:",
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
            foreach (var guid in AssetDatabase.FindAssets("t:prefab")) {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null) {
                    if (prefab.TryGetComponent(out EntityView entityView) == true
                        && Has(entityView.GetAllModules().items, query, searchNulls) == true) {
                        yield return provider.CreateItem(context, path, prefab.name, path, null, prefab);
                    }
                }
            }
        }

        private static void TrackSelection(SearchItem searchItem, SearchContext searchContext) {
            EditorGUIUtility.PingObject(AssetDatabase.LoadMainAssetAtPath(searchItem.id));
        }

        private static bool Has(ViewModules.Module[] dataComponents, string query, bool searchNulls) {
            foreach (var item in dataComponents) {
                if (searchNulls == true && item.module == null) return true;
                if (item.module?.GetType().FullName?.Contains(query, System.StringComparison.InvariantCultureIgnoreCase) == true) return true;
            }

            return false;
        }
    }
}