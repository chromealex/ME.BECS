using System;
using System.Linq;
using System.Collections.Generic;

namespace ME.BECS.Editor.Extensions.SubclassSelector {

    using BECS.Extensions.SubclassSelector;

    /// <summary>
    /// Provides type menu utility operations for the associated BECS data.
    /// </summary>
    public static class TypeMenuUtility {

        /// <summary>
        /// K null display name used by <c>TypeMenuUtility</c>.
        /// </summary>
        public const string k_NullDisplayName = "<null>";

        /// <summary>
        /// Returns attribute.
        /// </summary>
        public static ME.BECS.Extensions.SubclassSelector.AddTypeMenuAttribute GetAttribute(Type type) {
            return Attribute.GetCustomAttribute(type, typeof(ME.BECS.Extensions.SubclassSelector.AddTypeMenuAttribute)) as ME.BECS.Extensions.SubclassSelector.AddTypeMenuAttribute;
        }

        /// <summary>
        /// Returns splitted type path.
        /// </summary>
        public static string[] GetSplittedTypePath(Type type) {
            var typeMenu = TypeMenuUtility.GetAttribute(type);
            if (typeMenu != null) {
                return typeMenu.GetSplittedMenuName();
            } else {
                var name = type.FullName.Replace("+", ".");
                var splitIndex = name.LastIndexOf('.');
                if (splitIndex >= 0) {
                    return new string[] { name.Substring(0, splitIndex), name.Substring(splitIndex + 1) };
                } else {
                    return new string[] { type.Name };
                }
            }
        }

        /// <summary>
        /// Orders by type.
        /// </summary>
        public static IEnumerable<Type> OrderByType(this IEnumerable<Type> source) {
            return source.OrderBy(type => {
                if (type == null) {
                    return -999;
                }

                return TypeMenuUtility.GetAttribute(type)?.Order ?? 0;
            }).ThenBy(type => {
                if (type == null) {
                    return null;
                }

                return TypeMenuUtility.GetAttribute(type)?.MenuName ?? type.Name;
            });
        }

    }

}