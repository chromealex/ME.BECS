<img src="https://github.com/chromealex/ME.BECS/blob/main/ME.BECS/Editor/EditorResources/ME.BECS.Resources/Icons/logo-512.png" width="200px" align="left" />

# ME.BECS
Bursted Entity Component System
<br>
<br>
<br>
<br>
<br>
<br>

## Benefits
- You can use all API in Burst and in parallel mode without copying data to Native Arrays;
- Clone world/Serialize world very fast;
- Deterministic.

## Project Initialization
- Create csc.rsp in Assets directory;
- Use "Create/ME.BECS/Create Project" menu to create default project.

Tested in Unity 2023.1

## Dependencies
```
"dependencies": {
    "com.unity.collections": "1.4.0",
    "com.unity.ui": "1.0.0-preview.18",
    "com.unity.jobs": "0.70.0-preview.7",
    "com.unity.burst": "1.8.4",
    "com.unity.mathematics": "1.2.6",
    "com.unity.profiling.core": "1.0.2"
  },
```

## API
#### Create new world
```csharp
var world = World.Create();
...
world.Dispose();
```

#### Entities
```csharp
// Create new entity
var ent = Ent.New();

// Destroy entity
ent.Destroy();

// Get entity's version
ent.Version;

// Get entity's version group
ent.GetVersion(groupId);

// Clone entity
var clone = ent.Clone();

// Copy entity
ent.CopyFrom(sourceEntity);
```

#### Create components
```csharp
[ComponentGroup(10)] // Set component to group (optional)
public struct Component : IComponent {
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
