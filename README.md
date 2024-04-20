<img src="Editor/Resources/ME.BECS.Resources/Icons/logo-512.png" width="200px" align="left" />

# ME.BECS
Bursted Entity Component System
<br>
<br>
<br>
<br>
<br>
<br>

> [!IMPORTANT]
> Currently ME.BECS is in alpha version, if you need stable ECS with rollbacks use [ME.ECS](https://github.com/chromealex/ecs)

## <img src="Editor/Resources/ME.BECS.Resources/Icons/logo-32.png" width="18px" height="18px" /> Benefits
- You can use all API in Burst and in parallel mode without copying data to Native Arrays;
- Clone world/Serialize world very fast;
- Deterministic.

## <img src="Editor/Resources/ME.BECS.Resources/Icons/logo-32.png" width="18px" height="18px" /> Project Initialization
- Create csc.rsp in Assets directory with this content:
```
-define:EXCEPTIONS_CONTEXT
-define:EXCEPTIONS_THREAD_SAFE
-define:EXCEPTIONS_COLLECTIONS
-define:EXCEPTIONS_COMMAND_BUFFER
-define:EXCEPTIONS_ENTITIES
-define:EXCEPTIONS_QUERY_BUILDER
-define:EXCEPTIONS_INTERNAL
-define:EXCEPTIONS
```
- Use "Create/ME.BECS/Create Project" menu to create default project.

Tested in Unity 2023.1

## <img src="Editor/Resources/ME.BECS.Resources/Icons/logo-32.png" width="18px" height="18px" /> Dependencies
```
"dependencies": {
    "com.unity.collections": "2.4.0-pre.2",
    "com.unity.jobs": "0.70.0-preview.7",
    "com.unity.burst": "1.8.4",
    "com.unity.mathematics": "1.2.6",
    "com.unity.profiling.core": "1.0.2"
  },
```

## <img src="Editor/Resources/ME.BECS.Resources/Icons/logo-32.png" width="18px" height="18px" /> API
#### Create new world
WIKI [https://github.com/chromealex/ME.BECS/wiki/New-World](https://github.com/chromealex/ME.BECS/wiki/New-World)

#### Entities
WIKI [https://github.com/chromealex/ME.BECS/wiki/Entity-API](https://github.com/chromealex/ME.BECS/wiki/Entity-API)

#### Create components
```csharp
[ComponentGroup(10)] // Set component to group (optional)
public struct Component : IComponent {
    // (optional) Initialize component with default data (ex: ent.Read<Component>() or ent.Get<Component>() returns this value by default)
    public static Component Default => new Component() { data = 100 };
    // Any unmanaged data
    public int data;
    // Reference to any persistent UnityEngine.Object
    public ObjectReference<UnityEngine.Mesh> unityObjectReference;
}
```

#### Access components
```csharp
// Set data
ent.Set(new Component() { ... });

// Get data - create component data if not exist
ref var comp = ref ent.Get<Component>();

// Read data - returns empty data if not exist
ref readonly var comp = ref ent.Read<Component>();

// Remove data - returns true if removed
ent.Remove<Component>();

// Has data - return true if exist
bool has = ent.Has<Component>();

// Has data - return true if static component is exist (from EntityConfig)
bool has = ent.HasStatic<Component>();
    
// Read data - return static data (from EntityConfig)
var comp = ent.ReadStatic<Component>();

// Remove shared component - return true if removed
ent.RemoveShared<Component>([hash]);

// Set shared component
ent.SetShared(new Component());

// Has shared component - return true if component is exist
bool has = ent.HasShared<Component>();

// Read shared component
ref readonly var comp = ref ent.ReadShared<Component>([hash]);

// Get shared component
ref var comp = ref ent.GetShared<Component>([hash]);
```

#### Systems
Awake systems
```csharp
public unsafe struct TransformWorldMatrixUpdateSystem : IAwake {
    public void OnAwake(ref SystemContext context) {
        var jobHandle = ...
        context.SetDependency(jobHandle);
    }
}
```

Update systems
```csharp
[BurstCompile] // Use burst in awake/update/destroy by default
public unsafe struct TransformWorldMatrixUpdateSystem : IUpdate {
    [WithoutBurst] // Do not compile this method into burst
    public void OnUpdate(ref SystemContext context) {
        var jobHandle = ...
        context.SetDependency(jobHandle);
    }
}
```

Destroy systems
```csharp
public unsafe struct TransformWorldMatrixUpdateSystem : IDestroy {
    public void OnDestroy(ref SystemContext context) {
        var jobHandle = ...
        context.SetDependency(jobHandle);
    }
}
```

#### Aspects
```csharp
public struct MyAspect : IAspect {
        
    public Ent ent { get; set; }

    [QueryWith] // QueryWith attribute means that only this component will be used in query
    private RefRW<MyComponent1> component1Data;
    private RefRW<MyComponent3> component2Data;
    
    public ref MyComponent1 component1 => ref this.component1Data.Get(this.ent.id);
    public ref MyComponent2 component2 => ref this.component2Data.Get(this.ent.id);
    
    ...
        
}

var aspect = ent.GetAspect<MyAspect>();
aspect.component1.data = 123;
```

#### Queries
Regular runtime query
```csharp
var jobHandle = API.Query(world, dependsOn)
                   .WithAll<TestComponent, Test2Component>()
                   .WithAny<TestComponent, Test2Component>()
                   .With<TestComponent>()
                   .Without<Test3Component>()
                   .WithAspect<TestAspect>()
                   .ParallelFor(64)
                   .ForEach((in CommandBufferJob commandBuffer) => {
                       var ent = commandBuffer.ent;
                       ent // Entity access
                   });
```

Aspect job query
```csharp
[BurstCompile]
private struct MyJob : IJobParallelForAspect<MyAspect> {
    public void Execute(ref MyAspect aspect) {
        ...
    }
}
var query = API.Query(world, dependsOn).ScheduleParallelFor<MyJob, MyAspect>(new MyJob() { ... });
```

Components query
```csharp
[BurstCompile]
private struct MyJob : IJobComponents<MyComponent1, MyComponent2, ...> {
    public void Execute(ref MyComponent1 comp1, ref MyComponent2 comp2, ...) {
        ...
    }
}
var query = API.Query(world, dependsOn).ScheduleParallelFor<MyJob, MyComponent1, MyComponent2, ...>(new MyJob() { ... });
```

#### Jobs
Regular jobs
```csharp
[BurstCompile]
public void Job : IJob {
    public Ent ent;
    public void Execute() {
        ent.Get<MyComponent>().data = 123;
    }
}
```

#### Clone/Copy world
```csharp
// Clone world
var newWorld = world.Clone();

// Copy world
world.CopyFrom(sourceWorld);
```

#### Serialize/Deserialize world
```csharp
// Serialize world
var bytes = world.Serialize();

// Deserialize world
var world = World.Create(bytes);
```

#### Views
```csharp
// Instantiate view
ent.InstantiateView(viewSource);

// Destroy view
ent.DestroyViews(viewSource);
```
