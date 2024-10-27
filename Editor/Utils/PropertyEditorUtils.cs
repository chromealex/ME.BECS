using Enumerable = System.Linq.Enumerable;

namespace ME.BECS.Editor {

    public class PropertyEditorUtils {

        private static object GetValue_Imp(object source, string name, out System.Type fieldType) {
            fieldType = null;
            if (source == null) {
                return null;
            }

            var type = source.GetType();

            while (type != null) {
                var f = type.GetField(name, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (f != null) {
                    fieldType = f.FieldType;
                    return f.GetValue(source);
                }

                var p = type.GetProperty(
                    name,
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.IgnoreCase);
                if (p != null) {
                    return p.GetValue(source, null);
                }

                type = type.BaseType;
            }

            return null;
        }

        private static object GetValue_Imp(object source, string name, int index, out System.Type fieldType) {
            var enumerable = PropertyEditorUtils.GetValue_Imp(source, name, out fieldType) as System.Collections.IEnumerable;
            if (enumerable == null) {
                return null;
            }

            var enm = enumerable.GetEnumerator();
            //while (index-- >= 0)
            //    enm.MoveNext();
            //return enm.Current;

            for (var i = 0; i <= index; i++) {
                if (!enm.MoveNext()) {
                    return null;
                }
            }

            return enm.Current;
        }

        private static System.Type GetType_Imp(System.Type source, string name) {
            if (source == null) {
                return null;
            }

            var type = source;

            while (type != null) {
                var f = type.GetField(name, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (f != null) {
                    return f.FieldType;
                }

                var p = type.GetProperty(
                    name,
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.IgnoreCase);
                if (p != null) {
                    return p.PropertyType;
                }

                type = type.BaseType;
            }

            return null;
        }

        private static System.Type GetType_Imp(System.Type source, string name, int index) {
            var tp = PropertyEditorUtils.GetType_Imp(source, name);
            if (typeof(System.Collections.IEnumerable).IsAssignableFrom(tp) == true) {
                return tp.GenericTypeArguments[0];
            }
            return tp;
        }

        public static System.Type GetTargetTypeOfProperty(UnityEditor.SerializedProperty prop) {
            if (prop == null) {
                return null;
            }

            var path = prop.propertyPath.Replace(".Array.data[", "[");
            var obj = prop.serializedObject.targetObject.GetType();
            var elements = path.Split('.');
            foreach (var element in elements) {
                if (element.Contains("[")) {
                    var elementName = element.Substring(0, element.IndexOf("["));
                    var index = System.Convert.ToInt32(element.Substring(element.IndexOf("[")).Replace("[", "").Replace("]", ""));
                    obj = PropertyEditorUtils.GetType_Imp(obj, elementName, index);
                } else {
                    obj = PropertyEditorUtils.GetType_Imp(obj, element);
                }
            }

            return obj;
        }

        public static object GetTargetObjectOfProperty(UnityEditor.SerializedProperty prop) {
            return GetTargetObjectOfProperty(prop, out _);
        }

        public static object GetTargetObjectOfProperty(UnityEditor.SerializedProperty prop, out System.Type fieldType) {
            fieldType = null;
            if (prop == null) {
                return null;
            }

            var path = prop.propertyPath.Replace(".Array.data[", "[");
            object obj = prop.serializedObject.targetObject;
            var elements = path.Split('.');
            foreach (var element in elements) {
                if (element.Contains("[")) {
                    var elementName = element.Substring(0, element.IndexOf("["));
                    var index = System.Convert.ToInt32(element.Substring(element.IndexOf("[")).Replace("[", "").Replace("]", ""));
                    obj = PropertyEditorUtils.GetValue_Imp(obj, elementName, index, out fieldType);
                } else {
                    obj = PropertyEditorUtils.GetValue_Imp(obj, element, out fieldType);
                }
            }

            return obj;
        }

        public static uint GetLevel(UnityEditor.SerializedProperty prop, out uint arrayIndex) {
            arrayIndex = 0u;
            if (prop == null) {
                return 0u;
            }

            var path = prop.propertyPath.Replace(".Array.data[", "[");
            var elements = path.Split('.');
            var cnt = 0u;
            foreach (var element in elements) {
                if (element.Contains("[")) {
                    var index = System.Convert.ToUInt32(element.Substring(element.IndexOf("[")).Replace("[", "").Replace("]", ""));
                    arrayIndex = index;
                }
                ++cnt;
            }

            return cnt;
        }

        public static object GetTargetObjectOfProperty(UnityEditor.SerializedProperty prop, object targetObj) {
            var path = prop.propertyPath.Replace(".Array.data[", "[");
            var elements = path.Split('.');
            foreach (var element in elements) {
                if (element.Contains("[")) {
                    var elementName = element.Substring(0, element.IndexOf("["));
                    var index = System.Convert.ToInt32(element.Substring(element.IndexOf("[")).Replace("[", "").Replace("]", ""));
                    targetObj = PropertyEditorUtils.GetValue_Imp(targetObj, elementName, index, out _);
                } else {
                    targetObj = PropertyEditorUtils.GetValue_Imp(targetObj, element, out _);
                }
            }

            return targetObj;
        }

        public static void SetTargetObjectOfProperty(UnityEditor.SerializedProperty prop, object value) {
            var path = prop.propertyPath.Replace(".Array.data[", "[");
            object obj = prop.serializedObject.targetObject;
            var elements = path.Split('.');
            foreach (var element in Enumerable.Take(elements, elements.Length - 1)) {
                if (element.Contains("[")) {
                    var elementName = element.Substring(0, element.IndexOf("["));
                    var index = System.Convert.ToInt32(element.Substring(element.IndexOf("[")).Replace("[", "").Replace("]", ""));
                    obj = PropertyEditorUtils.GetValue_Imp(obj, elementName, index, out _);
                } else {
                    obj = PropertyEditorUtils.GetValue_Imp(obj, element, out _);
                }
            }

            if (System.Object.ReferenceEquals(obj, null)) {
                return;
            }

            try {
                var element = Enumerable.Last(elements);

                if (element.Contains("[")) {
                    //var tp = obj.GetType();
                    //var elementName = element.Substring(0, element.IndexOf("["));
                    //var index = System.Convert.ToInt32(element.Substring(element.IndexOf("[")).Replace("[", "").Replace("]", ""));
                    //var field = tp.GetField(elementName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    //var arr = field.GetValue(obj) as System.Collections.IList;
                    //arr[index] = value;
                    var elementName = element.Substring(0, element.IndexOf("["));
                    var index = System.Convert.ToInt32(element.Substring(element.IndexOf("[")).Replace("[", "").Replace("]", ""));
                    var arr = PropertyEditorUtils.GetValueDirect(element, elementName) as System.Collections.IList;
                    if (arr != null) {
                        arr[index] = value;
                    }
                } else {
                    //var tp = obj.GetType();
                    //var field = tp.GetField(element, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    //if (field != null)
                    //{
                    //    field.SetValue(obj, value);
                    //}
                    PropertyEditorUtils.SetValueDirect(obj, element, value);
                }

            } catch { }
        }

        public static bool SetValueDirect(object obj, string sprop, object value) {
            return PropertyEditorUtils.SetValueDirect(obj, sprop, value, null);
        }

        public static bool SetValueDirect(object obj, string sprop, object value, params object[] index) {
            if (string.IsNullOrEmpty(sprop)) {
                return false;
            }

            //if (sprop != null && sprop.Contains('.')) obj = DynamicUtil.ReduceSubObject(obj, sprop, out sprop);
            if (obj == null) {
                return false;
            }

            try {
                var vtp = value != null ? value.GetType() : null;
                var member = PropertyEditorUtils.GetValueSetterMemberFromType(obj.GetType(), sprop, vtp, true);
                if (member != null) {
                    switch (member.MemberType) {
                        case System.Reflection.MemberTypes.Field:
                            (member as System.Reflection.FieldInfo).SetValue(obj, value);
                            return true;
                    }
                }

                if (vtp != null) {
                    member = PropertyEditorUtils.GetValueSetterMemberFromType(obj.GetType(), sprop, null, true);
                    if (member != null) {
                        switch (member.MemberType) {
                            case System.Reflection.MemberTypes.Field:
                                (member as System.Reflection.FieldInfo).SetValue(obj, value);
                                return true;
                        }
                    }
                }
            } catch { }

            return false;
        }

        public static bool SetValueDirect(object obj, System.Reflection.MemberInfo member, object value) {
            return PropertyEditorUtils.SetValueDirect(obj, member, value, null);
        }

        public static bool SetValueDirect(object obj, System.Reflection.MemberInfo member, object value, params object[] index) {
            if (obj == null) {
                return false;
            }

            if (member == null) {
                return false;
            }

            try {
                switch (member.MemberType) {
                    case System.Reflection.MemberTypes.Field:
                        (member as System.Reflection.FieldInfo).SetValue(obj, value);
                        return true;
                }
            } catch { }

            return false;
        }

        public static object GetValueDirect(object obj, string sprop, params object[] args) {
            if (string.IsNullOrEmpty(sprop)) {
                return null;
            }

            //if (sprop != null && sprop.Contains('.')) obj = DynamicUtil.ReduceSubObject(obj, sprop, out sprop);
            if (obj == null) {
                return null;
            }

            try {
                var tp = obj.GetType();
                foreach (var member in PropertyEditorUtils.GetMembersFromType(tp, sprop, true)) {
                    switch (member.MemberType) {
                        case System.Reflection.MemberTypes.Field:
                            var field = member as System.Reflection.FieldInfo;
                            return field.GetValue(obj);
                    }
                }
            } catch { }

            return null;

            /*
            const BindingFlags BINDING = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            if (string.IsNullOrEmpty(sprop)) return null;
            //if (sprop != null && sprop.Contains('.')) obj = DynamicUtil.ReduceSubObject(obj, sprop, out sprop);
            if (obj == null) return null;
            try
            {
                var tp = obj.GetType();
                while (tp != null)
                {
                    var members = tp.GetMember(sprop, BINDING);
                    if (members == null || members.Length == 0) return null;
                    foreach (var member in members)
                    {
                        switch (member.MemberType)
                        {
                            case System.Reflection.MemberTypes.Field:
                                var field = member as System.Reflection.FieldInfo;
                                return field.GetValue(obj);
                            case System.Reflection.MemberTypes.Property:
                                {
                                    var prop = member as System.Reflection.PropertyInfo;
                                    var paramInfos = prop.GetIndexParameters();
                                    if (prop.CanRead && DynamicUtil.ParameterSignatureMatches(args, paramInfos, false))
                                    {
                                        return prop.GetValue(obj, args);
                                    }
                                    break;
                                }
                            case System.Reflection.MemberTypes.Method:
                                {
                                    var meth = member as System.Reflection.MethodInfo;
                                    var paramInfos = meth.GetParameters();
                                    if (DynamicUtil.ParameterSignatureMatches(args, paramInfos, false))
                                    {
                                        return meth.Invoke(obj, args);
                                    }
                                    break;
                                }
                        }
                    }
                    tp = tp.BaseType;
                }
            }
            catch
            {
            }
            return null;
            */
        }

        public static System.Reflection.MemberInfo GetValueSetterMemberFromType(System.Type tp, string sprop, System.Type valueType, bool includeNonPublic) {
            const System.Reflection.BindingFlags BINDING_PUBLIC = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance;
            const System.Reflection.BindingFlags PRIV_BINDING =
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly;
            if (tp == null) {
                throw new System.ArgumentNullException("tp");
            }

            //if (sprop.Contains('.'))
            //{
            //    tp = DynamicUtil.ReduceSubType(tp, sprop, includeNonPublic, out sprop);
            //    if (tp == null) return null;
            //}

            try {
                System.Type ltp;
                System.Reflection.MemberInfo[] members;

                //first strict test
                members = tp.GetMember(sprop, BINDING_PUBLIC);
                foreach (var member in members) {
                    if (PropertyEditorUtils.IsValidValueSetterMember(member, valueType)) {
                        return member;
                    }
                }

                ltp = tp;
                while (includeNonPublic && ltp != null) {
                    members = ltp.GetMember(sprop, PRIV_BINDING);
                    ltp = ltp.BaseType;
                    if (members == null || members.Length == 0) {
                        continue;
                    }

                    foreach (var member in members) {
                        if (PropertyEditorUtils.IsValidValueSetterMember(member, valueType)) {
                            return member;
                        }
                    }
                }
            } catch { }

            return null;
        }

        private static bool IsValidValueSetterMember(System.Reflection.MemberInfo member, System.Type valueType) {
            switch (member.MemberType) {
                case System.Reflection.MemberTypes.Field:
                    var field = member as System.Reflection.FieldInfo;
                    if ((valueType == null && !field.FieldType.IsValueType) ||
                        field.FieldType.IsAssignableFrom(valueType) ||
                        (PropertyEditorUtils.IsNumericType(field.FieldType) && PropertyEditorUtils.IsNumericType(valueType))) {
                        return true;
                    }

                    break;
            }

            return false;
        }

        public static bool IsNumericType(System.Type tp) {
            if (tp == null) {
                return false;
            }

            return tp.IsEnum || PropertyEditorUtils.IsNumericType(System.Type.GetTypeCode(tp));
        }

        public static bool IsNumericType(System.TypeCode code) {
            switch (code) {
                case System.TypeCode.SByte:
                    //5
                    return true;

                case System.TypeCode.Byte:
                    //6
                    return true;

                case System.TypeCode.Int16:
                    //7
                    return true;

                case System.TypeCode.UInt16:
                    //8
                    return true;

                case System.TypeCode.Int32:
                    //9
                    return true;

                case System.TypeCode.UInt32:
                    //10
                    return true;

                case System.TypeCode.Int64:
                    //11
                    return true;

                case System.TypeCode.UInt64:
                    //12
                    return true;

                case System.TypeCode.Single:
                    //13
                    return true;

                case System.TypeCode.Double:
                    //14
                    return true;

                case System.TypeCode.Decimal:
                    //15

                    return true;

                default:
                    return false;
            }
        }

        public static System.Collections.Generic.IEnumerable<System.Reflection.MemberInfo> GetMembersFromType(
            System.Type tp, string name, bool includeNonPublic,
            System.Reflection.MemberTypes mask = System.Reflection.MemberTypes.Field | System.Reflection.MemberTypes.Property | System.Reflection.MemberTypes.Method) {
            const System.Reflection.BindingFlags BINDING = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance;
            const System.Reflection.BindingFlags PRIV_BINDING =
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly;
            if (tp == null) {
                yield break;
            }

            foreach (var m in tp.GetMember(name, BINDING)) {
                if ((m.MemberType & mask) != 0) {
                    yield return m;
                }
            }

            if (includeNonPublic) {
                while (tp != null) {
                    foreach (var m in tp.GetMember(name, PRIV_BINDING)) {
                        if ((m.MemberType & mask) != 0) {
                            yield return m;
                        }
                    }

                    tp = tp.BaseType;
                }
            }
        }

        public static System.Type GetReturnType(System.Reflection.MemberInfo info) {
            if (info == null) {
                return null;
            }

            switch (info.MemberType) {
                case System.Reflection.MemberTypes.Field:
                    return (info as System.Reflection.FieldInfo).FieldType;
            }

            return null;
        }

        private static readonly System.Reflection.PropertyInfo propertyIsValidMethod =
            typeof(UnityEditor.SerializedProperty).GetProperty("isValid", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        public static bool IsValid(UnityEditor.SerializedProperty property) {
            if (property == null) {
                return false;
            }

            if (PropertyEditorUtils.propertyIsValidMethod != null) {
                return (bool)PropertyEditorUtils.propertyIsValidMethod.GetValue(property);
            }

            return false;
        }

    }

}