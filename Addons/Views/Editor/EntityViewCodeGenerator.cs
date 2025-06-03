using System.Linq;
using System.Reflection;
using ME.BECS.Views;
using ME.BECS.Editor.Jobs;
using scg = System.Collections.Generic;

namespace ME.BECS.Editor.Aspects {

    public class EntityViewCodeGenerator : CustomCodeGenerator {

        public override void AddInitialization(System.Collections.Generic.List<string> dataList, System.Collections.Generic.List<System.Type> references) {
            
            dataList.Add("EntityViewsStaticTracker.InitializeViewsTracker();");
            
        }

        public override FileContent[] AddFileContent(System.Collections.Generic.List<System.Type> references) {

            var fileContent = new FileContent();
            fileContent.filename = "EntityView";
            var str = new System.Text.StringBuilder();
            str.AppendLine("public class EntityViewsStaticTracker {");
            {
                var types = new scg::HashSet<JobsEarlyInitCodeGenerator.TypeInfo>();
                str.AppendLine("public static void InitializeViewsTracker() {");
                var startIndex = str.Length;
                var views = UnityEditor.TypeCache.GetTypesDerivedFrom<EntityView>().OrderBy(x => x.FullName).ToArray();
                var modules = UnityEditor.TypeCache.GetTypesDerivedFrom<IViewModule>().OrderBy(x => x.FullName).ToArray();
                str.AppendLine($"ViewsTracker.SetTracker({views.Length + modules.Length}u);");

                var methods = new System.Collections.Generic.Dictionary<string, System.Type[]> {
                    { "ViewsTracker.TrackView", views },
                    { "ViewsTracker.TrackViewModule", modules },
                };

                var ignoreTypes = new scg::HashSet<System.Type>();
                foreach (var method in methods) {
                    foreach (var viewType in method.Value) {
                        if (viewType.IsAbstract == true) continue;
                        if (viewType.GenericTypeArguments.Length > 0) continue;
                        if (this.IsValidTypeForAssembly(viewType) == false) continue;
                        references.Add(viewType);
                        str.AppendLine($"// ==============================");
                        str.AppendLine($"// {viewType.FullName}");
                        str.AppendLine("{");
                        str.AppendLine($"var viewInfo = new ViewsTracker.ViewInfo();");
                        var startIdx = str.Length;
                        var idx = 0u;
                        if (typeof(IViewIgnoreTracker).IsAssignableFrom(viewType) == false) {

                            ignoreTypes.Clear();
                            var interfaces = viewType.GetInterfaces();
                            foreach (var interfaceType in interfaces) {
                                if (interfaceType.IsGenericType == true && typeof(IViewTrackIgnore<>).IsAssignableFrom(interfaceType) == true) {
                                    var t = interfaceType.GetGenericArguments()[0];
                                    ignoreTypes.Add(t);
                                }
                            }

                            AddMethod(str, types, ignoreTypes, viewType, nameof(EntityView.ApplyState), ref idx);
                            AddMethod(str, types, ignoreTypes, viewType, nameof(EntityView.OnUpdate), ref idx);
                            AddMethod(str, types, ignoreTypes, viewType, nameof(EntityView.OnInitialize), ref idx);
                            AddMethod(str, types, ignoreTypes, viewType, nameof(EntityView.OnDeInitialize), ref idx);
                            AddMethod(str, types, ignoreTypes, viewType, nameof(EntityView.OnEnableFromPool), ref idx);
                            AddMethod(str, types, ignoreTypes, viewType, nameof(EntityView.OnDisableToPool), ref idx);

                            foreach (var interfaceType in interfaces) {
                                if (interfaceType.IsGenericType == true && typeof(IViewTrack<>).IsAssignableFrom(interfaceType) == true) {
                                    var t = interfaceType.GetGenericArguments()[0];
                                    if (typeof(IAspect).IsAssignableFrom(t) == true) {
                                        var fields = t.GetFields(System.Reflection.BindingFlags.Instance |
                                                                 System.Reflection.BindingFlags.Public |
                                                                 System.Reflection.BindingFlags.NonPublic);
                                        foreach (var field in fields) {
                                            var fieldType = field.FieldType;
                                            if (typeof(IAspectData).IsAssignableFrom(fieldType) == true &&
                                                field.GetCustomAttribute(typeof(QueryWithAttribute)) != null) {
                                                AddType(str, types, new JobsEarlyInitCodeGenerator.TypeInfo() {
                                                    type = fieldType.GenericTypeArguments[0],
                                                    op = RefOp.ReadOnly,
                                                }, ref idx);
                                            }
                                        }
                                    } else {
                                        AddType(str, types, new JobsEarlyInitCodeGenerator.TypeInfo() {
                                            type = t,
                                            op = RefOp.ReadOnly,
                                        }, ref idx);
                                    }
                                }
                            }

                            if (idx > 0u) str.Insert(startIdx, $"viewInfo.tracker.Resize({idx}u);\n");
                        } else {
                            str.AppendLine("// Ignore tracker");
                        }

                        str.AppendLine($"{method.Key}<{EditorUtils.GetTypeName(viewType)}>(viewInfo);");
                        str.AppendLine("}");
                    }
                }

                JobsEarlyInitCodeGenerator.UpdateDeps(types);
                foreach (var type in types) {
                    str.Insert(startIndex, $"StaticTypes<{EditorUtils.GetTypeName(type.type)}>.TrackVersion();\n");
                }
                str.Insert(startIndex, $"StaticTypes.SetTracker({types.Count}u);\n");

                str.AppendLine("}");
            }
            str.AppendLine("}");
            fileContent.content = str.ToString();
            
            return new FileContent[] { fileContent };
            
        }

        public static void AddMethod(System.Text.StringBuilder str, scg::HashSet<JobsEarlyInitCodeGenerator.TypeInfo> types, scg::HashSet<System.Type> ignoreTypes, System.Type obj, string methodName, ref uint idx) {
            var methodInfo = obj.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (methodInfo == null) {
                str.AppendLine($"// {methodName} not found");
                return;
            }
            var typesInfo = JobsEarlyInitCodeGenerator.GetMethodTypesInfo(methodInfo);
            if (typesInfo.Count > 0) str.AppendLine($"// {methodName}:");
            foreach (var type in typesInfo) {
                if (type.op != RefOp.ReadOnly) {
                    UnityEngine.Debug.LogWarning($"EntityView {obj.FullName} writes to {type.type.FullName} in method {methodName}, be sure this view has been used in Visual mode world only");
                }

                if (ignoreTypes.Contains(type.type) == false) {
                    AddType(str, types, type, ref idx);
                } else {
                    str.AppendLine($"// [IGNORE] {type.type.FullName} + {type.op}");
                }
            }
        }

        public static void AddType(System.Text.StringBuilder str, scg::HashSet<JobsEarlyInitCodeGenerator.TypeInfo> types, JobsEarlyInitCodeGenerator.TypeInfo type, ref uint idx) {
            str.AppendLine($"viewInfo.tracker.Get({idx++}u) = StaticTypes<{EditorUtils.GetTypeName(type.type)}>.trackerIndex;");
            str.AppendLine($"// [ADD] {type.type.FullName} + {type.op}");
            types.Add(type);
        }

    }

}