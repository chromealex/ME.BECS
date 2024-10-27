namespace ME.BECS.Editor.JSON {

    using Unity.Mathematics;
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
                var serializers = UnityEditor.TypeCache.GetTypesDerivedFrom<ISerializer>();
                foreach (var serializerType in serializers) {
                    if (serializerType.IsInterface == true || serializerType.IsAbstract == true) continue;
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
                CsvImporter.EntityConfigCsvImporterEditor.Link(list, false);
                copy.managedReferenceValue = comp.componentInstance;

            }
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
            } catch (System.Exception ex) {
                return false;
            }
        }

    }

}