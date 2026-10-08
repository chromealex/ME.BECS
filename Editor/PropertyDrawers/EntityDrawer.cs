using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using static ME.BECS.Cuts;

namespace ME.BECS.Editor {

    /// <summary>
    /// Draws entity values in the Unity Inspector.
    /// </summary>
    [CustomPropertyDrawer(typeof(Ent))]
    public unsafe class EntityDrawer : PropertyDrawer {
        // A controller belongs to a visual tree, not to the cached PropertyDrawer instance.
        private View view;
        private static WorldEntityEditorWindow.TempObject selectedEntity;
        private static void SelectEntity(Ent entity) {
            if (selectedEntity == null) {
                selectedEntity = ScriptableObject.CreateInstance<WorldEntityEditorWindow.TempObject>();
                // NotEditable disables the entire Unity inspector, including its foldouts.
                selectedEntity.hideFlags = HideFlags.HideAndDontSave & ~HideFlags.NotEditable;
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
        /// <summary>
        /// Replay mode resolver used by <c>EntityDrawer</c>.
        /// </summary>
        public static Func<World, bool> ReplayModeResolver { get; set; }
        /// <summary>
        /// Tests whether the context can edit components.
        /// </summary>
        public static bool CanEditComponents(Ent entity) {
            if (entity.IsEmpty() || !entity.World.isCreated || !entity.IsAlive()) return false;
            return CanEditWorld(entity.World);
        }
        /// <summary>
        /// Tests whether the context can edit world.
        /// </summary>
        public static bool CanEditWorld(World world) {
            return world.isCreated && (world.state.ptr->Mode != WorldMode.Logic || ReplayModeResolver?.Invoke(world) == true);
        }
        /// <summary>
        /// Builds the UI Toolkit editor for the supplied serialized property.
        /// </summary>
        public override VisualElement CreatePropertyGUI(SerializedProperty property) {
            var controller = new View(property);
            this.view = controller;
            return controller.root;
        }
        /// <summary>
        /// Sets foldout state.
        /// </summary>
        public void SetFoldoutState(bool value) { this.view?.SetExpanded(value); }
        /// <summary>
        /// Updates entity drawer using the current inputs and execution context.
        /// </summary>
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
        /// <summary>
        /// Processes structure copy.
        /// </summary>
        public static bool StructCopy<T>(T a, T b) where T : unmanaged {
            return _memcmp(_address(ref a), _address(ref b), TSize<T>.size) == 0;
        }
        /// <summary>
        /// Compares two boxed structures using their serialized field values.
        /// </summary>
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
                    EditorUIUtils.ApplyComponentGroupColor(this.element, type);
                    owner.BindTooltip(this.element, FieldTooltip.Get(type));
                    return;
                }
                this.foldout = new Foldout { text = EditorUtils.GetComponentName(type), value = owner.IsExpanded(type, shared) };
                this.element = this.foldout;
                this.element.AddToClassList("config-component-row");
                this.element.AddToClassList("config-component-foldout");
                EditorUIUtils.ApplyComponentGroupColor(this.element, type);
                owner.BindTooltip(this.foldout.Q<Toggle>(), FieldTooltip.Get(type));
                this.foldout.RegisterValueChangedCallback(evt => {
                    if (evt.target != this.element) return;
                    owner.RememberExpanded(type, shared, evt.newValue);
                    if (evt.newValue) this.Refresh();
                });
            }
            public void Refresh() {
                if (this.disposed || !this.owner.Alive || !this.access.has(this.owner.entity)) return;
                var disabled = this.access.enabled(this.owner.entity) == false;
                this.element.EnableInClassList("runtime-component-disabled", disabled);
                var title = EditorUtils.GetComponentName(this.type) + (disabled == true ? " (Disabled)" : string.Empty);
                if (this.foldout != null) {
                    this.foldout.text = title;
                } else {
                    ((Label)this.element).text = title;
                }
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
                        this.buffer.hideFlags = HideFlags.HideAndDontSave & ~HideFlags.NotEditable;
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
                    var fieldInfo = this.type.GetField(field.name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    var hasAttribute = fieldInfo != null && (Attribute.IsDefined(fieldInfo, typeof(ME.BECS.TooltipAttribute)) == true || Attribute.IsDefined(fieldInfo, typeof(UnityEngine.TooltipAttribute)) == true);
                    var tooltip = hasAttribute == false && string.IsNullOrEmpty(field.tooltip) == true ? FieldTooltip.Get(fieldInfo) : null;
                    if (string.IsNullOrEmpty(tooltip) == false) {
                        var hint = new VisualElement();
                        hint.AddToClassList("runtime-field-tooltip");
                        EditorUIUtils.DrawTooltip(hint, tooltip);
                        row.Add(hint);
                    }
                    this.owner.BindTooltip(row, FieldTooltip.Get(fieldInfo, field.tooltip));
                    row.Add(control); this.element.Add(row);
                    control.BindProperty(field);
                    control.RegisterCallback<SerializedPropertyChangeEvent>(evt => this.Commit(evt.changedProperty));
                } while (iterator.NextVisible(false));
            }
            private void ApplyEditability(PropertyField control) {
                // A disabled ancestor also disables every nested foldout. Reset containers first,
                // then lock only leaf value fields, preserving component/property navigation.
                control.SetEnabled(true);
                this.foldout?.SetEnabled(true);
                var inputs = control.Query<VisualElement>(className: "unity-base-field").ToList();
                foreach (var input in inputs) input.SetEnabled(true);
                foreach (var input in inputs) {
                    var foldout = input.GetFirstAncestorOfType<Foldout>();
                    if (input is Foldout || input.Q<Foldout>() != null || input.ClassListContains("unity-foldout__toggle") == true || (foldout != null && foldout.Q<Toggle>() == input)) continue;
                    // Vectors, lists and other compound fields must remain enabled so their
                    // structural controls work; their individual value fields are handled below.
                    if (input.Children().Any(child => child.ClassListContains("unity-base-field") == true || child.Q(className: "unity-base-field") != null) == true) continue;
                    if (input.Q(className: "runtime-entity-inspector") != null) continue;
                    var navigation = input;
                    var nestedInspector = false;
                    while (navigation != null && navigation != control) {
                        if (navigation.ClassListContains("runtime-entity-navigation") == true || navigation.ClassListContains("runtime-entity-inspector") == true) {
                            nestedInspector = true;
                            break;
                        }
                        navigation = navigation.parent;
                    }
                    if (nestedInspector == false) {
                        input.EnableInClassList("runtime-readonly-field", this.owner.Editable == false);
                        input.SetEnabled(this.owner.Editable);
                    }
                }
                control.Query<Button>().ForEach(button => {
                    var parent = button.parent;
                    while (parent != null && parent != control) {
                        if (parent.ClassListContains("runtime-entity-navigation") == true || parent.ClassListContains("runtime-entity-inspector") == true) return;
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
                    this.mergeBuffer.hideFlags = HideFlags.HideAndDontSave & ~HideFlags.NotEditable;
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
            private readonly Label metadata, reference;
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
                var identity = new VisualElement();
                identity.AddToClassList("runtime-entity-identity");
                this.reference = new Label(this.caption);
                this.reference.AddToClassList("runtime-entity-reference");
                identity.Add(this.reference);
                navigation.Add(identity);
                this.inlineButton = new Button(() => { this.SetExpanded(!this.header.value); this.Refresh(); }) { text = "Show Inline" };
                this.selectButton = new Button(() => { if (this.Alive) SelectEntity(this.entity); }) { text = "Select Entity" };
                navigation.Add(this.inlineButton); navigation.Add(this.selectButton);
                this.root.Add(this.header);
                this.header.style.display = this.header.value ? DisplayStyle.Flex : DisplayStyle.None;
                this.metadata = new Label(); this.metadata.AddToClassList("runtime-entity-metadata");
                identity.Add(this.metadata);
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
                EditorUIUtils.ApplyCommonStyles(this.root);
                this.root.EnableInClassList("config-light", !EditorGUIUtility.isProSkin);
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
                var name = alive ? ent.EditorName.ToString() : string.Empty;
                this.reference.text = string.IsNullOrEmpty(name) ? this.caption : name;
                this.header.text = this.reference.text;
                var world = ent.World;
                var worldName = world.isCreated ? world.Name.ToString() : "Unavailable world";
                this.metadata.text = ent.IsEmpty() ? "Entity is empty" :
                    $"ID {ent.id}  ·  Gen {ent.gen}  ·  {worldName} (#{ent.worldId})" +
                    (alive ? $"  ·  v{ent.Version}" + (ent.IsActive() == false ? "  ·  Disabled" : "") + (this.Editable ? "" : "  ·  Read only") : "  ·  Not alive");
                this.reference.tooltip = this.reference.text + "\n" + this.metadata.text;
                if (!alive) { this.ClearRows(); return; }
                if (!this.header.value) return;
                this.types.Clear();
                ref var componentsLock = ref world.state.ptr->entities.GetEntityComponentsLock(world.state, ent.id);
                componentsLock.Lock();
                try {
                    var iterator = world.state.ptr->entities.GetEntityComponentsEnumerator(world.state, ent.id);
                    while (iterator.MoveNext()) if (StaticTypesLoadedManaged.loadedTypes.TryGetValue(iterator.Current, out var type)) this.types.Add(type);
                } finally { componentsLock.Unlock(); }
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
                this.ShowTooltip(decorator, text);
            }
            public void BindTooltip(VisualElement anchor, string text) {
                if (anchor == null || string.IsNullOrEmpty(text) == true) return;
                anchor.pickingMode = PickingMode.Position;
                anchor.RegisterCallback<PointerOverEvent>(evt => { this.ShowTooltip(anchor, text); evt.StopPropagation(); });
                anchor.RegisterCallback<PointerLeaveEvent>(evt => this.HideTooltip());
                anchor.RegisterCallback<TooltipEvent>(evt => evt.StopImmediatePropagation());
            }
            private void ShowTooltip(VisualElement decorator, string text) {
                if (this.root.panel == null) return;
                this.HideTooltip();
                var overlay = this.root.panel.visualTree;
                this.popup = new Label(text) { pickingMode = PickingMode.Ignore, enableRichText = true };
                // The panel root does not inherit the inspector's text font.
                this.popup.style.unityFont = decorator.resolvedStyle.unityFont;
                this.popup.style.unityFontDefinition = decorator.resolvedStyle.unityFontDefinition;
                this.popup.AddToClassList("config-tooltip-popup");
                this.popup.EnableInClassList("config-tooltip-light", !EditorGUIUtility.isProSkin);
                EditorUIUtils.ApplyCommonStyles(this.popup);
                this.popup.styleSheets.Add(EditorUtils.LoadResource<StyleSheet>(Themes.CurrentTheme));
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
