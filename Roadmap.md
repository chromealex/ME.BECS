## Roadmap
+ Remove Ent from archetypes
+ Batches archetype update
+ Context.world/Context.allocator removed from code (use State* instead)
+ Add ent proxy debugger
+ Systems graph
+ Systems JobHandle parallel support (between frames)
+ Context.world/Context.allocator removed from collections
+ Static filters
+ Serialize/Deserialize world
+ Format all exceptions
+ Copy/Clone world
+ Entity versions
+ Entity grouped versions
+ Shared components
+ Entity Configs (dynamic and static components)
+ Features Graph + Initializer
+ Editor: Use world link in editor window instead of Context.world
+ Transform aspect
+ Archetypes refactoring: add bitarray
+ Add support for multiple graphs in initializer
+ Views: Entity View
+ Refactoring archetypes: add entities list + refactoring SetEntities in query
+ Refactoring: Make FromQueryData async, add archetypesBits to QueryDataStatic (add new archetypes when build query)
+ QueryData: check why we need to check archetypesBits on null
+ Views: test views
+ Views: add reference to prefab drawer
+ Views: add providers support
+ Views: add pools for gameobjects
+ Views Module: bug fix (entities != views)
+ Add modules to world instead of manual control
+ Replace JobUtility in batches/new entity
+ Refactoring aspects: construct cache
+ Entity drawer
+ Refactoring: Entity Add/Destroy entity in parallel
+ Editor: Initializer for world
+ Editor: Load templates and other resources by the search
+ Editor: search entity in archetype panel in worlds viewer
+ Editor: Memory Allocator editor
+ Networking + Input System (markers)
+ Refactoring aspects: make in direct-ref
+ Investigate where we are use _make method inside Set/Remove/Get
+ Graph Editor: add ability to enable/disable systems/features
+ Add WithoutBurstAttribute attribute instead of BurstDiscardAttribute for systems/methods
+ Views Module: refactoring code generator methods
+ Editor: Initialize project with new directory
+ Views Module refactoring: Reuse instances instead of Despawn and then Spawn
+ Systems Editor: add "Edit System Script" button
+ Check why we need job complete in static query schedule
+ Views Module refactoring: providers support
+ Add jobs IJobComponent<T0,... TN>
+ Add support for tag components
+ Editor: Add toolbar in graph view
+ Views Module: add DrawMesh provider
- Static Queries refactoring: store QueryData and CommandBuffer inside static data
- Views Module: add BRG provider
- Views Module: add Particles provider
+ CodeGen: generate systems flow
- Transform: add DestroyHierarchy method
- Views: investigate bug: wrong view getting from pool 