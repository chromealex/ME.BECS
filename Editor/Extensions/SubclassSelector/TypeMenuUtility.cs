using System;
using System.Linq;
using System.Collections.Generic;

namespace ME.BECS.Editor.Extensions.SubclassSelector {

    using BECS.Extensions.SubclassSelector;

    public static class TypeMenuUtility {

        public const string k_NullDisplayName = "<null>";

        public static ME.BECS.Extensions.SubclassSelector.AddTypeMenuAttribute GetAttribute(Type type) {
            return Attribute.GetCustomAttribute(type, typeof(ME.BECS.Extensions.SubclassSelector.AddTypeMenuAttribute)) as ME.BECS.Extensions.SubclassSelector.AddTypeMenuAttribute;
        }

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