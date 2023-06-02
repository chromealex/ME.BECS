using NUnit.Framework;

namespace ME.BECS.Tests {
    
    public struct TestAspect : IAspect {
            
        public Ent ent { get; set; }

        [QueryWith]
        public AspectDataPtr<TestComponent> dataPtr;
        public AspectDataPtr<Test1Component> dataPtr1;
        public AspectDataPtr<Test2Component> dataPtr2;
        public AspectDataPtr<Test3Component> dataPtr3;
        public AspectDataPtr<Test4Component> dataPtr4;
        public AspectDataPtr<Test5Component> dataPtr5;

        public ref TestComponent data => ref this.dataPtr.Get(this.ent);
        public ref Test1Component data1 => ref this.dataPtr1.Get(this.ent);
        public ref Test2Component data2 => ref this.dataPtr2.Get(this.ent);
        public ref Test3Component data3 => ref this.dataPtr3.Get(this.ent);
        public ref Test4Component data4 => ref this.dataPtr4.Get(this.ent);
        public ref Test5Component data5 => ref this.dataPtr5.Get(this.ent);

    }

    [ComponentGroup(1)]
    public struct TestComponent : IComponent {

        public int data;

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