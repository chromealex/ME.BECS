#if BECS_REMOTE_DEBUG && !UNITY_WEBGL
using System;
using scg = System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using UnityEngine;

namespace ME.BECS.RemoteDebug {
    // Uses BECS managed debug readers; never writes ECS data or exposes allocation pointers.
    internal static class RemoteEntityInspector {
        [Serializable] internal sealed class Snapshot {
            public uint id;
            public ushort generation, world;
            public string name, staticError;
            public bool alive, truncated;
            public scg.List<ComponentData> components = new();
        }
        [Serializable] internal sealed class ComponentData {
            public string name, kind, error;
            public bool enabled = true, tag;
            public scg.List<FieldData> fields = new();
        }
        [Serializable] internal sealed class FieldData {
            public string name, type, value;
            public bool entityReference;
            public uint entityId;
            public string entityVersion;
            public ushort entityWorld, entityGeneration;
        }
        private sealed class Readers {
            public MethodInfo has, read, tag, enabled;
        }
        private static readonly scg.Dictionary<Type, Readers> readers = new();
        private static readonly scg.Dictionary<Type, FieldInfo[]> fields = new();

        internal static Snapshot Read(Ent entity) {
            var result = new Snapshot { id = entity.id, generation = entity.gen, world = entity.worldId, alive = entity.IsAlive() };
            if (!result.alive) return result;
            result.name = entity.EditorName.ToString();
            AddGroup(result, entity, StaticTypesLoadedManaged.loadedTypes, "Component", typeof(IComponent));
            AddGroup(result, entity, StaticTypesLoadedManaged.loadedSharedTypes, "Shared", typeof(IComponentShared));
            AddStaticGroup(result, entity);
            result.components.Sort((a, b) => string.Compare(a.kind + a.name, b.kind + b.name, StringComparison.Ordinal));
            return result;
        }
        private static void AddStaticGroup(Snapshot result, Ent entity) {
            // HasStaticDirect reads EntityConfigComponent.EntityConfig, whose getter throws
            // for an absent/unregistered config before it can report a missing static type.
            if (!entity.Has<EntityConfigComponent>()) return;
            var configId = entity.Read<EntityConfigComponent>().id;
            if (configId == 0) return;
            try {
                var config = EntityConfigsRegistry.GetUnsafeEntityConfigBySourceId(configId);
                if (!config.IsValid()) {
                    result.staticError = "Static components unavailable: config #" + configId + " is not registered";
                    return;
                }
            } catch (System.Exception exception) {
                result.staticError = "Static components unavailable: " + exception.Message;
                return;
            }
            AddGroup(result, entity, StaticTypesLoadedManaged.loadedStaticTypes, "Static", typeof(IConfigComponentStatic));
        }
        private static Readers GetReaders(Type type, string kind) {
            if (readers.TryGetValue(type, out var cached)) return cached;
            var suffix = kind == "Shared" ? "SharedDirect" : kind == "Static" ? "StaticDirect" : "Direct";
            cached = new Readers {
                has = typeof(Components).GetMethod("Has" + suffix).MakeGenericMethod(type),
                read = typeof(Components).GetMethod("Read" + suffix).MakeGenericMethod(type),
                tag = typeof(Components).GetMethod(nameof(Components.IsTagDirect)).MakeGenericMethod(type),
                enabled = kind == "Component" ? typeof(Components).GetMethod(nameof(Components.IsEnabledDirect)).MakeGenericMethod(type) : null,
            };
            readers.Add(type, cached);
            return cached;
        }
        private static void AddGroup(Snapshot result, Ent entity, scg.Dictionary<uint, Type> types, string kind, Type contract) {
            foreach (var pair in types) {
                var type = pair.Value;
                if (!contract.IsAssignableFrom(type) || (kind == "Component" && typeof(IComponentShared).IsAssignableFrom(type))) continue;
                if (result.components.Count >= 256) { result.truncated = true; return; }
                ComponentData component = null;
                try {
                    var access = GetReaders(type, kind);
                    var args = new object[] { entity };
                    if (!(bool)access.has.Invoke(null, args)) continue;
                    component = new ComponentData { name = type.FullName ?? type.Name, kind = kind, tag = (bool)access.tag.Invoke(null, null) };
                    result.components.Add(component);
                    if (access.enabled != null) component.enabled = (bool)access.enabled.Invoke(null, args);
                    if (!component.tag) Describe(component.fields, "", access.read.Invoke(null, args), type, 0);
                } catch (System.Exception exception) {
                    component ??= new ComponentData { name = type.FullName ?? type.Name, kind = kind };
                    if (!result.components.Contains(component)) result.components.Add(component);
                    component.error = (exception.InnerException ?? exception).Message;
                }
            }
        }
        private static void Add(scg.List<FieldData> output, string path, Type type, string value) {
            if (output.Count < 512) output.Add(new FieldData { name = string.IsNullOrEmpty(path) ? "value" : path, type = type.Name, value = value });
        }
        private static void Describe(scg.List<FieldData> output, string path, object value, Type type, int depth) {
            if (output.Count >= 512) return;
            if (value == null) { Add(output, path, type, "null"); return; }
            if (type.IsPointer || type == typeof(IntPtr) || type == typeof(UIntPtr)) { Add(output, path, type, "Pointer (not exposed)"); return; }
            if (value is Ent ent) {
                var version = ent.IsAlive() ? ent.Version.ToString(CultureInfo.InvariantCulture) : "—";
                output.Add(new FieldData { name = string.IsNullOrEmpty(path) ? "value" : path, type = type.Name,
                    value = "#" + ent.id + " · Gen " + ent.gen + " · Version " + version + " · world #" + ent.worldId,
                    entityReference = true, entityVersion = version, entityId = ent.id, entityWorld = ent.worldId, entityGeneration = ent.gen });
                return;
            }
            if (value is bbool boolean) {
                Add(output, path, type, ((bool)boolean).ToString()); return;
            }
            if (type.IsPrimitive || type.IsEnum || value is string || value is decimal || type.Name == "sfloat" || type.Name == "usec" || type.Name.StartsWith("FixedString", StringComparison.Ordinal)) {
                Add(output, path, type, Convert.ToString(value, CultureInfo.InvariantCulture)); return;
            }
            if (depth >= 6) { Add(output, path, type, "Nested value (depth limit)"); return; }
            if (value is IUnmanagedList list) {
                Add(output, path + ".Count", typeof(uint), list.ElementsCount.ToString(CultureInfo.InvariantCulture));
                if (!list.IsCreated || list.ElementsCount == 0) return;
                if (list.ElementsCount > 128) { Add(output, path, type, "Collection contents omitted (more than 128 elements)"); return; }
                var items = list.ToManagedArray();
                for (int i = 0; i < items.Length; ++i) Describe(output, path + "[" + i + "]", items[i], items[i]?.GetType() ?? typeof(object), depth + 1);
                return;
            }
            if (value is Array array) {
                Add(output, path + ".Length", typeof(int), array.Length.ToString(CultureInfo.InvariantCulture));
                for (int i = 0; i < System.Math.Min(array.Length, 128); ++i) Describe(output, path + "[" + i + "]", array.GetValue(i), type.GetElementType(), depth + 1);
                return;
            }
            if (!type.IsValueType) { Add(output, path, type, "Object reference"); return; }
            if (!fields.TryGetValue(type, out var members)) {
                var visible = new scg.List<FieldInfo>();
                foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)) {
                    if (!field.IsStatic && (field.IsPublic || field.IsDefined(typeof(SerializeField), false))) visible.Add(field);
                }
                members = visible.ToArray(); fields.Add(type, members);
            }
            if (members.Length == 0) { Add(output, path, type, "No serialized fields"); return; }
            foreach (var field in members) {
                var fieldPath = string.IsNullOrEmpty(path) ? field.Name : path + "." + field.Name;
                if (field.FieldType.IsPointer) { Add(output, fieldPath, field.FieldType, "Pointer (not exposed)"); continue; }
                Describe(output, fieldPath, field.GetValue(value), field.FieldType, depth + 1);
            }
        }
    }
}
#endif
