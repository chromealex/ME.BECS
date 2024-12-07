using NUnit.Framework;

namespace ME.BECS.Tests {

    public static class AllTests {

        public static void Start() {
            ObjectReferenceRegistry.ClearRuntimeObjects();
            {
                var type = System.Type.GetType("ME.BECS.Editor.StaticMethods, ME.BECS.Gen.Editor");
                type.GetMethod("Load").Invoke(null, null);
            }
        }

        public static void Dispose() {
            ObjectReferenceRegistry.ClearRuntimeObjects();
            Worlds.ResetWorldsCounter();
        }

    }

    public struct TestAspect : IAspect {
            
        public Ent ent { get; set; }

        [QueryWith]
        public AspectDataPtr<TestComponent> dataPtr;
        public AspectDataPtr<Test1Component> dataPtr1;
        public AspectDataPtr<Test2Component> dataPtr2;
        public AspectDataPtr<Test3Component> dataPtr3;
        public AspectDataPtr<Test4Component> dataPtr4;
        public AspectDataPtr<Test5Component> dataPtr5;

        public ref TestComponent data => ref this.dataPtr.Get(this.ent.id, this.ent.gen);
        public ref Test1Component data1 => ref this.dataPtr1.Get(this.ent.id, this.ent.gen);
        public ref Test2Component data2 => ref this.dataPtr2.Get(this.ent.id, this.ent.gen);
        public ref Test3Component data3 => ref this.dataPtr3.Get(this.ent.id, this.ent.gen);
        public ref Test4Component data4 => ref this.dataPtr4.Get(this.ent.id, this.ent.gen);
        public ref Test5Component data5 => ref this.dataPtr5.Get(this.ent.id, this.ent.gen);
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