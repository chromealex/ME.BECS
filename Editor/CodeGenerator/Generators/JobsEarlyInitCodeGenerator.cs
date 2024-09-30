using System.Linq;
using System.Reflection;

namespace ME.BECS.Editor.Jobs {
    
    using ME.BECS.Jobs;

    public class JobsEarlyInitCodeGenerator : CustomCodeGenerator {

        private void Generate<TJobBase, TComponent>(System.Collections.Generic.List<string> dataList, string method) {
            
            {
                var jobsComponents = UnityEditor.TypeCache.GetTypesDerivedFrom(typeof(TJobBase));
                foreach (var jobType in jobsComponents) {

                    if (jobType.IsValueType == false) continue;
                    if (jobType.IsVisible == false) continue;

                    if (this.IsValidTypeForAssembly(jobType) == false) continue;

                    var jobTypeFullName = EditorUtils.GetTypeName(jobType);
                    var components = new System.Collections.Generic.List<string>();
                    var interfaces = jobType.GetInterfaces();
                    System.Type workInterface = null;
                    foreach (var i in interfaces) {
                        if (i.IsGenericType == true) {
                            foreach (var type in i.GenericTypeArguments) {
                                if (typeof(TComponent).IsAssignableFrom(type) == true) {
                                    if (this.IsValidTypeForAssembly(type) == false) continue;
                                    components.Add(EditorUtils.GetDataTypeName(type));
                                }
                            }

                            workInterface = i;
                            break;
                        }
                    }

                    if (workInterface != null && components.Count == workInterface.GenericTypeArguments.Length) {

                        var str = $"ME.BECS.Jobs.EarlyInit.{method}<{jobTypeFullName}, {string.Join(", ", components)}>();";
                        dataList.Add(str);

                    }

                }
            }
            
        }
        
        public override void AddInitialization(System.Collections.Generic.List<string> dataList, System.Collections.Generic.List<System.Type> references) {

            this.Generate<IJobParallelForComponentsBase, IComponent>(dataList, "DoParallelForComponents");
            this.Generate<IJobComponentsBase, IComponent>(dataList, "DoComponents");
            this.Generate<IJobParallelForAspectBase, IAspect>(dataList, "DoParallelForAspect");
            this.Generate<IJobAspectBase, IAspect>(dataList, "DoAspect");
            
        }

    }

}