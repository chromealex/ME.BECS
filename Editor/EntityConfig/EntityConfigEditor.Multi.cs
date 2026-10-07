using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace ME.BECS.Editor {

    public partial class EntityConfigEditor {

        private static readonly string[] MultiStorages = { "data", "staticData", "sharedData" };
        private EntityConfig[] multiSources;
        private EntityConfig[] multiProxies;
        private SerializedObject multiSerializedObject;
        private EntityConfig multiBaseline;
        private readonly Dictionary<string, Type[]> commonTypes = new();
        private bool committingMulti;

        private void ReleaseMultiObjects() {
            this.multiSerializedObject?.Dispose();
            this.multiSerializedObject = null;
            if (this.multiProxies != null) {
                foreach (var proxy in this.multiProxies) {
                    if (proxy == null) continue;
                    Undo.ClearUndo(proxy);
                    UnityEngine.Object.DestroyImmediate(proxy);
                }
            }
            this.DestroyBaseline();
            this.multiProxies = null;
            this.multiSources = null;
            this.commonTypes.Clear();
        }

        private static Dictionary<Type, System.Collections.Generic.List<int>> GetComponentIndices(SerializedObject owner, string storage) {
            var result = new Dictionary<Type, System.Collections.Generic.List<int>>();
            var components = owner.FindProperty(storage).FindPropertyRelative("components");
            for (var i = 0; i < components.arraySize; ++i) {
                var type = EditorUtils.GetTypeFromPropertyField(components.GetArrayElementAtIndex(i).managedReferenceFullTypename);
                if (type == null) continue;
                if (!result.TryGetValue(type, out var indices)) result.Add(type, indices = new System.Collections.Generic.List<int>());
                indices.Add(i);
            }
            return result;
        }

        private SerializedObject MultiOwner(EntityConfig config) {
            if (!this.baseSerializedObjects.TryGetValue(config, out var owner)) {
                owner = new SerializedObject(config);
                this.baseSerializedObjects.Add(config, owner);
            }
            owner.Update();
            return owner;
        }

        private void DrawMultiComponents(VisualElement root) {
            this.multiSources = this.targets.Cast<EntityConfig>().ToArray();
            var owners = this.multiSources.Select(this.MultiOwner).ToArray();
            this.multiProxies = this.multiSources.Select(source => {
                var proxy = UnityEngine.Object.Instantiate(source);
                proxy.hideFlags = UnityEngine.HideFlags.HideAndDontSave;
                return proxy;
            }).ToArray();
            foreach (var storage in MultiStorages) {
                var indices = owners.Select(owner => GetComponentIndices(owner, storage)).ToArray();
                var common = indices[0].Keys.Where(type => indices.All(map => map.TryGetValue(type, out var entries) && entries.Count == 1))
                    .OrderBy(type => type.FullName, StringComparer.Ordinal).ToArray();
                this.commonTypes.Add(storage, common);
                for (var i = 0; i < this.multiProxies.Length; ++i) {
                    using var proxyOwner = new SerializedObject(this.multiProxies[i]);
                    var proxyStorage = proxyOwner.FindProperty(storage);
                    var components = proxyStorage.FindPropertyRelative("components");
                    var masks = proxyStorage.FindPropertyRelative("masks");
                    // Snapshot before resizing: matching is by type, never by original array position.
                    var values = common.Select(type => components.GetArrayElementAtIndex(indices[i][type][0]).managedReferenceValue).ToArray();
                    var sourceMasks = proxyStorage.FindPropertyRelative("masks");
                    var bits = common.Select(type => {
                        var index = indices[i][type][0];
                        if (index >= sourceMasks.arraySize) return new bool[type.GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public).Length];
                        var mask = sourceMasks.GetArrayElementAtIndex(index).FindPropertyRelative("mask");
                        return Enumerable.Range(0, mask.arraySize).Select(j => mask.GetArrayElementAtIndex(j).boolValue).ToArray();
                    }).ToArray();
                    components.arraySize = common.Length;
                    masks.arraySize = common.Length;
                    for (var j = 0; j < common.Length; ++j) {
                        components.GetArrayElementAtIndex(j).managedReferenceValue = values[j];
                        var mask = masks.GetArrayElementAtIndex(j).FindPropertyRelative("mask");
                        mask.arraySize = bits[j].Length;
                        for (var k = 0; k < bits[j].Length; ++k) mask.GetArrayElementAtIndex(k).boolValue = bits[j][k];
                    }
                    proxyOwner.ApplyModifiedPropertiesWithoutUndo();
                }
            }
            this.RefreshBaseline();
            this.multiSerializedObject = new SerializedObject(this.multiProxies);
            var content = new VisualElement();
            content.AddToClassList("config-content");
            root.Add(content);
            var toolbar = new VisualElement();
            toolbar.AddToClassList("config-toolbar");
            content.Add(toolbar);
            toolbar.Add(new Label($"Selected configs: {this.multiSources.Length}"));
            var baseConfig = new ObjectField("Base Config") { objectType = typeof(EntityConfig), allowSceneObjects = false };
            var baseProperty = this.serializedObject.FindProperty("baseConfig");
            baseConfig.SetValueWithoutNotify(baseProperty.objectReferenceValue);
            baseConfig.showMixedValue = baseProperty.hasMultipleDifferentValues;
            baseConfig.RegisterValueChangedCallback(evt => {
                if (evt.target != baseConfig) return;
                var visited = new System.Collections.Generic.HashSet<EntityConfig>(this.multiSources);
                for (var config = evt.newValue as EntityConfig; config != null; config = config.baseConfig) {
                    if (visited.Add(config)) continue;
                    baseConfig.SetValueWithoutNotify(evt.previousValue);
                    EditorUtility.DisplayDialog("Base Config", "This reference would create a Base Config cycle.", "OK");
                    return;
                }
                this.serializedObject.Update();
                this.serializedObject.FindProperty("baseConfig").objectReferenceValue = evt.newValue;
                this.serializedObject.ApplyModifiedProperties();
                this.needSync = true;
                EditorApplication.delayCall += this.Update;
                root.schedule.Execute(this.RebuildInspector);
            });
            toolbar.Add(baseConfig);
            var search = new TextField();
            search.textEdition.placeholder = "Search components or fields…";
            search.SetValueWithoutNotify(this.searchText);
            search.AddToClassList("config-search");
            search.RegisterValueChangedCallback(evt => { this.searchText = evt.newValue; this.ApplySearch(); });
            var maskProperty = this.serializedObject.FindProperty("maskable");
            var maskable = new Toggle("Maskable Config") { showMixedValue = maskProperty.hasMultipleDifferentValues };
            maskable.SetValueWithoutNotify(maskProperty.boolValue);
            maskable.RegisterValueChangedCallback(evt => {
                if (evt.target != maskable) return;
                this.serializedObject.Update();
                this.serializedObject.FindProperty("maskable").boolValue = evt.newValue;
                this.serializedObject.ApplyModifiedProperties();
                this.needSync = true;
                EditorApplication.delayCall += this.Update;
                root.schedule.Execute(this.RebuildInspector);
            });
            toolbar.Add(maskable);
            content.Add(search);
            for (var s = 0; s < MultiStorages.Length; ++s) {
                var storage = MultiStorages[s];
                var section = new VisualElement();
                section.AddToClassList("entity-components");
                content.Add(section);
                var title = new Label(new[] { "Config Components", "Static Components", "Shared Components" }[s]);
                title.AddToClassList("entity-components-label");
                title.userData = title.text;
                section.Add(title);
                var shared = this.commonTypes[storage];
                if (shared.Length > 0) section.Add(new Label($"Common · {shared.Length}") { name = "common-heading" });
                var components = this.multiSerializedObject.FindProperty(storage).FindPropertyRelative("components");
                var masks = this.multiSerializedObject.FindProperty(storage).FindPropertyRelative("masks");
                for (var i = 0; i < shared.Length; ++i) {
                    var row = this.DrawComponent(components.GetArrayElementAtIndex(i).Copy(), shared[i], EditorUtils.GetComponentName(shared[i]),
                        masks.GetArrayElementAtIndex(i).FindPropertyRelative("mask"), s == 0 && this.multiSources.Any(config => config.maskable));
                    row.AddToClassList("config-common-row");
                    section.Add(row);
                }
                var indices = owners.Select(owner => GetComponentIndices(owner, storage)).ToArray();
                var different = indices.SelectMany(map => map.Keys).Distinct().Except(shared).OrderBy(type => type.FullName, StringComparer.Ordinal).ToArray();
                if (different.Length > 0) section.Add(new Label($"Different · {different.Length}") { name = "different-heading" });
                foreach (var type in different) {
                    var present = indices.Count(map => map.ContainsKey(type));
                    var group = new Foldout { text = $"{EditorUtils.GetComponentName(type)} · {present} / {owners.Length}", value = false };
                    group.AddToClassList("config-difference-group");
                    if (indices.Any(map => map.TryGetValue(type, out var entries) && entries.Count > 1)) {
                        group.Add(new HelpBox("Repeated component type: instances are edited separately because matching is ambiguous.", HelpBoxMessageType.Info));
                    }
                    section.Add(group);
                    for (var i = 0; i < owners.Length; ++i) {
                        if (!indices[i].TryGetValue(type, out var entries)) continue;
                        var source = this.multiSources[i];
                        foreach (var index in entries) {
                            var component = owners[i].FindProperty(storage).FindPropertyRelative("components").GetArrayElementAtIndex(index).Copy();
                            var row = this.DrawComponent(component, type, source.name + (entries.Count > 1 ? $" · instance {index + 1}" : ""), null, false);
                            row.AddToClassList("config-owner-row");
                            row.Q<Toggle>()?.Q<Label>()?.AddToClassList("config-owner-name");
                            row.tooltip = source.name;
                            group.Add(row);
                        }
                    }
                    if (present < owners.Length) {
                        var add = new Button(() => {
                            var menu = new GenericMenu();
                            for (var i = 0; i < owners.Length; ++i) {
                                if (!indices[i].TryGetValue(type, out var entries)) continue;
                                foreach (var index in entries) {
                                    var source = this.multiSources[i];
                                    var sourceIndex = index;
                                    menu.AddItem(new UnityEngine.GUIContent(source.name + (entries.Count > 1 ? $" / instance {index + 1}" : "")), false,
                                        () => this.AddMissingComponents(storage, type, source, sourceIndex));
                                }
                            }
                            menu.ShowAsContext();
                        }) { text = $"+ Add to {owners.Length - present} configs", tooltip = "Choose a config to copy this component from" };
                        add.AddToClassList("add-button");
                        var actions = new VisualElement();
                        actions.AddToClassList("config-difference-actions");
                        actions.Add(add);
                        group.Add(actions);
                    }
                }
            }
            this.DrawMultiInherited(content);
            this.ApplySearch();
        }

        private void DrawMultiInherited(VisualElement content) {
            if (!this.multiSources.Any(config => config.baseConfig != null)) return;
            var inherited = new Foldout { text = "Inherited components · per config" };
            content.Add(inherited);
            foreach (var config in this.multiSources) {
                if (config.baseConfig == null) continue;
                var configGroup = new Foldout { text = config.name };
                inherited.Add(configGroup);
                var visited = new System.Collections.Generic.HashSet<EntityConfig> { config };
                for (var parent = config.baseConfig; parent != null && visited.Add(parent); parent = parent.baseConfig) {
                    var owner = this.MultiOwner(parent);
                    foreach (var storage in MultiStorages) {
                        var data = owner.FindProperty(storage);
                        var components = data.FindPropertyRelative("components");
                        for (var i = 0; i < components.arraySize; ++i) {
                            var component = components.GetArrayElementAtIndex(i).Copy();
                            var type = EditorUtils.GetTypeFromPropertyField(component.managedReferenceFullTypename);
                            if (type == null) continue;
                            var masks = data.FindPropertyRelative("masks");
                            var mask = i < masks.arraySize ? masks.GetArrayElementAtIndex(i).FindPropertyRelative("mask") : null;
                            var row = this.DrawComponent(component, type, EditorUtils.GetComponentName(type), mask, storage == "data" && parent.maskable, parent.name);
                            row.AddToClassList("config-inherited-row");
                            configGroup.Add(row);
                        }
                    }
                }
            }
        }

        private void DestroyBaseline() {
            if (this.multiBaseline != null) UnityEngine.Object.DestroyImmediate(this.multiBaseline);
            this.multiBaseline = null;
        }

        private void RefreshBaseline() {
            this.DestroyBaseline();
            this.multiBaseline = UnityEngine.Object.Instantiate(this.multiProxies[0]);
            this.multiBaseline.hideFlags = UnityEngine.HideFlags.HideAndDontSave;
        }

        // Unity resolves [SerializeReference] children of a multi-object edit through the FIRST
        // target's managed reference ids. When configs list components in different orders the
        // other proxies resolve the same path to another component (or none), so their copy of
        // the edit is missing or lands on the wrong slot. Only the first proxy is reliable: take
        // the fields that changed there (against a baseline) and apply them by type to every
        // config, keeping their other values; then resync all proxies from the configs.
        private void CommitCommonComponent(SerializedObject owner, string componentPath) {
            if (this.committingMulti || owner != this.multiSerializedObject || this.multiSources == null) return;
            var storage = componentPath.Substring(0, componentPath.IndexOf('.'));
            var bracket = componentPath.LastIndexOf('[');
            var proxyIndex = int.Parse(componentPath.Substring(bracket + 1, componentPath.Length - bracket - 2), System.Globalization.CultureInfo.InvariantCulture);
            var type = this.commonTypes[storage][proxyIndex];
            this.committingMulti = true;
            try {
                Undo.SetCurrentGroupName("Edit Entity Config Components");
                var changed = new System.Collections.Generic.List<string>();
                using (var editedOwner = new SerializedObject(this.multiProxies[0]))
                using (var baselineOwner = new SerializedObject(this.multiBaseline)) {
                    CollectChanged(editedOwner.FindProperty(componentPath), baselineOwner.FindProperty(componentPath), changed);
                    for (var i = 0; i < this.multiSources.Length; ++i) {
                        using var sourceOwner = new SerializedObject(this.multiSources[i]);
                        var indices = GetComponentIndices(sourceOwner, storage);
                        if (!indices.TryGetValue(type, out var entries) || entries.Count != 1) continue;
                        using var proxyOwner = new SerializedObject(this.multiProxies[i]);
                        var destination = sourceOwner.FindProperty(storage);
                        var proxy = proxyOwner.FindProperty(storage);
                        var target = destination.FindPropertyRelative("components").GetArrayElementAtIndex(entries[0]);
                        var edited = editedOwner.FindProperty(componentPath);
                        foreach (var relative in changed) {
                            var from = edited.FindPropertyRelative(relative);
                            var to = target.FindPropertyRelative(relative);
                            if (from != null && to != null) CopyValue(from, to);
                        }
                        // Masks are plain arrays (no managed references): multi-edit reaches every proxy.
                        var masks = destination.FindPropertyRelative("masks");
                        if (masks.arraySize < destination.FindPropertyRelative("components").arraySize) masks.arraySize = destination.FindPropertyRelative("components").arraySize;
                        var targetMask = masks.GetArrayElementAtIndex(entries[0]).FindPropertyRelative("mask");
                        var sourceMask = proxy.FindPropertyRelative("masks").GetArrayElementAtIndex(proxyIndex).FindPropertyRelative("mask");
                        targetMask.arraySize = sourceMask.arraySize;
                        for (var k = 0; k < sourceMask.arraySize; ++k) targetMask.GetArrayElementAtIndex(k).boolValue = sourceMask.GetArrayElementAtIndex(k).boolValue;
                        sourceOwner.CopyFromSerializedProperty(proxyOwner.FindProperty("collectionsData"));
                        sourceOwner.ApplyModifiedProperties();
                    }
                }
                // Resync only after a real field edit: the refreshed bindings raise change
                // events again, and an unchanged commit must not start another round.
                if (changed.Count > 0) {
                    this.SyncProxies(storage);
                    this.RefreshBaseline();
                    this.multiSerializedObject.Update();
                }
            } finally { this.committingMulti = false; }
        }

        private void SyncProxies(string storage) {
            var common = this.commonTypes[storage];
            for (var i = 0; i < this.multiSources.Length; ++i) {
                using var sourceOwner = new SerializedObject(this.multiSources[i]);
                using var proxyOwner = new SerializedObject(this.multiProxies[i]);
                var indices = GetComponentIndices(sourceOwner, storage);
                var sources = sourceOwner.FindProperty(storage).FindPropertyRelative("components");
                var proxies = proxyOwner.FindProperty(storage).FindPropertyRelative("components");
                for (var j = 0; j < common.Length && j < proxies.arraySize; ++j) {
                    if (!indices.TryGetValue(common[j], out var entries) || entries.Count != 1) continue;
                    CopyValue(sources.GetArrayElementAtIndex(entries[0]), proxies.GetArrayElementAtIndex(j));
                }
                proxyOwner.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void CollectChanged(SerializedProperty edited, SerializedProperty baseline, System.Collections.Generic.List<string> changed) {
            if (edited == null) return;
            var prefix = edited.propertyPath.Length + 1;
            var it = edited.Copy();
            var end = edited.GetEndProperty();
            var enter = true;
            while (it.Next(enter) && !SerializedProperty.EqualContents(it, end)) {
                var relative = it.propertyPath.Substring(prefix);
                var other = baseline?.FindPropertyRelative(relative);
                if (other != null && SerializedProperty.DataEquals(it, other)) { enter = false; continue; }
                if (other != null && it.propertyType == SerializedPropertyType.Generic && !it.isArray && it.hasChildren) { enter = true; continue; }
                changed.Add(relative);
                enter = false;
            }
        }

        private static void CopyChildren(SerializedProperty from, SerializedProperty to) {
            var it = from.Copy();
            var end = from.GetEndProperty();
            if (!it.Next(true)) return;
            while (!SerializedProperty.EqualContents(it, end)) {
                var target = to.FindPropertyRelative(it.name);
                if (target != null) CopyValue(it, target);
                if (!it.Next(false)) break;
            }
        }

        private static void CopyValue(SerializedProperty from, SerializedProperty to) {
            switch (from.propertyType) {
                case SerializedPropertyType.ManagedReference:
                    var value = from.managedReferenceValue;
                    if (value != null && from.managedReferenceFullTypename == to.managedReferenceFullTypename) CopyChildren(from, to);
                    else to.managedReferenceValue = value == null ? null : UnityEngine.JsonUtility.FromJson(UnityEngine.JsonUtility.ToJson(value), value.GetType());
                    return;
                case SerializedPropertyType.Generic when from.isArray:
                    to.arraySize = from.arraySize;
                    for (var i = 0; i < from.arraySize; ++i) CopyValue(from.GetArrayElementAtIndex(i), to.GetArrayElementAtIndex(i));
                    return;
                case SerializedPropertyType.Generic:
                    CopyChildren(from, to);
                    return;
                default:
                    if (from.isArray && from.propertyType != SerializedPropertyType.String) {
                        to.arraySize = from.arraySize;
                        for (var i = 0; i < from.arraySize; ++i) CopyValue(from.GetArrayElementAtIndex(i), to.GetArrayElementAtIndex(i));
                        return;
                    }
                    to.boxedValue = from.boxedValue;
                    return;
            }
        }

        private void AddMissingComponents(string storage, Type type, EntityConfig source, int index) {
            using var sourceOwner = new SerializedObject(source);
            var sourceComponent = sourceOwner.FindProperty(storage).FindPropertyRelative("components").GetArrayElementAtIndex(index);
            var json = JSON.JsonUtils.ComponentToJSON(sourceComponent);
            foreach (var target in this.multiSources) {
                using var owner = new SerializedObject(target);
                if (GetComponentIndices(owner, storage).ContainsKey(type)) continue;
                var data = owner.FindProperty(storage);
                var components = data.FindPropertyRelative("components");
                AddComponent(owner, data, components, type);
                var component = owner.FindProperty(storage).FindPropertyRelative("components").GetArrayElementAtIndex(components.arraySize - 1);
                JSON.JsonUtils.JSONToComponent(json, component);
                var targetMasks = owner.FindProperty(storage).FindPropertyRelative("masks");
                var targetMask = targetMasks.GetArrayElementAtIndex(targetMasks.arraySize - 1).FindPropertyRelative("mask");
                var sourceMasks = sourceOwner.FindProperty(storage).FindPropertyRelative("masks");
                var sourceMask = index < sourceMasks.arraySize ? sourceMasks.GetArrayElementAtIndex(index).FindPropertyRelative("mask") : null;
                for (var k = 0; k < targetMask.arraySize; ++k) {
                    targetMask.GetArrayElementAtIndex(k).boolValue = !source.maskable || (sourceMask != null && k < sourceMask.arraySize && sourceMask.GetArrayElementAtIndex(k).boolValue);
                }
                owner.ApplyModifiedProperties();
            }
            this.needSync = true;
            EditorApplication.delayCall += this.Update;
            this.rootVisualElement.schedule.Execute(this.RebuildInspector);
        }
    }
}
