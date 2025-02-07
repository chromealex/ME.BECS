#if FIXED_POINT
using tfloat = sfloat;
using ME.BECS.FixedPoint;
using Bounds = ME.BECS.FixedPoint.AABB;
using Rect = ME.BECS.FixedPoint.Rect;
#else
using tfloat = System.Single;
using Unity.Mathematics;
using Bounds = UnityEngine.Bounds;
using Rect = UnityEngine.Rect;
#endif

using System.Linq;
using UnityEditor.UIElements;

namespace ME.BECS.Views.Editor {
    
    using UnityEditor;
    using UnityEngine.UIElements;
    using ME.BECS.Editor;
    using UnityEngine;

    [CustomPropertyDrawer(typeof(InstantiateAvatarViewComponent.AnimatorData))]
    public class AvatarAnimatorDataDrawer : PropertyDrawerWithDispose {

        private static StyleSheet styleSheetBase;
        private PreviewRenderUtility previewRenderUtility;
        private UnityEngine.AnimationClip previewRenderUtilityClip;
        private Vector2 mousePosition;
        private Transform selected;

        private void LoadStyle() {
            if (styleSheetBase == null) {
                styleSheetBase = ME.BECS.Editor.EditorUtils.LoadResource<StyleSheet>("ME.BECS.Resources/Styles/Avatar.uss");
            }
        }

        public override void OnEnable(SerializedProperty property) {
            
        }

        public override void OnDisable() {
            
        }

        public override void OnDestroy() {
            this.CleanUp();
        }

        private void CleanUp() {
            if (this.previewRenderUtility != null) this.previewRenderUtility.Cleanup();
            if (this.previewRenderUtilityClip != null) UnityEngine.Object.DestroyImmediate(this.previewRenderUtilityClip);
            this.previewRenderUtility = null;
            this.previewRenderUtilityClip = null;
        }

        private static string GetName(int index, UnityEngine.AnimationClip clip) {
            var name = string.Empty;
            if (AssetDatabase.IsMainAsset(clip) == true) {
                name = clip.name;
            } else {
                name = AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GetAssetPath(clip)).name;
            }
            return $"#{index}: {name}";
        }

        public class ClipSelection {

            public AnimationClip clip;

        }

        private static Material[] pointMaterials = new Material[100];
        private static Material[] pointSelectedMaterials = new Material[100];
        public override UnityEngine.UIElements.VisualElement CreateProperty(SerializedProperty property) {
            
            this.LoadStyle();
            var root = new VisualElement();
            root.styleSheets.Add(styleSheetBase);

            var animatorData = property;
            var view = animatorData.FindPropertyRelative(nameof(InstantiateAvatarViewComponent.animatorData.view));
            
            var avatarContainer = new VisualElement();
            var viewProp = new UnityEditor.UIElements.PropertyField(view);
            viewProp.BindProperty(view);
            viewProp.RegisterValueChangeCallback((pEvt) => {
                OnViewChanged();
            });
            
            root.Add(viewProp);
            root.Add(avatarContainer);
            
            var points = animatorData.FindPropertyRelative(nameof(InstantiateAvatarViewComponent.AnimatorData.points));
            var id = points.FindPropertyRelative("data").FindPropertyRelative("Length");
            id.uintValue = EditorUtils.GetEntityCollection(property.serializedObject, id.uintValue, out var collection, out var index);

            OnViewChanged();
            
            void OnViewChanged() {
                
                avatarContainer.Clear();

                var viewSource = view.FindPropertyRelative(nameof(View.viewSource));
                var entityView = ObjectReferenceRegistry.GetObjectBySourceId<EntityView>(
                    viewSource.FindPropertyRelative(nameof(InstantiateAvatarViewComponent.animatorData.view.viewSource.prefabId)).uintValue);
                if (entityView != null) {

                    var animatorViewModule = (AnimatorViewModule)entityView.viewModules.FirstOrDefault(x => x is AnimatorViewModule);
                    if (animatorViewModule != null && animatorViewModule.animator != null) {

                        VisualElement pointsContainer = null;
                        Slider progress = null;
                        ClipSelection selectedClip = new ClipSelection();
                        ME.BECS.Extensions.GraphProcessor.GridBackground grid = null;

                        var defaultMaterial = UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline?.defaultMaterial;
                        if (defaultMaterial == null) defaultMaterial = animatorViewModule.animator.GetComponentInChildren<Renderer>(true)?.sharedMaterial;

                        var pointMaterial = new Material(defaultMaterial);
                        pointMaterial.color = Color.green;
                        pointMaterial.SetColor("_EmissionColor", Color.Lerp(Color.yellow, Color.black, 0.7f));
                        var pointMaterialSelected = new Material(defaultMaterial);
                        pointMaterialSelected.color = Color.red;
                        pointMaterialSelected.SetColor("_EmissionColor", Color.Lerp(Color.yellow, Color.black, 0.7f));

                        var avatarPreview = new VisualElement();
                        avatarPreview.AddToClassList("no-preview");
                        var animator = animatorViewModule.animator;
                        var clips = animator.runtimeAnimatorController.animationClips.ToList();
                        var i = 0;
                        var list = clips.Select(x => GetName(++i, x)).ToList();
                        list.Insert(0, "<none>");
                        var dropdown = new DropdownField(list, 0, formatListItemCallback: (str) => {
                            var idx = list.FindIndex(x => x == str);
                            if (idx <= 0) return str;
                            return str + $" (Events: {this.GetPointsCount(animatorData, clips[idx - 1])})";
                        });
                        avatarPreview.Add(dropdown);

                        var info = new Label();
                        avatarPreview.Add(info);

                        avatarPreview.AddToClassList("avatar-preview");
                        var image = new Image();
                        image.AddToClassList("avatar-preview-image");
                        var progressManual = false;
                        tfloat progressValue = 0f;
                        {
                            var playContainer = new VisualElement();
                            playContainer.AddToClassList("play-container");
                            Button button = null;
                            button = new Button(() => {
                                progressManual = !progressManual;
                                if (progressManual == true) {
                                    button.text = "||";
                                } else {
                                    button.text = "\u25b6";
                                }
                            });
                            button.text = "\u25b6";
                            playContainer.Add(button);

                            var progressContainer = new VisualElement();
                            progressContainer.AddToClassList("progress-container");
                            playContainer.Add(progressContainer);

                            grid = new ME.BECS.Extensions.GraphProcessor.GridBackground();
                            grid.spacingY = 1000f;
                            grid.spacing = 1000f;
                            grid.offset = new Vector2(-500f, -500f);
                            grid.AddToClassList("grid");
                            progressContainer.Add(grid);

                            pointsContainer = new VisualElement();
                            pointsContainer.AddToClassList("points-container");
                            pointsContainer.pickingMode = PickingMode.Ignore;
                            grid.Add(pointsContainer);

                            progress = new Slider();
                            progress.AddManipulator(new ContextualMenuManipulator(menu => {
                                if (selectedClip.clip != null) {
                                    var point = this.GetPoint(animatorData, selectedClip.clip, progress.value / (selectedClip.clip.length * selectedClip.clip.frameRate),
                                                              out var idx);
                                    if (point != null) {
                                        menu.menu.AppendAction("Remove Event", (evt) => {
                                            animatorData.serializedObject.Update();
                                            this.RemovePoint(animatorData, idx, pointsContainer, selectedClip.clip);
                                            animatorData.serializedObject.ApplyModifiedProperties();
                                            animatorData.serializedObject.Update();
                                        });
                                    } else {
                                        menu.menu.AppendAction("Add Event", (evt) => {
                                            animatorData.serializedObject.Update();
                                            this.AddPoint(animatorData, pointsContainer, selectedClip.clip,
                                                          progress.value / (selectedClip.clip.length * selectedClip.clip.frameRate));
                                            animatorData.serializedObject.ApplyModifiedProperties();
                                            animatorData.serializedObject.Update();
                                        });
                                    }
                                }
                            }));
                            progress.RegisterValueChangedCallback(evt => {
                                progressManual = true;
                                if (progressManual == true) {
                                    button.text = "||";
                                } else {
                                    button.text = "\u25b6";
                                }

                                progress.SetValueWithoutNotify((float)(progressValue = math.round(evt.newValue)));
                            });
                            progressContainer.Add(progress);

                            image.Add(playContainer);
                        }
                        var imgui = new IMGUIContainer();
                        image.Add(imgui);
                        avatarPreview.Add(image);
                        avatarContainer.Add(avatarPreview);

                        GameObject instance = null;
                        AnimatorViewModule selectedModule = null;
                        System.Action<MouseDownEvent> onMouseDown = null;
                        System.Action<MouseMoveEvent> onMouseMove = null;
                        System.Action<MouseUpEvent> onMouseUp = null;
                        System.Action<WheelEvent> onMouseScroll = null;
                        dropdown.RegisterValueChangedCallback(x => {
                            var clipName = x.newValue;
                            var idx = list.IndexOf(clipName) - 1;
                            avatarPreview.AddToClassList("no-preview");
                            if (idx < 0) {
                                selectedClip.clip = null;
                                this.CleanUp();
                                info.text = string.Empty;
                                return;
                            }

                            var clip = clips[idx];
                            if (clip != null) {
                                avatarPreview.RemoveFromClassList("no-preview");
                                selectedClip.clip = clip;
                                ObjectReferenceRegistryUtils.Assign(clip, clip);
                                this.LoadPoints(animatorData, pointsContainer, clip);
                                info.text = $"Frame rate: {clip.frameRate}, Length: {clip.length}";
                                progress.lowValue = 0f;
                                progress.highValue = clip.length * clip.frameRate;
                                grid.offset = new Vector2(-grid.parent.layout.x, 500f);
                                grid.spacing = progress.worldBound.width / progress.highValue;
                                this.CleanUp();
                                var previewRenderUtility = new PreviewRenderUtility(true, false);
                                this.previewRenderUtility = previewRenderUtility;
                                previewRenderUtility.camera.fieldOfView = 60f;
                                previewRenderUtility.camera.allowHDR = false;
                                previewRenderUtility.camera.allowMSAA = false;
                                previewRenderUtility.camera.nearClipPlane = 0.01f;
                                previewRenderUtility.camera.farClipPlane = 1000f;
                                previewRenderUtility.ambientColor = new UnityEngine.Color(0.1f, 0.1f, 0.1f, 0.0f);
                                previewRenderUtility.lights[0].intensity = 1.4f;
                                previewRenderUtility.lights[0].transform.rotation = UnityEngine.Quaternion.Euler(40f, 40f, 0.0f);
                                previewRenderUtility.lights[1].intensity = 1.4f;
                                instance = this.previewRenderUtility.InstantiatePrefabInScene(entityView.gameObject);
                                instance.transform.position = UnityEngine.Vector3.zero;
                                instance.transform.rotation = UnityEngine.Quaternion.Euler(0f, 45f, 0f);
                                var bounds = new UnityEngine.Bounds();
                                var pointMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
                                var renderers = instance.GetComponentsInChildren<UnityEngine.Renderer>(true);
                                foreach (var ren in renderers) {
                                    bounds.Encapsulate(ren.bounds);
                                    ren.sharedMaterial = defaultMaterial;
                                }

                                var module = (AnimatorViewModule)instance.GetComponent<EntityView>().viewModules.FirstOrDefault(x => x is AnimatorViewModule);
                                selectedModule = module;
                                var colliders = instance.GetComponentsInChildren<Collider>(true);
                                foreach (var collider in colliders) {
                                    Object.DestroyImmediate(collider);
                                }

                                foreach (var item in module.points) {
                                    var box = item.gameObject.AddComponent<BoxCollider>();
                                    box.transform.localScale = new Vector3(0.2f, 0.2f, 0.2f);
                                    box.center = Vector3.zero;
                                    box.size = new Vector3(1f, 1f, 1f);
                                }

                                var anim = module.animator.gameObject.AddComponent<UnityEngine.Animation>();
                                previewRenderUtility.camera.transform.localPosition = new UnityEngine.Vector3(0f, bounds.size.y, -bounds.size.z * 2f);
                                previewRenderUtility.camera.transform.LookAt(instance.transform.position + new UnityEngine.Vector3(0f, bounds.size.y * 0.5f, 0f));
                                var clipCopy = UnityEngine.Object.Instantiate(clip);
                                this.previewRenderUtilityClip = clipCopy;
                                clipCopy.legacy = true;
                                anim.clip = clipCopy;
                                anim.Rewind();
                                anim.playAutomatically = true;
                                anim.AddClip(clipCopy, clipCopy.name);
                                anim.Play(clipCopy.name, UnityEngine.PlayMode.StopAll);
                                var time = EditorApplication.timeSinceStartup;
                                var mouseDown = false;
                                var scrollValue = 1f;
                                onMouseDown = (evt) => {
                                    if (this.previewRenderUtility == null) return;
                                    mouseDown = true;
                                };
                                onMouseMove = (evt) => {
                                    if (this.previewRenderUtility == null) return;
                                    this.mousePosition = evt.localMousePosition;
                                    if (mouseDown == true) {
                                        instance.transform.rotation = UnityEngine.Quaternion.Euler(0, instance.transform.rotation.eulerAngles.y - evt.mouseDelta.x, 0);
                                    }
                                };
                                onMouseUp = (evt) => { mouseDown = false; };
                                onMouseScroll = (evt) => {
                                    if (this.previewRenderUtility == null) return;
                                    scrollValue += evt.delta.y * UnityEngine.Time.deltaTime;
                                    scrollValue = UnityEngine.Mathf.Clamp(scrollValue, 0.1f, 2f);
                                    previewRenderUtility.camera.transform.localPosition =
                                        new UnityEngine.Vector3(0f, bounds.size.y * scrollValue, -bounds.size.z * 2f * scrollValue);
                                    previewRenderUtility.camera.transform.LookAt(instance.transform.position + new UnityEngine.Vector3(0f, bounds.size.y * 0.5f, 0f));
                                };
                                imgui.onGUIHandler = () => {
                                    if (this.previewRenderUtility == null) return;
                                    if (progress.worldBound.width == 0) return;
                                    grid.spacing = progress.worldBound.width / progress.highValue;
                                    var p = this.GetPoint(animatorData, selectedClip.clip, progress.value / (selectedClip.clip.length * selectedClip.clip.frameRate), out _);
                                    if (p != null) {
                                        for (var j = 0; j < module.points.Length; ++j) {
                                            var item = module.points[j];
                                            var isSelected = (this.selected == item);
                                            var isAssigned = this.HasPoint(animatorData, clip, (uint)j);
                                            Material mat = null;
                                            if (isAssigned == true) {
                                                mat = pointSelectedMaterials[j];
                                                if (mat == null) mat = pointSelectedMaterials[j] = new Material(pointMaterialSelected);
                                            } else {
                                                mat = pointMaterials[j];
                                                if (mat == null) mat = pointMaterials[j] = new Material(pointMaterial);
                                            }

                                            mat.DisableKeyword("_EMISSION");
                                            if (isSelected == true) {
                                                mat.EnableKeyword("_EMISSION");
                                            }

                                            this.previewRenderUtility.DrawMesh(pointMesh, item.position, item.localScale, item.rotation, mat, 0, default, default, false);
                                        }

                                        var viewportPos = new Vector3(this.mousePosition.x / image.worldBound.width, 1f - this.mousePosition.y / image.worldBound.height, 0f);
                                        var dist = float.MaxValue;
                                        var maxDistance = 0.1f * 0.1f;
                                        Transform selected = null;
                                        foreach (var point in module.points) {
                                            var pointView = this.previewRenderUtility.camera.WorldToViewportPoint(point.position);
                                            pointView.z = 0f;
                                            var d = (viewportPos - pointView).sqrMagnitude;
                                            if (d <= dist && d <= maxDistance) {
                                                dist = d;
                                                selected = point;
                                            }
                                        }

                                        this.selected = selected;
                                    } else {
                                        this.selected = null;
                                    }

                                    if (progressManual == false) {
                                        var dt = EditorApplication.timeSinceStartup - time;
                                        time = EditorApplication.timeSinceStartup;
                                        var n = anim[clipCopy.name].normalizedTime;
                                        n += (float)dt;
                                        n = UnityEngine.Mathf.Repeat(n, 1f);
                                        anim[clipCopy.name].normalizedTime = n;
                                        progress.SetValueWithoutNotify(n * (progress.highValue - progress.lowValue));
                                    } else {
                                        var n = (float)(progressValue / (progress.highValue - progress.lowValue));
                                        anim[clipCopy.name].normalizedTime = n;
                                        progress.SetValueWithoutNotify(n * (progress.highValue - progress.lowValue));
                                    }

                                    anim.Sample();
                                    previewRenderUtility.BeginPreview(new UnityEngine.Rect(0f, 0f, image.worldBound.width, image.worldBound.height), EditorStyles.label);
                                    previewRenderUtility.camera.Render();
                                    var tex = previewRenderUtility.EndPreview();
                                    image.image = tex;
                                    imgui.MarkDirtyRepaint();
                                };
                            }
                        });
                        image.RegisterCallback<ClickEvent>(evt => {
                            if (this.selected != null) {
                                animatorData.serializedObject.Update();
                                var prevPos = instance.transform.position;
                                var prevRot = instance.transform.rotation;
                                instance.transform.position = Vector3.zero;
                                instance.transform.rotation = Quaternion.identity;
                                this.SetPoint(animatorData, selectedClip.clip, System.Array.IndexOf(selectedModule.points, this.selected), this.selected);
                                instance.transform.position = prevPos;
                                instance.transform.rotation = prevRot;
                                animatorData.serializedObject.ApplyModifiedProperties();
                                animatorData.serializedObject.Update();
                            }
                        });
                        image.RegisterCallback<WheelEvent>(evt => { onMouseScroll?.Invoke(evt); });
                        image.RegisterCallback<MouseDownEvent>(evt => { onMouseDown?.Invoke(evt); });
                        image.RegisterCallback<MouseMoveEvent>(evt => { onMouseMove?.Invoke(evt); });
                        image.RegisterCallback<MouseUpEvent>(evt => { onMouseUp?.Invoke(evt); });

                    } else {
                        var err = new Label("AnimatorViewModule must to be added to an EntityView with an AnimatorController");
                        avatarContainer.Add(err);
                    }

                } else {
                    var err = new Label("EntityView must be assigned");
                    avatarContainer.Add(err);
                }

            }

            return root;

        }

        private void SetPoint(SerializedProperty animatorData, AnimationClip clip, int index, Transform target) {
            var pointsArr = animatorData.FindPropertyRelative("points");
            var id = ObjectReferenceRegistry.GetId(clip);
            var size = EditorUtils.GetArraySize(pointsArr);
            for (uint i = 0; i < size; ++i) {
                var prop = EditorUtils.GetArrayElementByIndex(pointsArr, i);
                var val = (InstantiateAvatarViewComponent.AnimationData)prop.managedReferenceValue;
                if (val.animationId == id) {
                    val.firePoint.id = (uint)(index + 1);
                    val.firePoint.position = (float3)target.position;
                    val.firePoint.rotation = (quaternion)target.rotation;
                    prop.managedReferenceValue = val;
                    return;
                }
            }
        }

        private bool HasPoint(SerializedProperty animatorData, AnimationClip clip, uint index) {
            var pointsArr = animatorData.FindPropertyRelative("points");
            var id = ObjectReferenceRegistry.GetId(clip);
            var size = EditorUtils.GetArraySize(pointsArr);
            for (uint i = 0; i < size; ++i) {
                var prop = EditorUtils.GetArrayElementByIndex(pointsArr, i);
                var val = (InstantiateAvatarViewComponent.AnimationData)prop.managedReferenceValue;
                if (val.animationId == id && val.firePoint.id == index + 1u) {
                    return true;
                }
            }
            return false;
        }

        private uint GetPointsCount(SerializedProperty animatorData, AnimationClip clip) {
            var count = 0u;
            var pointsArr = animatorData.FindPropertyRelative("points");
            var id = ObjectReferenceRegistry.GetId(clip);
            var size = EditorUtils.GetArraySize(pointsArr);
            for (uint i = 0; i < size; ++i) {
                var prop = EditorUtils.GetArrayElementByIndex(pointsArr, i);
                var val = (InstantiateAvatarViewComponent.AnimationData)prop.managedReferenceValue;
                if (val.animationId == id) {
                    ++count;
                }
            }
            return count;
        }

        private void AddPoint(SerializedProperty animatorData, VisualElement pointsContainer, AnimationClip clip, float normalizedPosition) {
            var pointsArr = animatorData.FindPropertyRelative("points");
            pointsContainer.Clear();
            var id = ObjectReferenceRegistry.GetId(clip);
            var size = EditorUtils.GetArraySize(pointsArr);
            EditorUtils.SetArraySize(pointsArr, size + 1u);
            var prop = EditorUtils.GetArrayElementByIndex(pointsArr, size);
            var data = new InstantiateAvatarViewComponent.AnimationData() {
                fireFrame = (uint)(normalizedPosition * (clip.length * clip.frameRate)),
                animationId = id,
                firePoint = new InstantiateAvatarViewComponent.AnimationData.FirePoint() {
                    position = float3.zero,
                    rotation = quaternion.identity,
                },
            };
            prop.managedReferenceValue = data;
            this.LoadPoints(animatorData, pointsContainer, clip);
        }

        private void RemovePoint(SerializedProperty animatorData, uint index, VisualElement pointsContainer, AnimationClip clip) {
            var pointsArr = animatorData.FindPropertyRelative("points");
            EditorUtils.RemoveArrayElementByIndex(pointsArr, index);
            this.LoadPoints(animatorData, pointsContainer, clip);
        }
        
        private SerializedProperty GetPoint(SerializedProperty animatorData, AnimationClip clip, float normalizedPosition, out uint index) {
            index = 0u;
            var pointsArr = animatorData.FindPropertyRelative("points");
            var id = ObjectReferenceRegistry.GetId(clip);
            var size = EditorUtils.GetArraySize(pointsArr);
            var fireFrame = (uint)math.round(normalizedPosition * (clip.length * clip.frameRate));
            for (uint i = 0; i < size; ++i) {
                var prop = EditorUtils.GetArrayElementByIndex(pointsArr, i);
                var val = (InstantiateAvatarViewComponent.AnimationData)prop.managedReferenceValue;
                if (val.animationId == id && val.fireFrame == fireFrame) {
                    index = i;
                    return prop;
                }
            }

            return null;
        }

        private void LoadPoints(SerializedProperty animatorData, VisualElement pointsContainer, AnimationClip clip) {
            var pointsArr = animatorData.FindPropertyRelative("points");
            pointsContainer.Clear();
            var id = ObjectReferenceRegistry.GetId(clip);
            var size = EditorUtils.GetArraySize(pointsArr);
            for (uint i = 0; i < size; ++i) {
                var prop = EditorUtils.GetArrayElementByIndex(pointsArr, i);
                var val = (InstantiateAvatarViewComponent.AnimationData)prop.managedReferenceValue;
                if (val.animationId == id) {
                    var point = new VisualElement();
                    point.AddToClassList("point");
                    point.style.left = new StyleLength(new Length(val.fireFrame / (clip.length * clip.frameRate) * 100f, LengthUnit.Percent));
                    pointsContainer.Add(point);
                }
            }
        }

    }

}