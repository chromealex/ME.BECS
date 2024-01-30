using ME.BECS.Transforms;
using ME.BECS.Views;

namespace ME.BECS.Pathfinding {
    
    using BURST = Unity.Burst.BurstCompileAttribute;
    using Unity.Mathematics;

    public struct DemoSystem : IUpdate {

        // we reserve X trees for each player starting from this index
        public const int PLAYERS_TREE_OFFSET = 5;
        public const int DEFAULT_TREE_MASK = (1 << 0) | (1 << 1) | (1 << 2) | (1 << 3) | (1 << 4);
        
        public ME.BECS.Views.View view;
        public ME.BECS.Views.View view2;
        public ME.BECS.Views.View buildingView;
        public float3 testPosition;
        public uint typeId;
        public uint typeId2;
        public int spawnCount;
        public int spawnCount2;
        public float cameraSpeed;
        public float smoothSpeed;
        public float2 buildingSize;

        public Config config1;
        public Config config2;
        public Config config1Attack;
        public Config config2Attack;
        
        private UnityEngine.Vector2 targetPos;

        public ME.BECS.Players.PlayerAspect activePlayer;

        private static Ent CreateUnit(ref ME.BECS.Units.UnitGroupAspect group, in ME.BECS.Units.AgentType props,
                                      in ME.BECS.Players.PlayerAspect owner, in Config config, in Config attackConfig, in ViewSource view, in float3 position) {
            
            var unit = Units.Utils.CreateUnit(in props, PLAYERS_TREE_OFFSET + (int)owner.index);

            ME.BECS.Players.PlayerUtils.SetOwner(unit.ent, in owner);
            var tr = unit.ent.GetAspect<Transforms.TransformAspect>();
            tr.rotation = quaternion.identity;
            tr.position = position;
            config.Apply(unit.ent);

            var queryAspect = unit.ent.GetAspect<QuadTreeQueryAspect>();
            queryAspect.query.range = props.avoidanceRange;
            queryAspect.query.nearestCount = 5;
            queryAspect.query.treeMask = 1 << (PLAYERS_TREE_OFFSET + (int)owner.index);
            { // add attack sensor
                // use 5,6,7,8 trees for each player, so we need to check other trees only
                var targetsMask = ~((1 << (PLAYERS_TREE_OFFSET + (int)owner.index)) | DEFAULT_TREE_MASK);
                var attackSensor = ME.BECS.Attack.AttackUtils.CreateAttackSensor(targetsMask, attackConfig);
                attackSensor.SetParent(unit.ent);
                unit.componentRuntime.attackSensor = attackSensor;
            }
            unit.owner = owner.ent;
            unit.ent.InstantiateView(view);
            group.Add(in unit);

            return unit.ent;

        }
        
        public void OnUpdate(ref SystemContext context) {

            Unity.Jobs.LowLevel.Unsafe.JobsUtility.ResetJobWorkerCount();
            //Unity.Jobs.LowLevel.Unsafe.JobsUtility.JobWorkerCount = 0;
            
            {
                if (this.activePlayer.IsAlive() == false) {
                    this.activePlayer = context.world.GetSystem<ME.BECS.Players.PlayersSystem>().GetPlayerEntity(0u);
                }
                if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.Alpha1) == true) {
                    this.activePlayer = context.world.GetSystem<ME.BECS.Players.PlayersSystem>().GetPlayerEntity(0u);
                }
                if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.Alpha2) == true) {
                    this.activePlayer = context.world.GetSystem<ME.BECS.Players.PlayersSystem>().GetPlayerEntity(1u);
                }
                if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.Alpha3) == true) {
                    this.activePlayer = context.world.GetSystem<ME.BECS.Players.PlayersSystem>().GetPlayerEntity(2u);
                }
                if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.Alpha4) == true) {
                    this.activePlayer = context.world.GetSystem<ME.BECS.Players.PlayersSystem>().GetPlayerEntity(3u);
                }
            }
            {
                var camera = UnityEngine.Camera.main;
                var v = new UnityEngine.Vector2();
                if (UnityEngine.Input.GetKey(UnityEngine.KeyCode.A) == true) {
                    v += UnityEngine.Vector2.left;
                }

                if (UnityEngine.Input.GetKey(UnityEngine.KeyCode.W) == true) {
                    v += UnityEngine.Vector2.up;
                }

                if (UnityEngine.Input.GetKey(UnityEngine.KeyCode.S) == true) {
                    v += UnityEngine.Vector2.down;
                }

                if (UnityEngine.Input.GetKey(UnityEngine.KeyCode.D) == true) {
                    v += UnityEngine.Vector2.right;
                }

                var tr = camera.transform.parent;
                v *= this.cameraSpeed * context.deltaTime;
                this.targetPos += v;
                tr.position = UnityEngine.Vector3.Lerp(tr.position, tr.position + tr.rotation * new UnityEngine.Vector3(this.targetPos.x, 0f, this.targetPos.y), context.deltaTime * this.smoothSpeed);
                this.targetPos = UnityEngine.Vector2.Lerp(this.targetPos, UnityEngine.Vector2.zero, context.deltaTime * this.smoothSpeed);
            }

            if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.Space) == true) {

                var system = context.world.GetSystem<BuildGraphSystem>();
                
                var group = Units.Utils.CreateGroup(system.GetTargetsCapacity());
                for (int i = 0; i < this.spawnCount; ++i) {
                    
                    var props = system.GetAgentProperties(this.typeId);
                    CreateUnit(ref group, in props, in this.activePlayer, this.config1, this.config1Attack,
                               this.view, ME.BECS.Units.Utils.GetSpiralPosition(this.testPosition, i, props.radius));
                    
                }
                
                for (int i = 0; i < this.spawnCount2; ++i) {
                    
                    var props = system.GetAgentProperties(this.typeId2);
                    CreateUnit(ref group, in props, in this.activePlayer, this.config2, this.config2Attack,
                               this.view2, ME.BECS.Units.Utils.GetSpiralPosition(this.testPosition, i, props.radius));
                    
                }

            }

            if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.F) == true) {
                
                var system = context.world.GetSystem<BuildGraphSystem>();
                for (uint j = 0; j < 2; ++j) {
                    var player = context.world.GetSystem<ME.BECS.Players.PlayersSystem>().GetPlayerEntity(j);

                    var group = Units.Utils.CreateGroup(system.GetTargetsCapacity());
                    for (int i = 0; i < this.spawnCount; ++i) {

                        var props = system.GetAgentProperties(this.typeId);
                        CreateUnit(ref group, in props, in player, this.config1, this.config1Attack,
                                   this.view, ME.BECS.Units.Utils.GetSpiralPosition(this.testPosition, i, props.radius));

                    }

                    for (int i = 0; i < this.spawnCount2; ++i) {

                        var props = system.GetAgentProperties(this.typeId2);
                        CreateUnit(ref group, in props, in player, this.config2, this.config2Attack,
                                   this.view2, ME.BECS.Units.Utils.GetSpiralPosition(this.testPosition, i, props.radius));

                    }
                }
                
            }

            if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.G) == true) {

                // make one group from all groups
                var units = new Unity.Collections.NativeList<ME.BECS.Units.UnitAspect>(Unity.Collections.Allocator.Temp);
                API.Query(in context).WithAspect<ME.BECS.Units.UnitAspect>().ForEach((in CommandBufferJob buffer) => {
                    var unit = buffer.ent.GetAspect<ME.BECS.Units.UnitAspect>();
                    if (unit.WillRemoveGroup() == true) {
                        // group will be removed - remove path
                        PathUtils.DestroyTargets(unit.unitGroup.GetAspect<ME.BECS.Units.UnitGroupAspect>());
                    }

                    unit.RemoveFromGroup();
                    units.Add(unit);
                });

                var group = ME.BECS.Units.Utils.CreateGroup((uint)units.Length);
                foreach (var unit in units) group.Add(unit);

            }

            if (UnityEngine.Input.GetMouseButtonDown(0) == true) {

                var ray = UnityEngine.Camera.main.ScreenPointToRay(UnityEngine.Input.mousePosition);
                if (UnityEngine.Physics.Raycast(ray, out var hit, 1000f) == true) {

                    var worldPos = hit.point;
                    worldPos.y = 0f;

                    var buildGraphSystem = context.world.GetSystem<BuildGraphSystem>();
                    API.Query(in context).WithAspect<ME.BECS.Units.UnitGroupAspect>().ForEach((in CommandBufferJob buffer) => {
                        PathUtils.UpdateTarget(buildGraphSystem, buffer.ent.GetAspect<ME.BECS.Units.UnitGroupAspect>(), worldPos);
                    });

                }

            }

            if (UnityEngine.Input.GetMouseButtonDown(1) == true) {

                var ray = UnityEngine.Camera.main.ScreenPointToRay(UnityEngine.Input.mousePosition);
                if (UnityEngine.Physics.Raycast(ray, out var hit, 1000f) == true) {

                    var worldPos = hit.point;
                    worldPos.y = 0f;

                    // build
                    var buildGraphSystem = context.world.GetSystem<BuildGraphSystem>();
                    if (GraphUtils.IsGraphMaskValid(buildGraphSystem, worldPos, quaternion.identity, this.buildingSize, 0, 254) == true) {
                        var building = GraphUtils.CreateGraphMask(worldPos, quaternion.identity, this.buildingSize);
                        building.InstantiateView(this.buildingView);
                    }

                }

            }

        }

    }

}