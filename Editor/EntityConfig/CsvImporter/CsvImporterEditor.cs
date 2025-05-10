using System.Linq;

namespace ME.BECS.Editor.CsvImporter {

    using ME.BECS.CsvImporter;
    using UnityEngine;
    using UnityEditor;
    using UnityEngine.UIElements;
    using scg = System.Collections.Generic;
    
    [CustomEditor(typeof(EntityConfigCsvImporter))]
    public class EntityConfigCsvImporterEditor : Editor {

        public StyleSheet styleSheetBase;
        public StyleSheet styleSheetTooltip;
        public StyleSheet styleSheet;

        private Button loadingButton;
        private Label loadingIndicator;
        private VisualElement loadingIndicatorContainer;
        private Label result;
        
        private void LoadStyle() {
            if (this.styleSheetBase == null) {
                this.styleSheetBase = EditorUtils.LoadResource<StyleSheet>("ME.BECS.Resources/Styles/CsvImporter.uss");
            }
            if (this.styleSheetTooltip == null) {
                this.styleSheetTooltip = EditorUtils.LoadResource<StyleSheet>("ME.BECS.Resources/Styles/Tooltip.uss");
            }
            if (this.styleSheet == null) {
                this.styleSheet = EditorUtils.LoadResource<StyleSheet>("ME.BECS.Resources/Styles/EntityConfig.uss");
            }
        }

        public override VisualElement CreateInspectorGUI() {

            this.LoadStyle();
            var root = new VisualElement();
            root.styleSheets.Add(this.styleSheetBase);
            root.styleSheets.Add(this.styleSheet);
            root.styleSheets.Add(this.styleSheetTooltip);
            root.AddToClassList("root");

            var targetDir = this.serializedObject.FindProperty("targetDirectory");
            {
                var container = new VisualElement();
                EditorUIUtils.DrawTooltip(container, "Target directory which will be used to generate entity config files.");
                root.Add(container);
                var prop = new UnityEditor.UIElements.PropertyField(targetDir);
                prop.AddToClassList("target-directory");
                container.Add(prop);
            }

            var csvUrls = this.serializedObject.FindProperty("csvUrls");
            {
                var container = new VisualElement();
                EditorUIUtils.DrawTooltip(container, "Multiple csv urls which data will be combined to make cross-links.");
                root.Add(container);
                var prop = new UnityEditor.UIElements.PropertyField(csvUrls);
                prop.AddToClassList("csv-urls");
                container.Add(prop);
            }

            var button = new Button(() => {
                var strings = new string[csvUrls.arraySize];
                for (int i = 0; i < csvUrls.arraySize; ++i) {
                    strings[i] = csvUrls.GetArrayElementAtIndex(i).stringValue;
                }

                this.loadingButton.SetEnabled(false);
                this.loadingIndicatorContainer.style.display = DisplayStyle.Flex;
                this.Load(strings, AssetDatabase.GetAssetPath(targetDir.objectReferenceValue), () => {
                    this.loadingButton.SetEnabled(true);
                    this.loadingIndicatorContainer.style.display = DisplayStyle.None;
                });
            }) { text = "Load" };
            button.AddToClassList("load-button");
            this.loadingButton = button;
            root.Add(button);

            this.loadingIndicatorContainer = new VisualElement();
            this.loadingIndicatorContainer.AddToClassList("loader");
            root.Add(this.loadingIndicatorContainer);
            
            this.completeLabel = new Label();
            this.completeLabel.AddToClassList("complete-progress");
            this.loadingIndicatorContainer.Add(this.completeLabel);

            this.loadingIndicator = new Label();
            this.loadingIndicator.AddToClassList("label-progress");
            this.loadingIndicatorContainer.Add(this.loadingIndicator);

            this.result = new Label();
            this.result.AddToClassList("result");
            this.result.pickingMode = PickingMode.Ignore;
            root.Add(this.result);

            return root;

        }

        private int loaderIndex;
        private readonly System.Text.StringBuilder loaderText = new System.Text.StringBuilder();
        private string GetLoaderText() {
            ++this.loaderIndex;
            const int dots = 5;
            var idx = this.loaderIndex % dots;
            this.loaderText.Clear();
            for (int i = 0; i < dots; ++i) {
                if (idx == i) {
                    this.loaderText.Append("<size=10>[</size>");
                    this.loaderText.Append('.');
                    this.loaderText.Append("<size=10>]</size>");
                } else {
                    this.loaderText.Append("<size=10>[</size>");
                    this.loaderText.Append(' ');
                    this.loaderText.Append("<size=10>]</size>");
                }
            }
            return this.loaderText.ToString();
        }

        private Label completeLabel;
        public void Load(string[] urls, string targetDir, System.Action callback) {

            this.result.RemoveFromClassList("hide");
            this.result.RemoveFromClassList("success");
            this.result.RemoveFromClassList("failed");

            this.completeLabel.text = $"0/{urls.Length} Completed";
            var list = new scg::List<UnityEngine.Networking.UnityWebRequest>();
            for (var index = 0; index < urls.Length; ++index) {
                
                var url = urls[index];
                var www = UnityEngine.Networking.UnityWebRequest.Get(url);
                www.SendWebRequest();
                list.Add(www);
                
            }

            UnityEditor.EditorApplication.delayCall += OnDelayCall;
            return;

            void OnDelayCall() {
                var allDone = true;
                var completed = 0;
                foreach (var item in list) {
                    if (item.isDone == false) {
                        allDone = false;
                    } else {
                        ++completed;
                    }
                }

                this.loadingIndicator.text = this.GetLoaderText();
                this.completeLabel.text = $"{completed}/{urls.Length} Completed";
                if (allDone == false) {
                    UnityEditor.EditorApplication.delayCall += OnDelayCall;
                } else {
                    var hasErrors = false;
                    var err = string.Empty;
                    var configs = new scg::List<ConfigFile>();
                    var csvs = new scg::List<scg::List<string[]>>();
                    var configsPerItem = new scg::List<scg::List<ConfigFile>>();
                    var data = new scg::List<string>();
                    var versions = new scg::List<string>();
                    foreach (var item in list) {
                        hasErrors = item.responseCode != 200 || string.IsNullOrEmpty(item.error) == false;
                        if (hasErrors == true) {
                            err = item.error;
                            break;
                        }

                        data.Add(item.downloadHandler.text);
                    }

                    foreach (var item in data) {
                        var csv = CSVParser.ReadCSV(item);
                        csvs.Add(csv);
                        var configFiles = ParseConfigs(csv, targetDir, out var ver, out var name);
                        configsPerItem.Add(configFiles);
                        configs.AddRange(configFiles);
                        versions.Add(ver);
                        Debug.Log($"Configs from list {name} with version {ver} has been updated");
                    }

                    { // add project configs
                        foreach (var item in ObjectReferenceRegistry.data.items) {
                            var obj = new ObjectItem(item);
                            if (obj.Is<EntityConfig>() == true) {
                                configs.Add(new ConfigFile(obj.Load<EntityConfig>()));
                            }
                        }
                    }
                    
                    foreach (var config in configs) {
                        CreateConfig(config);
                    }
                    
                    for (var index = 0; index < data.Count; ++index) {
                        ParseBaseConfigs(configs, configsPerItem[index], csvs[index]);
                    }

                    { // assign configs to ObjectReferenceRegistry
                        var startIndex = -1;
                        var count = 0;
                        foreach (var config in configs) {
                            ObjectReferenceRegistry.data.Add(config.instance, out var isNew);
                            if (isNew == true) {
                                if (startIndex == -1) startIndex = ObjectReferenceRegistry.data.items.Length - 1;
                                ++count;
                            }
                        }
                        if (startIndex >= 0) ObjectReferenceValidate.Validate(startIndex, count);
                    }
                    
                    for (var index = 0; index < data.Count; ++index) {
                        Parse(configs, configsPerItem[index], csvs[index]);
                    }

                    if (hasErrors == true) {
                        this.result.AddToClassList("failed");
                        this.result.text = err;
                    } else {
                        Link(configs);
                        this.result.AddToClassList("success");
                        this.result.text = $"Operation succeed. Version has been updated to {string.Join(", ", versions)}.";
                    }

                    EditorApplication.delayCall += () => { this.result.AddToClassList("hide"); };

                    callback.Invoke();
                }
            }
        }

        private static void CreateConfig(ConfigFile config) {
            if (config.instance != null) return;
            EntityConfig configInstance = null;
            if (config.groupPath != null) {
                // Create group if not exist
                GroupConfig groupInstance = null;
                if (System.IO.File.Exists(config.groupPath) == false) {
                    groupInstance = GroupConfig.CreateInstance<GroupConfig>();
                    AssetDatabase.CreateAsset(groupInstance, config.groupPath);
                } else {
                    groupInstance = AssetDatabase.LoadAssetAtPath<GroupConfig>(config.groupPath);
                }

                var allAssets = AssetDatabase.LoadAllAssetsAtPath(config.groupPath);
                var configObj = allAssets.FirstOrDefault(x => x.name == config.name);
                if (configObj == null) {
                    // Add new config
                    configInstance = EntityConfig.CreateInstance<EntityConfig>();
                    configInstance.name = config.name;
                    AssetDatabase.AddObjectToAsset(configInstance, config.groupPath);
                    if (groupInstance.configs == null) groupInstance.configs = System.Array.Empty<EntityConfig>();
                    var list = groupInstance.configs.ToList();
                    list.Add(configInstance);
                    groupInstance.configs = list.ToArray();
                    EditorUtility.SetDirty(groupInstance);
                    AssetDatabase.SaveAssetIfDirty(groupInstance);
                    AssetDatabase.ImportAsset(config.groupPath);
                } else {
                    configInstance = (EntityConfig)configObj;
                }

            } else {
                if (System.IO.File.Exists(config.fullPath) == false) {
                    configInstance = EntityConfig.CreateInstance<EntityConfig>();
                    AssetDatabase.CreateAsset(configInstance, config.fullPath);
                    AssetDatabase.ImportAsset(config.fullPath);
                } else {
                    configInstance = AssetDatabase.LoadAssetAtPath<EntityConfig>(config.fullPath);
                }
            }
            configInstance.collectionsData = new EntityConfig.CollectionsData();
            config.instance = configInstance;
            EditorUtility.SetDirty(configInstance);
        }

        public class ConfigFile {

            public class Component {

                public string name;
                public System.Type type;
                public scg::Dictionary<string, string> fields;
                public object componentInstance;

            }

            public class Aspect {

                public string name;
                public System.Type type;

            }

            public bool imported;
            public string name;
            public string path;
            public string fullPath;
            public string groupPath;
            public int baseConfig;
            public scg::List<Component> components;
            public scg::List<Aspect> aspects;
            public EntityConfig instance;

            public ConfigFile() { }

            public ConfigFile(EntityConfig config) {
                this.name = config.name;
                this.fullPath = AssetDatabase.GetAssetPath(config);
                this.path = EditorUtils.GetFullPathWithoutExtension(this.fullPath);
                this.instance = config;
                this.baseConfig = -1;
                this.imported = false;
            }

            public ConfigFile(string targetDir, string data, string groupsBy) {
                this.name = data;
                this.path = System.IO.Path.Combine(targetDir, data);
                this.fullPath = $"{this.path}.asset";
                this.baseConfig = -1;
                this.components = new scg::List<Component>();
                this.aspects = new scg::List<Aspect>();
                this.imported = true;
                var hasGroup = false;
                var groups = groupsBy.Split(',', System.StringSplitOptions.RemoveEmptyEntries);
                foreach (var groupByStr in groups) {
                    var groupBy = groupByStr.Trim();
                    if (string.IsNullOrEmpty(groupBy) == false && this.fullPath.Contains(groupBy) == true) {
                        var splitted = this.fullPath.Split(new string[] { groupBy }, System.StringSplitOptions.RemoveEmptyEntries);
                        this.groupPath = $"{splitted[0].TrimEnd('/')}/{groupBy}.asset";
                        this.name = splitted[1].TrimStart('/').Split('/').Last().Replace(".asset", string.Empty);
                        EditorUtils.CreateDirectoriesByPath(this.groupPath);
                        hasGroup = true;
                        break;
                    }
                }
                if (hasGroup == false) {
                    EditorUtils.CreateDirectoriesByPath(this.fullPath);
                    this.groupPath = null;
                }
            }

        }

        public static scg::List<ConfigFile> ParseConfigs(scg::List<string[]> csv, string targetDir, out string version, out string name) {
            version = csv[0][0];
            name = csv[0][1];
            var groupBy = csv[1][1];
            var configFiles = new scg::List<ConfigFile>();
            var offset = 2;
            var line = csv[0];
            {
                // Read config names
                for (int j = offset; j < line.Length; ++j) {
                    var configName = line[j];
                    configFiles.Add(new ConfigFile(targetDir, configName, groupBy));
                }
            }
            return configFiles;
        }

        public static void ParseBaseConfigs(scg::List<ConfigFile> allConfigs, scg::List<ConfigFile> configFiles, scg::List<string[]> csv) {
            var offset = 2;
            var line = csv[1];
            {
                // Read base configs
                for (int j = offset; j < line.Length; ++j) {
                    var configName = line[j];
                    if (string.IsNullOrEmpty(configName) == false) {
                        var idx = allConfigs.FindIndex(x => x.path.EndsWith(configName));
                        if (idx == -1) {
                            var configObj = EditorUtils.GetAssetByPathPart<EntityConfig>(configName);
                            if (configObj != null) {
                                allConfigs.Add(new ConfigFile(configObj));
                                idx = allConfigs.Count - 1;
                            }
                        }
                        var configIdx = j - offset;
                        var config = configFiles[configIdx];
                        config.baseConfig = idx;
                    }
                }
            }
        }

        public static void Parse(scg::List<ConfigFile> allConfigs, scg::List<ConfigFile> configFiles, scg::List<string[]> csv) {
            var offset = 2;
            var components = TypeCache.GetTypesDerivedFrom<IComponentBase>().Where(x => EditorUtils.IsValidTypeForAssembly(false, x)).ToArray();
            var aspects = TypeCache.GetTypesDerivedFrom<IAspect>().Where(x => EditorUtils.IsValidTypeForAssembly(false, x)).ToArray();
            var componentName = string.Empty;
            System.Type componentType = null;
            for (int i = 2; i < csv.Count; ++i) {
                var line = csv[i];
                {
                    // Read components and aspects
                    if (string.IsNullOrEmpty(line[0]) == false || string.IsNullOrEmpty(componentName) == true) {
                        componentName = line[0];
                        componentType = components.FirstOrDefault(x => x.FullName.EndsWith(componentName));
                        if (componentType == null) componentType = aspects.FirstOrDefault(x => x.FullName.EndsWith(componentName));
                    }

                    if (componentType == null) {
                        Debug.LogWarning($"Component Type or Aspect Type not found: {componentName}");
                        continue;
                    }

                    var isTag = componentType.GetFields().Length == 0;
                    
                    var isAspect = typeof(IAspect).IsAssignableFrom(componentType);
                    for (int j = offset; j < line.Length; ++j) {
                        var configIdx = j - offset;
                        var fieldName = line[1];
                        var value = line[j];
                        if (string.IsNullOrEmpty(value) == true) continue;
                        var configFile = configFiles[configIdx];
                        if (isAspect == true) {
                            if (value == "0" || value == "FALSE") continue;
                            var aspect = configFile.aspects.FirstOrDefault(x => x.type == componentType);
                            if (aspect == null) {
                                aspect = new ConfigFile.Aspect() {
                                    name = componentName,
                                    type = componentType,
                                };
                                configFile.aspects.Add(aspect);
                            }
                        } else {
                            if (isTag == true) {
                                if (value == "0" || value == "FALSE") continue;
                            }
                            var component = configFile.components.FirstOrDefault(x => x.type == componentType);
                            if (component == null) {
                                component = new ConfigFile.Component() {
                                    name = componentName,
                                    type = componentType,
                                    componentInstance = System.Activator.CreateInstance(componentType),
                                    fields = new System.Collections.Generic.Dictionary<string, string>(),
                                };
                                configFile.components.Add(component);
                            }

                            if (component.fields.ContainsKey(fieldName) == false) {
                                component.fields.Add(fieldName, value);
                            } else {
                                Debug.LogWarning($"Key duplicate {fieldName} in component {componentName}");
                            }
                        }
                    }
                }
            }
        }

        public static void Link(scg::List<ConfigFile> configFiles) {

            //ValidateConfigs(configFiles);
            
            var temp = TempComponent.CreateInstance<TempComponent>();
            foreach (var config in configFiles) {
                if (config.components == null) continue;
                foreach (var comp in config.components) {
                    var instance = comp.componentInstance;
                    temp.component = instance;
                    temp.config = config.instance;
                    var so = new SerializedObject(temp);
                    foreach (var field in comp.fields) {
                        so.Update();
                        var component = so.FindProperty("component");
                        var keys = field.Key.Split('/');
                        for (int i = 0; i < keys.Length; ++i) {
                            var key = keys[i];
                            if (component == null) break;
                            if (key.EndsWith(']') == true) {
                                // array element
                                var arr = key.Split('[');
                                var keyArrIndex = int.Parse(arr[1].TrimEnd(']'));
                                
                                component = component.FindPropertyRelative(arr[0]);
                                PropertyEditorUtils.GetTargetObjectOfProperty(component, out var fieldType);
                                SerializedProperty idProp = null;
                                if (typeof(IMemArray).IsAssignableFrom(fieldType) == true) {
                                    idProp = component.FindPropertyRelative("data").FindPropertyRelative("Length");
                                } else if (typeof(IMemList).IsAssignableFrom(fieldType) == true) {
                                    idProp = component.FindPropertyRelative("Count");
                                }

                                if (idProp == null) continue;
                                
                                var id = config.instance.GetCollection(idProp.uintValue, out var collectionData, out var collectionIndex);
                                idProp.uintValue = id;
                                while (keyArrIndex >= collectionData.array.Count) {
                                    collectionData.array.Add(null);
                                }
                                
                                component.serializedObject.ApplyModifiedProperties();

                                component = new SerializedObject(so.FindProperty("config").objectReferenceValue).FindProperty("collectionsData").FindPropertyRelative("items").GetArrayElementAtIndex(collectionIndex).FindPropertyRelative("array").GetArrayElementAtIndex(keyArrIndex);
                                if (component.managedReferenceValue == null) {
                                    component.managedReferenceValue = System.Activator.CreateInstance(fieldType.GenericTypeArguments[0]);
                                }

                            } else {
                                component = component.FindPropertyRelative(key);
                            }
                        }

                        if (component == null) {
                            if (string.IsNullOrEmpty(field.Key) == true && comp.type.GetFields().Length == 0) {
                                continue;
                            }
                            Debug.LogWarning($"Key not found {field.Key} in component {comp.type.FullName}");
                            continue;
                        }

                        {
                            component.serializedObject.ApplyModifiedProperties();
                            component.serializedObject.Update();
                            //var fieldInfo = comp.GetField(field.Key);
                            //var type = fieldInfo.FieldType;
                            PropertyEditorUtils.GetTargetObjectOfProperty(component, out var type);
                            if (type == null) {
                                Debug.LogError($"Type is null for component {component.propertyPath}");
                            }
                            var serializer = JSON.JsonUtils.GetSerializer(type);
                            if (serializer != null) {
                                var obj = serializer.FromString(type, field.Value);
                                try {
                                    component.boxedValue = obj;
                                } catch (System.Exception ex) {
                                    Debug.LogError($"Serializer returns invalid value `{obj}` while source value was `{field.Value}` in field `{component.type}` at config {config.fullPath}");
                                    Debug.LogException(ex);
                                }
                                component.serializedObject.ApplyModifiedProperties();
                            } else {
                                Debug.LogWarning($"Serializer was not found for component {comp.type.FullName} type {type.FullName}");
                            }
                        }
                    }

                    comp.componentInstance = temp.component;
                }
            }
            TempComponent.DestroyImmediate(temp);

            ValidateConfigs(configFiles);
            
        }

        private static void ValidateConfigs(scg::List<ConfigFile> configFiles) {

            foreach (var config in configFiles) {
                if (config.imported == false) continue;
                if (config.baseConfig >= 0) config.instance.baseConfig = configFiles[config.baseConfig].instance;
                var res = false;
                res |= Apply(ref config.instance.aspects.components, config.aspects?.Select(x => (IAspect)System.Activator.CreateInstance(x.type)).ToArray());
                res |= Apply(ref config.instance.data.components, config.components.Where(x => x.componentInstance is IConfigComponent).Select(x => (IConfigComponent)x.componentInstance).ToArray());
                res |= Apply(ref config.instance.sharedData.components, config.components.Where(x => x.componentInstance is IConfigComponentShared).Select(x => (IConfigComponentShared)x.componentInstance).ToArray());
                res |= Apply(ref config.instance.staticData.components, config.components.Where(x => x.componentInstance is IConfigComponentStatic).Select(x => (IConfigComponentStatic)x.componentInstance).ToArray());
                //config.instance.aspects.components = config.aspects?.Select(x => (IAspect)System.Activator.CreateInstance(x.type)).ToArray();
                //config.instance.data.components = config.components.Where(x => x.componentInstance is IConfigComponent).Select(x => (IConfigComponent)x.componentInstance).ToArray();
                //config.instance.sharedData.components = config.components.Where(x => x.componentInstance is IConfigComponentShared).Select(x => (IConfigComponentShared)x.componentInstance).ToArray();
                //config.instance.staticData.components = config.components.Where(x => x.componentInstance is IConfigComponentStatic).Select(x => (IConfigComponentStatic)x.componentInstance).ToArray();
                if (res == true) {
                    config.instance.Validate();
                    EditorUtility.SetDirty(config.instance);
                }
            }

        }

        private static bool Apply<T>(ref T[] source, T[] arr) {

            if (source == null) {
                source = arr;
                return true;
            }

            if (source.Length != arr.Length) {
                source = arr;
                return true;
            }

            for (int i = 0; i < source.Length; ++i) {
                var src = source[i];
                var target = arr[i];
                if (src.GetType() != target.GetType() || CompareStructs(target, src) == false) {
                    source = arr;
                    return true;
                }
            }
            
            return false;

        }

        private static bool CompareStructs(object t1, object t2) {
            var m = typeof(EntityConfigCsvImporterEditor).GetMethod(nameof(CompareStructsGen), System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            return (bool)m.MakeGenericMethod(t1.GetType(), t2.GetType()).Invoke(null, new object[] { t1, t2 });
        }

        private static unsafe bool CompareStructsGen<T0, T1>(T0 t1, T1 t2) where T0 : unmanaged where T1 : unmanaged {
            T0* p1 = &t1;
            T1* p2 = &t2;
            var size = sizeof(T0);
            return Unity.Collections.LowLevel.Unsafe.UnsafeUtility.MemCmp(p1, p2, size) == 0;
        }


        public class TempComponent : ScriptableObject {

            [SerializeReference]
            public object component;
            public EntityConfig config;

        }
        
    }

}