namespace ME.BECS.Editor.JSON {

    using System.Text;

    public interface ISerializer {

        bool IsValid(System.Type type);
        void Serialize(StringBuilder builder, object obj, UnityEditor.SerializedProperty property);
        void Deserialize(object obj, UnityEditor.SerializedProperty property);
        object FromString(System.Type fieldType, string value);

    }

    public abstract class SerializerBase<T> : ISerializer {

        public virtual bool IsValid(System.Type type) => typeof(T).IsAssignableFrom(type);
        public abstract void Serialize(StringBuilder builder, object obj, UnityEditor.SerializedProperty property);

        public virtual void Deserialize(object obj, UnityEditor.SerializedProperty property) {
            property.managedReferenceValue = obj;
        }
        
        public abstract object FromString(System.Type fieldType, string value);

    }
    
    public abstract class JsonUtils {

        private static readonly System.Collections.Generic.List<ISerializer> allSerializers = new System.Collections.Generic.List<ISerializer>();
        public static ISerializer GetSerializer(System.Type type) {
            if (allSerializers.Count == 0) {
                allSerializers.Clear();
                var bannedTypes = new System.Collections.Generic.HashSet<System.Type>();
                var types = new System.Collections.Generic.HashSet<System.Type>();
                var serializers = UnityEditor.TypeCache.GetTypesDerivedFrom<ISerializer>();
                foreach (var serializerType in serializers) {
                    if (serializerType.IsInterface == true || serializerType.IsAbstract == true) continue;
                    types.Add(serializerType);
                    if (serializerType.BaseType != null && serializerType.BaseType.IsAbstract == false && serializerType.BaseType.IsInterface == false) bannedTypes.Add(serializerType.BaseType);
                }
                
                foreach (var serializerType in types) {
                    if (bannedTypes.Contains(serializerType)) continue;
                    allSerializers.Add((ISerializer)System.Activator.CreateInstance(serializerType));
                }
            }

            foreach (var serializer in allSerializers) {
                if (serializer.IsValid(type) == true) {
                    return serializer;
                }
            }

            return null;
        }

        public static void JSONToComponent(string data, UnityEditor.SerializedProperty targetProperty) {
            var copy = targetProperty.Copy();
            var obj = JsonParser.ParseValue(data);
            if (obj is System.Collections.Generic.Dictionary<string, object> dictionary) {
                var keys = new System.Collections.Generic.Dictionary<string, string>();
                foreach (var kv in dictionary) {
                    Collect(kv.Key, kv.Value, keys);
                }

                void Collect(string key, object value, System.Collections.Generic.Dictionary<string, string> keys) {
                    if (value is System.Collections.IList || value is System.Collections.IDictionary) {
                        if (value is System.Collections.IList array) {
                            for (var index = 0; index < array.Count; ++index) {
                                var item = array[index];
                                Collect($"{key}[{index}]", item, keys);
                            }
                        } else if (value is System.Collections.Generic.Dictionary<string, object> dic) {
                            foreach (var kv in dic) {
                                Collect($"{key}/{kv.Key}", kv.Value, keys);
                            }
                        }
                    } else {
                        keys.Add(key, value.ToString());
                    }
                }

                var list = new System.Collections.Generic.List<ME.BECS.Editor.CsvImporter.EntityConfigCsvImporterEditor.ConfigFile>();
                var config = new ME.BECS.Editor.CsvImporter.EntityConfigCsvImporterEditor.ConfigFile() {
                    instance = copy.serializedObject.targetObject as EntityConfig,
                    components = new System.Collections.Generic.List<ME.BECS.Editor.CsvImporter.EntityConfigCsvImporterEditor.ConfigFile.Component>(),
                };
                list.Add(config);
                var comp = new ME.BECS.Editor.CsvImporter.EntityConfigCsvImporterEditor.ConfigFile.Component() {
                    fields = new System.Collections.Generic.Dictionary<string, string>(),
                    componentInstance = copy.managedReferenceValue,
                    type = PropertyEditorUtils.GetTargetObjectOfProperty(copy).GetType(),
                };
                config.components.Add(comp);
                foreach (var kv in keys) {
                    comp.fields.Add(kv.Key, kv.Value);
                }
                CsvImporter.EntityConfigCsvImporterEditor.Link(list);
                copy.managedReferenceValue = comp.componentInstance;

            }
        }

        public static string ComponentToCSV(UnityEditor.SerializedProperty srcProperty) {

            static System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, string>> GetFields(UnityEditor.SerializedProperty property) {
                var fields = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, string>>();
                var copy = property.Copy();
                var enterChildren = true;
                var valueBuilder = new StringBuilder();
                var level = PropertyEditorUtils.GetLevel(copy, out var lookUpArrayIndex) + 1u;
                while (copy.NextVisible(enterChildren)) {
                    enterChildren = false;
                    var curLevel = PropertyEditorUtils.GetLevel(copy, out var arrayIndex);
                    if (arrayIndex != lookUpArrayIndex || curLevel != level) break;
                    valueBuilder.Clear();
                    var val = PropertyEditorUtils.GetTargetObjectOfProperty(copy, out var valType);
                    var serializerPrimitive = GetSerializer(valType);
                    if (serializerPrimitive != null) {
                        serializerPrimitive.Serialize(valueBuilder, val, copy);
                        fields.Add(new System.Collections.Generic.KeyValuePair<string, string>(copy.name, valueBuilder.ToString()));
                    } else if (val is IUnmanagedList list && copy.serializedObject.targetObject is EntityConfig config) {
                        config.GetCollection(list.GetConfigId(), out var collectionData, out var collectionIndex);
                        var items = copy.serializedObject.FindProperty(nameof(EntityConfig.collectionsData)).FindPropertyRelative(nameof(EntityConfig.CollectionsData.items));
                        var arr = items.GetArrayElementAtIndex(collectionIndex).FindPropertyRelative(nameof(EntityConfig.CollectionsData.Collection.array));
                        var index = 0;
                        foreach (UnityEditor.SerializedProperty item in arr) {
                            var subFields = GetFields(item);
                            foreach (var kv in subFields) {
                                fields.Add(new System.Collections.Generic.KeyValuePair<string, string>($"{copy.name}[{index}]/{kv.Key}", kv.Value));
                            }
                            ++index;
                        }
                    } else {
                        var subFields = GetFields(copy);
                        foreach (var kv in subFields) {
                            fields.Add(new System.Collections.Generic.KeyValuePair<string, string>($"{copy.name}/{kv.Key}", kv.Value));
                        }
                    }
                }
                return fields;
            }

            var fields = GetFields(srcProperty);
            var type = EditorUtils.GetTypeFromPropertyField(srcProperty.managedReferenceFullTypename);
            var builder = new StringBuilder();
            if (fields.Count == 0) {
                // tag component
                {
                    builder.Append('"');
                    builder.Append(type.Name);
                    builder.Append('"');
                    builder.Append(',');
                    { // fields
                        builder.Append('"');
                        builder.Append('"');
                    }
                    builder.Append(',');
                    { // values
                        builder.Append("TRUE");
                    }
                    builder.Append('\n');
                }
            } else {
                for (var index = 0; index < fields.Count; ++index) {
                    var field = fields[index];
                    builder.Append('"');
                    if (index == 0) builder.Append(type.Name);
                    builder.Append('"');
                    builder.Append(',');
                    { // fields
                        builder.Append('"');
                        builder.Append(field.Key);
                        builder.Append('"');
                    }
                    builder.Append(',');
                    { // values
                        builder.Append(field.Value);
                    }
                    builder.Append('\n');
                }
            }

            return builder.ToString();
        }

        public static string ComponentToJSON(UnityEditor.SerializedProperty srcProperty) {
            return EditorUtils.ReFormatCode(ComponentToJSON_INTERNAL(srcProperty));
        }

        private static string ComponentToJSON_INTERNAL(UnityEditor.SerializedProperty srcProperty) {
            if (srcProperty.isArray == true) {
                var sbArr = new StringBuilder();
                sbArr.Append('[');
                for (int i = 0; i < srcProperty.arraySize; ++i) {
                    var item = srcProperty.GetArrayElementAtIndex(i);
                    sbArr.Append(ComponentToJSON_INTERNAL(item));
                    if (i < srcProperty.arraySize - 1) {
                        sbArr.Append(',');
                        sbArr.Append('\n');
                    }
                }
                sbArr.Append(']');
                return sbArr.ToString();
            }
            var srcObj = PropertyEditorUtils.GetTargetObjectOfProperty(srcProperty, out var srcObjType);
            if (srcObj is IUnmanagedList list) {
                if (srcProperty.serializedObject.targetObject is EntityConfig config) {
                    config.GetCollection(list.GetConfigId(), out var collectionData, out var collectionIndex);
                    var items = srcProperty.serializedObject.FindProperty(nameof(EntityConfig.collectionsData)).FindPropertyRelative(nameof(EntityConfig.CollectionsData.items));
                    var arr = items.GetArrayElementAtIndex(collectionIndex).FindPropertyRelative(nameof(EntityConfig.CollectionsData.Collection.array));
                    return ComponentToJSON_INTERNAL(arr);
                }
                return "[]";
            }
            var property = srcProperty.Copy();
            var serializer = GetSerializer(srcObjType);
            var sb = new StringBuilder();
            if (serializer != null) {
                serializer.Serialize(sb, srcObj, property);
                return sb.ToString();
            }
            sb.Append('{');
            sb.Append('\n');
            {
                if (property.hasVisibleChildren == true) {
                    var level = PropertyEditorUtils.GetLevel(property, out var lookUpArrayIndex) + 1u;
                    var enterChildren = true;
                    var idx = 0u;
                    while (property.NextVisible(enterChildren) == true) {
                        enterChildren = false;
                        var curLevel = PropertyEditorUtils.GetLevel(property, out var arrayIndex);
                        if (arrayIndex != lookUpArrayIndex || curLevel != level) break;
                        if (idx > 0u) {
                            sb.Append(',');
                            sb.Append('\n');
                        }
                        ++idx;
                        sb.Append('"');
                        sb.Append(property.name);
                        sb.Append('"');
                        sb.Append(": ");
                        if (property.propertyType == UnityEditor.SerializedPropertyType.Generic) {
                            sb.Append(ComponentToJSON_INTERNAL(property));
                            continue;
                        }
                        var val = PropertyEditorUtils.GetTargetObjectOfProperty(property, out var valType);
                        var serializerPrimitive = GetSerializer(valType);
                        if (serializerPrimitive != null) {
                            serializerPrimitive.Serialize(sb, val, property);
                        } else {
                            sb.Append(val);
                        }
                    }
                }
            }
            sb.Append('\n');
            sb.Append('}');
            return sb.ToString();
        }

        public static bool IsValidJson(string data) {
            try {
                var obj = JsonParser.ParseValue(data);
                return obj != null && obj is System.Collections.Generic.Dictionary<string, object>;
            } catch (System.Exception) {
                return false;
            }
        }

    }

}