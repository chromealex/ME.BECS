using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.IMGUI.Controls;

namespace ME.BECS.Editor.Extensions.SubclassSelector {

    /// <summary>
    /// Stores advanced type popup item for the associated editor API.
    /// </summary>
    public class AdvancedTypePopupItem : AdvancedDropdownItem {

        /// <summary>
        /// Type descriptor used by the associated operation.
        /// </summary>
        public Type Type { get; }

        /// <summary>
        /// Initializes <c>AdvancedTypePopupItem</c> from the supplied type, name.
        /// </summary>
        public AdvancedTypePopupItem(Type type, string name) : base(name) {
            this.Type = type;
        }

    }

    /// <summary>
    /// A type popup with a fuzzy finder.
    /// </summary>
    public class AdvancedTypePopup : AdvancedDropdown {

        private const int kMaxNamespaceNestCount = 16;

        /// <summary>
        /// Adds to.
        /// </summary>
        public static void AddTo(bool showNullElement, AdvancedDropdownItem root, IEnumerable<Type> types) {
            var itemCount = 0;

            if (showNullElement == true) {
                // Add null item.
                var nullItem = new AdvancedTypePopupItem(null, TypeMenuUtility.k_NullDisplayName) {
                    id = itemCount++,
                };
                root.AddChild(nullItem);
            }

            var typeArray = types.OrderByType().ToArray();

            // Single namespace if the root has one namespace and the nest is unbranched.
            var isSingleNamespace = true;
            var namespaces = new string[AdvancedTypePopup.kMaxNamespaceNestCount];
            foreach (var type in typeArray) {
                if (type == null) continue;
                var splittedTypePath = TypeMenuUtility.GetSplittedTypePath(type);
                if (splittedTypePath.Length <= 1) {
                    continue;
                }

                // If they explicitly want sub category, let them do.
                if (TypeMenuUtility.GetAttribute(type) != null) {
                    isSingleNamespace = false;
                    break;
                }

                for (var k = 0; splittedTypePath.Length - 1 > k; k++) {
                    var ns = namespaces[k];
                    if (ns == null) {
                        namespaces[k] = splittedTypePath[k];
                    } else if (ns != splittedTypePath[k]) {
                        isSingleNamespace = false;
                        break;
                    }
                }

                if (!isSingleNamespace) {
                    break;
                }
            }

            // Add type items.
            foreach (var type in typeArray) {
                if (type == null) continue;
                var splittedTypePath = TypeMenuUtility.GetSplittedTypePath(type);
                if (splittedTypePath.Length == 0) {
                    continue;
                }

                var parent = root;

                // Add namespace items.
                if (!isSingleNamespace) {
                    for (var k = 0; splittedTypePath.Length - 1 > k; k++) {
                        var foundItem = AdvancedTypePopup.GetItem(parent, splittedTypePath[k]);
                        if (foundItem != null) {
                            parent = foundItem;
                        } else {
                            var newItem = new AdvancedDropdownItem(splittedTypePath[k]) {
                                id = itemCount++,
                            };
                            parent.AddChild(newItem);
                            parent = newItem;
                        }
                    }
                }

                // Add type item.
                var item = new AdvancedTypePopupItem(type, ObjectNames.NicifyVariableName(splittedTypePath[splittedTypePath.Length - 1])) {
                    id = itemCount++,
                };
                parent.AddChild(item);
            }
        }

        private static AdvancedDropdownItem GetItem(AdvancedDropdownItem parent, string name) {
            foreach (var item in parent.children) {
                if (item.name == name) {
                    return item;
                }
            }

            return null;
        }

        private static readonly float k_HeaderHeight = EditorGUIUtility.singleLineHeight * 2f;

        private Type[] m_Types;
        private bool showNullElement;

        /// <summary>
        /// Raised when item selected is reported by this API.
        /// </summary>
        public event Action<AdvancedTypePopupItem> OnItemSelected;
        
        /// <summary>
        /// Initializes <c>AdvancedTypePopup</c> from the supplied types, max line count, state, show null element, min size.
        /// </summary>
        public AdvancedTypePopup(IEnumerable<Type> types, int maxLineCount, AdvancedDropdownState state, bool showNullElement, Vector2 minSize) : base(state) {
            this.SetTypes(types);
            this.minimumSize = minSize;
            this.minimumSize = new Vector2(this.minimumSize.x, EditorGUIUtility.singleLineHeight * maxLineCount + AdvancedTypePopup.k_HeaderHeight);
            this.showNullElement = showNullElement;
        }

        /// <summary>
        /// Sets types.
        /// </summary>
        public void SetTypes(IEnumerable<Type> types) {
            this.m_Types = types.ToArray();
        }

        /// <summary>
        /// Builds root.
        /// </summary>
        protected override AdvancedDropdownItem BuildRoot() {
            var root = new AdvancedDropdownItem("Select Type");
            AdvancedTypePopup.AddTo(this.showNullElement, root, this.m_Types);
            return root;
        }

        /// <summary>
        /// Applies the selected type from the editor dropdown.
        /// </summary>
        protected override void ItemSelected(AdvancedDropdownItem item) {
            base.ItemSelected(item);
            if (item is AdvancedTypePopupItem typePopupItem) {
                this.OnItemSelected?.Invoke(typePopupItem);
            }
        }

    }

}