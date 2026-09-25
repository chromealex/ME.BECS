using NUnit.Framework;
using System.Linq;

namespace ME.BECS.Tests {

    public static class AllTests {

        public static void Start() {
            var type = ResolveEditorBootstrap();
            var load = type.GetMethod("Load", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
                null, System.Type.EmptyTypes, null);
            if (load == null || load.ReturnType != typeof(void))
                throw new System.InvalidOperationException("ME.BECS Editor bootstrap is incompatible: expected public static void StaticMethods.Load(). Regenerate the Editor bootstrap and wait for Unity compilation.");
            ObjectReferenceRegistry.ClearRuntimeObjects();
            load.Invoke(null, null);
        }

        private static System.Type ResolveEditorBootstrap() {
            const string assemblyName = "ME.BECS.Gen.Editor";
            const string typeName = "ME.BECS.Editor.StaticMethods";
            var loaded = System.AppDomain.CurrentDomain.GetAssemblies();
            var candidates = loaded.Where(assembly => !assembly.IsDynamic && assembly.GetName().Name == assemblyName).ToArray();
            if (candidates.Length > 1)
                throw new System.InvalidOperationException("Ambiguous Editor bootstrap: multiple loaded assemblies named " + assemblyName + ". Restart the Unity domain before running integration tests.");
            System.Reflection.Assembly owner;
            if (candidates.Length == 1) {
                owner = candidates[0];
            } else {
                // Resolve by assembly explicitly: Type.GetType returning null conflates a
                // missing assembly with an existing assembly that lacks the expected type.
                try {
                    owner = System.Reflection.Assembly.Load(new System.Reflection.AssemblyName(assemblyName));
                } catch (System.Exception exception) when (exception is System.IO.FileNotFoundException ||
                    exception is System.IO.FileLoadException || exception is System.BadImageFormatException) {
                    var related = loaded.Select(assembly => assembly.GetName().Name)
                        .Where(name => name.StartsWith("ME.BECS", System.StringComparison.Ordinal))
                        .OrderBy(name => name, System.StringComparer.Ordinal);
                    throw new System.InvalidOperationException("Editor bootstrap assembly cannot be loaded: " + assemblyName +
                        ". Loader error: " + exception.GetType().Name + ": " + exception.Message +
                        "\nLoaded BECS assemblies: " + string.Join(", ", related) +
                        "\nThis is an assembly loading/compilation failure, not an entity-count mismatch.", exception);
                }
            }
            var type = owner.GetType(typeName, false);
            if (type == null)
                throw new System.InvalidOperationException("Editor bootstrap assembly is loaded (" + owner.FullName +
                    "), but type " + typeName + " is absent. StaticTypesInitializer present: " +
                    (owner.GetType("ME.BECS.Editor.StaticTypesInitializer", false) != null) +
                    ". Check the first Editor export error: compiled source catalogs alone do not provide the bootstrap entry point.");
            return type;
        }

        public static void Dispose() {
            ObjectReferenceRegistry.ClearRuntimeObjects();
            Worlds.ResetWorldsCounter();
        }

    }

    public partial struct TestAspect : IAspect {
            
        public Ent ent { get; set; }

        [QueryWith]
        public AspectDataPtr<TestComponent> dataPtr;
        public AspectDataPtr<Test1Component> dataPtr1;
        public AspectDataPtr<Test2Component> dataPtr2;
        public AspectDataPtr<Test3Component> dataPtr3;
        public AspectDataPtr<Test4Component> dataPtr4;
        public AspectDataPtr<Test5Component> dataPtr5;

        public ref TestComponent data => ref this.dataPtr.GetOrThrow(this.ent.id, this.ent.gen);
        public ref Test1Component data1 => ref this.dataPtr1.Get(this.ent.id, this.ent.gen);
        public ref Test2Component data2 => ref this.dataPtr2.Get(this.ent.id, this.ent.gen);
        public ref Test3Component data3 => ref this.dataPtr3.Get(this.ent.id, this.ent.gen);
        public ref Test4Component data4 => ref this.dataPtr4.Get(this.ent.id, this.ent.gen);
        public ref Test5Component data5 => ref this.dataPtr5.Get(this.ent.id, this.ent.gen);
        public readonly ref readonly TestComponent dataRead => ref this.dataPtr.Read(this.ent.id, this.ent.gen);
        public readonly ref readonly Test5Component data5read => ref this.dataPtr5.Read(this.ent.id, this.ent.gen);

        public static void TestInitialize(in World world) {
            ref var aspect = ref world.InitializeAspect<TestAspect>();
            aspect.dataPtr = new AspectDataPtr<TestComponent>(in world);
            aspect.dataPtr1 = new AspectDataPtr<Test1Component>(in world);
            aspect.dataPtr2 = new AspectDataPtr<Test2Component>(in world);
            aspect.dataPtr3 = new AspectDataPtr<Test3Component>(in world);
            aspect.dataPtr4 = new AspectDataPtr<Test4Component>(in world);
            aspect.dataPtr5 = new AspectDataPtr<Test5Component>(in world);
        }

    }

    public struct TestGroup { }

    [ComponentGroup(typeof(TestGroup))]
    public struct TestComponent : IComponent {

        public int data;

    }
    
    public struct TestComponentTag : IComponent {

    }
    
    public struct Test1Component : IComponent {

        public int data;

    }

    public struct Test2Component : IComponent {

        public int data;

    }

    public struct Test3Component : IComponent {

        public int data;

    }

    public struct Test4Component : IComponent {

        public int data;

    }
    
    public struct Test5Component : IComponent {

        public int data;

    }

}
