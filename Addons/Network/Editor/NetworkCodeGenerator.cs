
using System.Linq;

namespace ME.BECS.Network.Editor {

    using ME.BECS.Editor;
    
    public class NetworkCodeGenerator : CustomCodeGenerator {
        
        public override System.Collections.Generic.List<CodeGenerator.MethodDefinition> AddMethods(System.Collections.Generic.List<System.Type> references) {
            
            var content = new System.Collections.Generic.List<string>();
            var methods = UnityEditor.TypeCache.GetMethodsWithAttribute<NetworkMethodAttribute>().OrderBy(x => x.Name).ToList();
            references.Add(typeof(UnsafeNetworkModule));
            foreach (var method in methods) {

                if (method.IsStatic == false) continue;
                if (method.DeclaringType.IsVisible == false) continue;
                
                if (this.IsValidTypeForAssembly(method.DeclaringType) == false) continue;

                var str = $"methods.Add({method.DeclaringType.FullName}.{method.Name});";
                content.Add(str);

            }
            
            var def = new CodeGenerator.MethodDefinition() {
                methodName = "NetworkLoad",
                type = "ME.BECS.Network.UnsafeNetworkModule.MethodsStorage",
                registerMethodName = "RegisterCallback",
                definition = "ref ME.BECS.Network.UnsafeNetworkModule.MethodsStorage methods",
                content = string.Join("\n", content.ToArray()),
            };
            return new System.Collections.Generic.List<CodeGenerator.MethodDefinition>() { def };
            
        }

    }

}