using System.Linq;

namespace ME.BECS.Views.Editor {

    using ME.BECS.Editor;
    
    public class ViewsCodeGenerator : CustomCodeGenerator {

        private delegate void UpdateViewInfo(ref ViewTypeInfo viewTypeInfo);
        private static System.Collections.Generic.KeyValuePair<string, UpdateViewInfo>[] methods = new System.Collections.Generic.KeyValuePair<string, UpdateViewInfo>[] {
            new (nameof(EntityView.ApplyState), (ref ViewTypeInfo x) => x.flags |= TypeFlags.ApplyState),
            new (nameof(EntityView.OnUpdate), (ref ViewTypeInfo x) => x.flags |= TypeFlags.Update),
            new (nameof(EntityView.OnInitialize), (ref ViewTypeInfo x) => x.flags |= TypeFlags.Initialize),
            new (nameof(EntityView.OnDeInitialize), (ref ViewTypeInfo x) => x.flags |= TypeFlags.DeInitialize),
            new (nameof(EntityView.OnEnableFromPool), (ref ViewTypeInfo x) => x.flags |= TypeFlags.EnableFromPool),
            new (nameof(EntityView.OnDisableToPool), (ref ViewTypeInfo x) => x.flags |= TypeFlags.DisableToPool),
        };
        
        public override System.Collections.Generic.List<CodeGenerator.MethodDefinition> AddMethods(System.Collections.Generic.List<System.Type> references) {
            
            var content = new System.Collections.Generic.List<string>();
            var views = UnityEditor.TypeCache.GetTypesDerivedFrom<ME.BECS.Views.EntityView>().OrderBy(x => x.FullName).ToArray();
            references.Add(typeof(UnsafeViewsModule));
            foreach (var viewType in views) {

                if (viewType.IsAbstract == true) continue;
                if (viewType.GenericTypeArguments.Length > 0) continue;

                if (this.IsValidTypeForAssembly(viewType) == false) continue;
                
                var info = new ME.BECS.Views.ViewTypeInfo();
                foreach (var method in methods) {
                    var methodInfo = viewType.GetMethod(method.Key, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                    if (ViewsCodeGenerator.HasOverrideAndNotEmpty(methodInfo) == true) {
                        method.Value.Invoke(ref info);
                    }
                }

                var str = $"ME.BECS.Views.ViewsTypeInfo.RegisterType<{viewType.FullName}>({ViewsCodeGenerator.InfoToString(info)});";
                content.Add(str);
                
                references.Add(viewType);
                
            }
            
            var def = new CodeGenerator.MethodDefinition() {
                methodName = "ViewsLoad",
                type = "ME.BECS.Views.ViewsModuleData",
                registerMethodName = "RegisterCallback",
                definition = "ref ME.BECS.Views.ViewsModuleData viewsModule",
                content = string.Join("\n", content.ToArray()),
            };
            return new System.Collections.Generic.List<CodeGenerator.MethodDefinition>() { def };
            
        }

        private static string InfoToString(ViewTypeInfo info) {
            return $@"new ME.BECS.Views.ViewTypeInfo() {{
{nameof(ViewTypeInfo.flags)} = (ME.BECS.Views.TypeFlags){(int)info.flags},
}}";
        }

        private static bool HasOverrideAndNotEmpty(System.Reflection.MethodInfo m) {
            System.Reflection.MethodBody mb = m.GetMethodBody();
            byte[] b = mb.GetILAsByteArray();
            if (b.Length <= 2) {
                return false;
            }
            return m.GetBaseDefinition().DeclaringType != m.DeclaringType;
        }

    }

}