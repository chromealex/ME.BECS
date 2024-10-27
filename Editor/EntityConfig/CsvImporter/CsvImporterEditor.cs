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
                this.loadingIndicator.style.display = DisplayStyle.Flex;
                this.Load(strings, AssetDatabase.GetAssetPath(targetDir.objectReferenceValue), () => {
                    this.loadingButton.SetEnabled(true);
                    this.loadingIndicator.style.display = DisplayStyle.None;
                });
            }) { text = "Load" };
            button.AddToClassList("load-button");
            this.loadingButton = button;
            root.Add(button);

            this.loadingIndicator = new Label();
            this.loadingIndicator.AddToClassList("loader");
            root.Add(this.loadingIndicator);

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
        
        public void Load(string[] urls, string targetDir, System.Action callback) {

            this.result.RemoveFromClassList("hide");
            this.result.RemoveFromClassList("success");
            this.result.RemoveFromClassList("failed");
            
            var list = new scg::List<UnityEngine.Networking.UnityWebRequest>();
            foreach (var url in urls) {

                var www = UnityEngine.Networking.UnityWebRequest.Get(url);
                www.SendWebRequest();
                list.Add(www);

            }

            UnityEditor.EditorApplication.delayCall += OnDelayCall;
            return;

            void OnDelayCall() {
                var allDone = true;
                foreach (var item in list) {
                    if (item.isDone == false) {
                        allDone = false;
                        break;
                    }
                }

                this.loadingIndicator.text = this.GetLoaderText();
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
                        var configFiles = this.ParseConfigs(csv, targetDir, out var ver);
                        configsPerItem.Add(configFiles);
                        configs.AddRange(configFiles);
                        versions.Add(ver);
                        Debug.Log($"Configs with version {ver} has been updated");
                    }
                    
                    for (var index = 0; index < data.Count; ++index) {
                        this.ParseBaseConfigs(configs, configsPerItem[index], csvs[index]);
                    }

                    foreach (var config in configs) {
                        CreateConfig(config);
                    }

                    for (var index = 0; index < data.Count; ++index) {
                        this.Parse(configs, configsPerItem[index], csvs[index]);
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
            if (System.IO.File.Exists(config.fullPath) == false) {
                configInstance = EntityConfig.CreateInstance<EntityConfig>();
                AssetDatabase.CreateAsset(configInstance, config.fullPath);
                AssetDatabase.ImportAsset(config.fullPath);
            } else {
                configInstance = AssetDatabase.LoadAssetAtPath<EntityConfig>(config.fullPath);
            }
            configInstance.collectionsData = new EntityConfig.CollectionsData();
            config.instance = configInstance;
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

            public string name;
            public string path;
            public string fullPath;
            public int baseConfig;
            public scg::List<Component> components;
            public scg::List<Aspect> aspects;
            public EntityConfig instance;

            public ConfigFile() { }

            public ConfigFile(string targetDir, string data) {
                this.name = data;
                this.path = System.IO.Path.Combine(targetDir, data);
                this.fullPath = $"{this.path}.asset";
                EditorUtils.CreateDirectoriesByPath(this.fullPath);
                this.baseConfig = -1;
                this.components = new scg::List<Component>();
                this.aspects = new scg::List<Aspect>();
            }

        }

        public scg::List<ConfigFile> ParseConfigs(scg::List<string[]> csv, string targetDir, out string version) {
            version = csv[0][0];
            var configFiles = new scg::List<ConfigFile>();
            var offset = 2;
            var line = csv[0];
            {
                // Read config names
                for (int j = offset; j < line.Length; ++j) {
                    var configName = line[j];
                    configFiles.Add(new ConfigFile(targetDir, configName));
                }
            }
            return configFiles;
        }

        public void ParseBaseConfigs(scg::List<ConfigFile> allConfigs, scg::List<ConfigFile> configFiles, scg::List<string[]> csv) {
            var offset = 2;
            var line = csv[1];
            {
                // Read base configs
                for (int j = offset; j < line.Length; ++j) {
                    var configName = line[j];
                    if (string.IsNullOrEmpty(configName) == false) {
                        var idx = allConfigs.FindIndex(x => x.path.EndsWith(configName));
                        var configIdx = j - offset;
                        var config = configFiles[configIdx];
                        config.baseConfig = idx;
                    }
                }
            }
        }

        public void Parse(scg::List<ConfigFile> allConfigs, scg::List<ConfigFile> configFiles, scg::List<string[]> csv) {
            var offset = 2;
            var components = TypeCache.GetTypesDerivedFrom<IComponent>().Where(x => EditorUtils.IsValidTypeForAssembly(false, x)).ToArray();
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

                    var isAspect = typeof(IAspect).IsAssignableFrom(componentType);
                    for (int j = offset; j < line.Length; ++j) {
                        var configIdx = j - offset;
                        var fieldName = line[1];
                        var value = line[j];
                        if (string.IsNullOrEmpty(value) == true) continue;
                        var configFile = configFiles[configIdx];
                        if (isAspect == true) {
                            var aspect = configFile.aspects.FirstOrDefault(x => x.type == componentType);
                            if (aspect == null) {
                                aspect = new ConfigFile.Aspect() {
                                    name = componentName,
                                    type = componentType,
                                };
                                configFile.aspects.Add(aspect);
                            }
                        } else {
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

                            component.fields.Add(fieldName, value);
                        }
                    }
                }
            }
        }

        public static void Link(scg::List<ConfigFile> configFiles, bool updateComponents = true) {
            
            var temp = TempComponent.CreateInstance<TempComponent>();
            
            foreach (var config in configFiles) {
                config.instance.aspects.components = config.aspects.Select(x => (IAspect)System.Activator.CreateInstance(x.type)).ToArray();
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
                                component.managedReferenceValue = System.Activator.CreateInstance(fieldType.GenericTypeArguments[0]);

                            } else {
                                component = component.FindPropertyRelative(key);
                            }
                        }

                        if (component == null) {
                            Debug.LogWarning($"Key not found {field.Key} in component {comp.type.FullName}");
                            continue;
                        }

                        {
                            component.serializedObject.ApplyModifiedProperties();
                            component.serializedObject.Update();
                            //var fieldInfo = comp.GetField(field.Key);
                            //var type = fieldInfo.FieldType;
                            PropertyEditorUtils.GetTargetObjectOfProperty(component, out var type);
                            var serializer = JSON.JsonUtils.GetSerializer(type);
                            if (serializer != null) {
                                var obj = serializer.FromString(type, field.Value);
                                component.boxedValue = obj;
                                component.serializedObject.ApplyModifiedProperties();
                            } else {
                                Debug.LogWarning($"Serializer was not found for type {comp.type.FullName}");
                            }
                        }
                    }

                    comp.componentInstance = temp.component;
                }
            }
            TempComponent.DestroyImmediate(temp);

            if (updateComponents == true) {
                foreach (var config in configFiles) {
                    if (config.baseConfig >= 0) config.instance.baseConfig = configFiles[config.baseConfig].instance;
                    config.instance.data.components = config.components.Where(x => x.componentInstance is IConfigComponent).Select(x => (IConfigComponent)x.componentInstance).ToArray();
                    config.instance.sharedData = new ComponentsStorage<IConfigComponentShared>() { isShared = true };
                    config.instance.sharedData.components = config.components.Where(x => x.componentInstance is IConfigComponentShared).Select(x => (IConfigComponentShared)x.componentInstance).ToArray();
                    config.instance.staticData.components = config.components.Where(x => x.componentInstance is IConfigComponentStatic).Select(x => (IConfigComponentStatic)x.componentInstance).ToArray();
                    config.instance.OnValidate();
                    EditorUtility.SetDirty(config.instance);
                }
            }
            
        }

        public class TempComponent : ScriptableObject {

            [SerializeReference]
            public object component;
            public EntityConfig config;

        }
        
    }

}