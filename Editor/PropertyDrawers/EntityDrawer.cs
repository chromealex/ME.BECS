using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using static ME.BECS.Cuts;

namespace ME.BECS.Editor {

    [CustomPropertyDrawer(typeof(Ent))]
    public unsafe class EntityDrawer : PropertyDrawer {
        // A controller belongs to a visual tree, not to the cached PropertyDrawer instance.
        private View view;
        private static WorldEntityEditorWindow.TempObject selectedEntity;
        private static void SelectEntity(Ent entity) {
            if (selectedEntity == null) {
                selectedEntity = ScriptableObject.CreateInstance<WorldEntityEditorWindow.TempObject>();
                selectedEntity.hideFlags = HideFlags.HideAndDontSave;
                AssemblyReloadEvents.beforeAssemblyReload -= ReleaseSelection;
                AssemblyReloadEvents.beforeAssemblyReload += ReleaseSelection;
            }
            selectedEntity.entity = entity;
            Selection.activeObject = selectedEntity;
        }
        private static void ReleaseSelection() {
            if (selectedEntity != null) UnityEngine.Object.DestroyImmediate(selectedEntity);
            selectedEntity = null;
        }
        // Optional network addon supplies replay state without a runtime Network dependency.
        public static Func<World, bool> ReplayModeResolver { get; set; }
        public static bool CanEditComponents(Ent entity) {
            if (entity.IsEmpty() || !entity.World.isCreated || !entity.IsAlive()) return false;
            return CanEditWorld(entity.World);
        }
        public static bool CanEditWorld(World world) {
            return world.isCreated && (world.state.ptr->Mode != WorldMode.Logic || ReplayModeResolver?.Invoke(world) == true);
        }
        public override VisualElement CreatePropertyGUI(SerializedProperty property) {
            var controller = new View(property);
            this.view = controller;
            return controller.root;
        }
        public void SetFoldoutState(bool value) { this.view?.SetExpanded(value); }
        public void OnUpdate() { this.view?.Refresh(); }

        private sealed class Access {
            public bool tag;
            public Func<Ent, bool> has, enabled;
            public Func<Ent, object> read;
            public Action<Ent, object> write;
            public Func<object, object, bool> equal;
            public Func<object, object> clone;
        }
        private static readonly Dictionary<Type, Access> normalAccess = new Dictionary<Type, Access>();
        private static readonly Dictionary<Type, Access> sharedAccess = new Dictionary<Type, Access>();
        private static Access GetAccess(Type type, bool shared) {
            var cache = shared ? sharedAccess : normalAccess;
            if (cache.TryGetValue(type, out var access)) return access;
            var factory = typeof(EntityDrawer).GetMethod(shared ? nameof(SharedAccess) : nameof(NormalAccess), BindingFlags.NonPublic | BindingFlags.Static);
            access = (Access)factory.MakeGenericMethod(type).Invoke(null, null);
            cache.Add(type, access);
            return access;
        }
        private static Access NormalAccess<T>() where T : unmanaged, IComponent {
            return new Access { tag = StaticTypes<T>.isTag, has = Components.HasDirect<T>, enabled = Components.HasDirectEnabled<T>,
                read = ent => Components.ReadDirect<T>(ent), write = (ent, value) => Components.SetDirect(ent, (T)value),
                equal = (a, b) => StructCopy((T)a, (T)b), clone = value => (T)value };
        }
        private static Access SharedAccess<T>() where T : unmanaged, IComponentShared {
            return new Access { tag = StaticTypes<T>.isTag, has = Components.HasSharedDirect<T>, enabled = Components.HasSharedDirect<T>,
                read = ent => Components.ReadSharedDirect<T>(ent), write = (ent, value) => Components.SetSharedDirect(ent, (T)value),
                equal = (a, b) => StructCopy((T)a, (T)b), clone = value => (T)value };
        }
        public static bool StructCopy<T>(T a, T b) where T : unmanaged {
            return _memcmp(_address(ref a), _address(ref b), TSize<T>.size) == 0;
        }
        public static bool StructsAreEqual(object a, object b) {
            if (a == null || b == null) return a == b;
            if (a.GetType() != b.GetType()) return false;
            var method = typeof(EntityDrawer).GetMethod(nameof(StructCopy)).MakeGenericMethod(a.GetType());
            return (bool)method.Invoke(null, new[] { a, b });
        }

        private sealed class Row : IDisposable {
            public readonly Type type;
            public readonly Access access;
            public readonly VisualElement element;
            private readonly Foldout foldout;
            private readonly View owner;
            private TempObject buffer, mergeBuffer;
            private SerializedObject serialized, mergeSerialized;
            private object snapshot;
            private bool syncing, built, disposed;
            private bool? editable;
            private readonly System.Collections.Generic.List<PropertyField> controls = new System.Collections.Generic.List<PropertyField>();
            public Row(View owner, Type type, bool shared) {
                this.owner = owner; this.type = type; this.access = GetAccess(type, shared);
                if (this.access.tag) {
                    this.element = new Label(EditorUtils.GetComponentName(type));
                    this.element.AddToClassList("config-component-row");
                    this.element.AddToClassList("config-tag-row");
                    return;
                }
                this.foldout = new Foldout { text = EditorUtils.GetComponentName(type), value = owner.IsExpanded(type, shared) };
                this.element = this.foldout;
                this.element.AddToClassList("config-component-row");
                this.element.AddToClassList("config-component-foldout");
                this.foldout.RegisterValueChangedCallback(evt => {
                    if (evt.target != this.element) return;
                    owner.RememberExpanded(type, shared, evt.newValue);
                    if (evt.newValue) this.Refresh();
                });
            }
            public void Refresh() {
                if (this.disposed || !this.owner.Alive || !this.access.has(this.owner.entity)) return;
                this.element.EnableInClassList("runtime-component-disabled", !this.access.enabled(this.owner.entity));
                if (this.editable != this.owner.Editable) {
                    this.editable = this.owner.Editable;
                    foreach (var control in this.controls) this.ApplyEditability(control);
                }
                if (this.access.tag || !this.foldout.value || this.element.resolvedStyle.display == DisplayStyle.None) return;
                // Keep a user's in-progress text intact; other components continue updating.
                var focus = this.element.panel?.focusController.focusedElement as VisualElement;
                if (focus != null && this.foldout.contentContainer.Contains(focus)) return;
                var value = this.access.read(this.owner.entity);
                if (this.snapshot != null && this.access.equal(this.snapshot, value)) return;
                this.syncing = true;
                try {
                    if (this.buffer == null) {
                        this.buffer = ScriptableObject.CreateInstance<TempObject>();
                        this.buffer.hideFlags = HideFlags.HideAndDontSave;
                        this.buffer.data = new[] { value };
                        this.serialized = new SerializedObject(this.buffer);
                    } else {
                        this.buffer.data[0] = value;
                        this.serialized.Update();
                    }
                    this.snapshot = this.access.clone(value);
                    if (!this.built) this.BuildFields();
                } finally { this.syncing = false; }
            }
            private void BuildFields() {
                this.built = true;
                var component = this.serialized.FindProperty("data").GetArrayElementAtIndex(0);
                var iterator = component.Copy();
                var end = iterator.GetEndProperty();
                if (!iterator.NextVisible(true)) return;
                do {
                    if (SerializedProperty.EqualContents(iterator, end) || iterator.depth <= component.depth) break;
                    var field = iterator.Copy();
                    var row = new VisualElement(); row.AddToClassList("config-field-row");
                    var control = new PropertyField(field);
                    this.controls.Add(control);
                    control.RegisterCallback<GeometryChangedEvent>(evt => this.ApplyEditability(control));
                    control.RegisterCallback<AttachToPanelEvent>(evt => control.schedule.Execute(() => this.ApplyEditability(control)));
                    row.Add(control); this.element.Add(row);
                    control.BindProperty(field);
                    control.RegisterCallback<SerializedPropertyChangeEvent>(evt => this.Commit(evt.changedProperty));
                } while (iterator.NextVisible(false));
            }
            private void ApplyEditability(PropertyField control) {
                // Disable value inputs, never their containers: foldouts and entity navigation stay usable.
                control.Query<VisualElement>(className: "unity-base-field").ForEach(input => {
                    var foldout = input.GetFirstAncestorOfType<Foldout>();
                    if (input is Foldout || input.Q<Foldout>() != null || input.ClassListContains("unity-foldout__toggle") || (foldout != null && foldout.Q<Toggle>() == input)) {
                        input.SetEnabled(true);
                        return;
                    }
                    if (input.Q(className: "runtime-entity-inspector") != null) { input.SetEnabled(true); return; }
                    var navigation = input;
                    while (navigation != null && navigation != control) {
                        if (navigation.ClassListContains("runtime-entity-navigation") || navigation.ClassListContains("runtime-entity-inspector")) return;
                        navigation = navigation.parent;
                    }
                    input.SetEnabled(this.owner.Editable);
                });
                control.Query<Button>().ForEach(button => {
                    var parent = button.parent;
                    while (parent != null && parent != control) {
                        if (parent.ClassListContains("runtime-entity-navigation") || parent.ClassListContains("runtime-entity-inspector")) return;
                        parent = parent.parent;
                    }
                    button.SetEnabled(this.owner.Editable);
                });
            }
            private void Commit(SerializedProperty changed) {
                if (this.disposed || this.syncing || changed == null || changed.serializedObject.targetObject != this.buffer || !CanEditComponents(this.owner.entity) || !this.access.has(this.owner.entity)) return;
                var edited = this.serialized.FindProperty("data").GetArrayElementAtIndex(0).managedReferenceValue;
                // Binding notifications from a runtime refresh are not user edits.
                if (edited == null || this.snapshot == null || this.access.equal(edited, this.snapshot)) return;
                const string prefix = "data.Array.data[0].";
                if (!changed.propertyPath.StartsWith(prefix, StringComparison.Ordinal)) return;
                if (this.mergeBuffer == null) {
                    this.mergeBuffer = ScriptableObject.CreateInstance<TempObject>();
                    this.mergeBuffer.hideFlags = HideFlags.HideAndDontSave;
                    this.mergeBuffer.data = new object[1];
                    this.mergeSerialized = new SerializedObject(this.mergeBuffer);
                }
                // Merge only the changed property into freshly read runtime data.
                this.mergeBuffer.data[0] = this.access.read(this.owner.entity);
                this.mergeSerialized.Update();
                this.mergeSerialized.CopyFromSerializedProperty(changed);
                this.mergeSerialized.ApplyModifiedPropertiesWithoutUndo();
                if (CanEditComponents(this.owner.entity) && this.access.has(this.owner.entity)) this.access.write(this.owner.entity, this.mergeBuffer.data[0]);
                this.snapshot = this.access.clone(edited);
            }
            public void Dispose() {
                this.disposed = true;
                this.element.Unbind(); this.element.RemoveFromHierarchy();
                this.serialized?.Dispose(); this.mergeSerialized?.Dispose();
                if (this.buffer != null) UnityEngine.Object.DestroyImmediate(this.buffer);
                if (this.mergeBuffer != null) UnityEngine.Object.DestroyImmediate(this.mergeBuffer);
                this.buffer = null; this.mergeBuffer = null;
            }
        }

        private sealed class View {
            public readonly VisualElement root = new VisualElement();
            public Ent entity;
            public bool Editable { get; private set; }
            public bool Alive => !this.entity.IsEmpty() && this.entity.World.isCreated && this.entity.IsAlive();
            private SerializedObject source;
            private readonly UnityEngine.Object[] sourceTargets;
            private readonly string path, caption;
            private readonly Foldout header, journal;
            private readonly Button inlineButton, selectButton;
            private readonly Label metadata;
            private readonly VisualElement normal, shared, journalContent;
            private readonly Label normalTitle, sharedTitle;
            private readonly Dictionary<Type, Row> normalRows = new Dictionary<Type, Row>();
            private readonly Dictionary<Type, Row> sharedRows = new Dictionary<Type, Row>();
            private readonly Dictionary<string, bool> expanded = new Dictionary<string, bool>();
            private readonly System.Collections.Generic.HashSet<Type> present = new System.Collections.Generic.HashSet<Type>();
            private readonly System.Collections.Generic.List<Type> types = new System.Collections.Generic.List<Type>();
            private readonly System.Collections.Generic.List<Type> removed = new System.Collections.Generic.List<Type>();
            private IVisualElementScheduledItem timer;
            private string search = "";
            private JournalEditorWindow.VisualElementData[] journalItems;
            private double nextJournal;
            private Label popup;
            public View(SerializedProperty property) {
                this.sourceTargets = property.serializedObject.targetObjects;
                this.source = new SerializedObject(this.sourceTargets);
                this.path = property.propertyPath; this.caption = property.displayName;
                this.root.AddToClassList("compact-config-inspector");
                this.root.AddToClassList("runtime-entity-inspector");
                this.ApplyTheme();
                this.header = new Foldout { text = this.caption, value = EditorPrefs.GetBool("ME.BECS.Foldouts.Entity." + this.path, false) };
                var navigation = new VisualElement();
                navigation.AddToClassList("runtime-entity-navigation");
                this.root.Add(navigation);
                var reference = new Label(this.caption);
                reference.AddToClassList("runtime-entity-reference");
                navigation.Add(reference);
                this.inlineButton = new Button(() => { this.SetExpanded(!this.header.value); this.Refresh(); }) { text = "Show Inline" };
                this.selectButton = new Button(() => { if (this.Alive) SelectEntity(this.entity); }) { text = "Select Entity" };
                navigation.Add(this.inlineButton); navigation.Add(this.selectButton);
                this.root.Add(this.header);
                this.header.style.display = this.header.value ? DisplayStyle.Flex : DisplayStyle.None;
                this.metadata = new Label(); this.metadata.AddToClassList("runtime-entity-metadata");
                this.header.Add(this.metadata);
                var searchField = new ToolbarSearchField(); searchField.AddToClassList("config-search");
                this.header.Add(searchField);
                searchField.RegisterValueChangedCallback(evt => { this.search = evt.newValue.Trim(); this.Filter(); });
                this.normal = this.Section("Components", out this.normalTitle);
                this.shared = this.Section("Shared Components", out this.sharedTitle);
                this.journal = new Foldout { text = "Journal", value = false };
                this.journal.AddToClassList("config-component-foldout"); this.header.Add(this.journal);
                this.journalContent = new VisualElement(); this.journal.Add(this.journalContent);
                this.journal.styleSheets.Add(EditorUtils.LoadResource<StyleSheet>("ME.BECS.Resources/Styles/Journal.uss"));
                this.header.RegisterValueChangedCallback(evt => { if (evt.target == this.header) { this.SetExpanded(evt.newValue); this.Refresh(); } });
                this.root.RegisterCallback<AttachToPanelEvent>(evt => {
                    if (this.source == null && this.HasSourceTargets()) this.source = new SerializedObject(this.sourceTargets);
                    Themes.Changed -= this.ApplyTheme; Themes.Changed += this.ApplyTheme;
                    this.timer?.Pause(); this.timer = this.root.schedule.Execute(this.Refresh).Every(100);
                    this.Refresh();
                });
                this.root.RegisterCallback<DetachFromPanelEvent>(evt => {
                    this.StopUpdates();
                });
                this.root.RegisterCallback<PointerOverEvent>(this.Tooltip);
                this.root.RegisterCallback<PointerOutEvent>(evt => this.HideTooltip());
                this.root.RegisterCallback<WheelEvent>(evt => this.HideTooltip());
                this.root.RegisterCallback<TooltipEvent>(evt => { if (this.popup != null) evt.StopImmediatePropagation(); });
            }
            private void ApplyTheme() {
                this.root.styleSheets.Clear();
                this.root.styleSheets.Add(EditorUtils.LoadResource<StyleSheet>(Themes.CurrentTheme));
                this.root.styleSheets.Add(EditorUtils.LoadResource<StyleSheet>("ME.BECS.Resources/Styles/EntityConfigCompact.uss"));
            }
            private VisualElement Section(string name, out Label title) {
                var section = new VisualElement(); section.AddToClassList("entity-components"); this.header.Add(section);
                title = new Label(name + " · 0"); title.AddToClassList("entity-components-label"); section.Add(title);
                var list = new VisualElement(); list.AddToClassList("fields-container"); section.Add(list); return list;
            }
            public bool IsExpanded(Type type, bool shared) => this.expanded.TryGetValue((shared ? "s:" : "c:") + type.FullName, out var value) && value;
            public void RememberExpanded(Type type, bool shared, bool value) { this.expanded[(shared ? "s:" : "c:") + type.FullName] = value; }
            public void SetExpanded(bool value) { this.header.SetValueWithoutNotify(value); this.header.style.display = value ? DisplayStyle.Flex : DisplayStyle.None; this.inlineButton.text = value ? "Hide Inline" : "Show Inline"; EditorPrefs.SetBool("ME.BECS.Foldouts.Entity." + this.path, value); }
            private bool HasSourceTargets() {
                foreach (var target in this.sourceTargets) if (target == null) return false;
                return this.sourceTargets.Length > 0;
            }
            private void StopUpdates() {
                this.timer?.Pause();
                Themes.Changed -= this.ApplyTheme;
                this.HideTooltip(); this.ClearRows();
                this.entity = default;
                var oldSource = this.source; this.source = null;
                oldSource?.Dispose();
            }
            public void Refresh() {
                if (this.source == null) return;
                if (!this.HasSourceTargets()) { this.StopUpdates(); return; }
                this.source.Update();
                var property = this.source.FindProperty(this.path);
                if (property == null) { this.StopUpdates(); return; }
                var value = PropertyEditorUtils.GetTargetObjectOfProperty(property);
                if (value is not Ent ent) return;
                if (ent.ToULong() != this.entity.ToULong()) { this.ClearRows(); this.entity = ent; this.nextJournal = 0; }
                var alive = this.Alive;
                this.Editable = CanEditComponents(this.entity);
                this.selectButton.SetEnabled(alive);
                this.inlineButton.SetEnabled(alive);
                this.inlineButton.text = this.header.value ? "Hide Inline" : "Show Inline";
                this.header.text = alive && !string.IsNullOrEmpty(this.entity.EditorName.ToString()) ? this.entity.EditorName.ToString() : this.caption;
                this.metadata.text = alive ? $"ID {ent.id}  ·  Gen {ent.gen}  ·  World {ent.worldId}  ·  Version {ent.Version}" + (this.Editable ? "" : "  ·  Read only") : (ent.IsEmpty() ? "Entity is empty" : "Entity is not alive");
                if (!alive) { this.ClearRows(); return; }
                if (!this.header.value) return;
                this.types.Clear();
                var world = ent.World;
                #if ENABLE_BECS_FLAT_QUERIES
                ref var componentsLock = ref world.state.ptr->entities.GetEntityComponentsLock(world.state, ent.id);
                componentsLock.Lock();
                try {
                    var iterator = world.state.ptr->entities.GetEntityComponentsEnumerator(world.state, ent.id);
                    while (iterator.MoveNext()) if (StaticTypesLoadedManaged.loadedTypes.TryGetValue(iterator.Current, out var type)) this.types.Add(type);
                } finally { componentsLock.Unlock(); }
                #else
                var archId = world.state.ptr->archetypes.entToArchetypeIdx[world.state.ptr->allocator, ent.id];
                var arch = world.state.ptr->archetypes.list[world.state.ptr->allocator, archId];
                var iterator = arch.components.GetEnumerator(world);
                while (iterator.MoveNext()) if (StaticTypesLoadedManaged.loadedTypes.TryGetValue(iterator.Current, out var type)) this.types.Add(type);
                #endif
                this.Reconcile(this.normalRows, this.normal, this.normalTitle, false);
                this.types.Clear();
                foreach (var pair in StaticTypesLoadedManaged.loadedSharedTypes) if (GetAccess(pair.Value, true).has(ent)) this.types.Add(pair.Value);
                this.Reconcile(this.sharedRows, this.shared, this.sharedTitle, true);
                if (this.journal.value && EditorApplication.timeSinceStartup >= this.nextJournal) {
                    this.nextJournal = EditorApplication.timeSinceStartup + 0.5;
                    this.journalContent.Clear();
                    JournalEditorWindow.DrawEntityJournal(this.journalContent, ref this.journalItems, ent);
                }
            }
            private void Reconcile(Dictionary<Type, Row> rows, VisualElement list, Label title, bool shared) {
                this.present.Clear(); foreach (var type in this.types) this.present.Add(type);
                this.removed.Clear(); foreach (var pair in rows) if (!this.present.Contains(pair.Key)) this.removed.Add(pair.Key);
                foreach (var type in this.removed) { rows[type].Dispose(); rows.Remove(type); }
                var added = false;
                foreach (var type in this.types) {
                    if (rows.ContainsKey(type)) continue;
                    var row = new Row(this, type, shared); rows.Add(type, row); list.Add(row.element); added = true;
                }
                if (added) {
                    this.types.Sort((a, b) => string.Compare(EditorUtils.GetComponentName(a), EditorUtils.GetComponentName(b), StringComparison.Ordinal));
                    for (var i = 0; i < this.types.Count; ++i) { var element = rows[this.types[i]].element; if (list.IndexOf(element) != i) list.Insert(i, element); }
                    this.Filter();
                }
                title.text = (shared ? "Shared Components" : "Components") + " · " + rows.Count;
                foreach (var pair in rows) pair.Value.Refresh();
            }
            private void Filter() {
                this.Filter(this.normalRows); this.Filter(this.sharedRows);
            }
            private void Filter(Dictionary<Type, Row> rows) {
                foreach (var pair in rows) {
                    var matches = string.IsNullOrEmpty(this.search) || EditorUtils.GetComponentName(pair.Key).IndexOf(this.search, StringComparison.OrdinalIgnoreCase) >= 0 || pair.Key.FullName.IndexOf(this.search, StringComparison.OrdinalIgnoreCase) >= 0;
                    pair.Value.element.style.display = matches ? DisplayStyle.Flex : DisplayStyle.None;
                }
            }
            private void ClearRows() {
                foreach (var pair in this.normalRows) pair.Value.Dispose(); this.normalRows.Clear();
                foreach (var pair in this.sharedRows) pair.Value.Dispose(); this.sharedRows.Clear();
                this.normalTitle.text = "Components · 0"; this.sharedTitle.text = "Shared Components · 0";
                this.journalContent.Clear(); this.journalItems = null;
            }
            private void Tooltip(PointerOverEvent evt) {
                var target = evt.target as VisualElement;
                var decorator = target;
                while (decorator != null && !decorator.ClassListContains("has-tooltip")) decorator = decorator.parent;
                var text = decorator?.Q<Label>(className: "tooltip-text")?.text;
                if (string.IsNullOrEmpty(text) || this.root.panel == null) return;
                this.HideTooltip();
                var overlay = this.root.panel.visualTree;
                this.popup = new Label(text) { pickingMode = PickingMode.Ignore, enableRichText = true };
                this.popup.AddToClassList("config-tooltip-popup");
                this.popup.styleSheets.Add(EditorUtils.LoadResource<StyleSheet>("ME.BECS.Resources/Styles/EntityConfigCompact.uss"));
                this.popup.style.width = Mathf.Min(360, overlay.worldBound.width - 16);
                var position = overlay.WorldToLocal(decorator.worldBound.position);
                this.popup.style.left = Mathf.Max(8, position.x);
                this.popup.style.bottom = overlay.worldBound.height - position.y + 4;
                overlay.Add(this.popup); this.popup.BringToFront();
            }
            private void HideTooltip() { this.popup?.RemoveFromHierarchy(); this.popup = null; }
        }
    }
}
