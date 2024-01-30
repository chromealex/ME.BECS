using System.Linq;
using UnityEngine;

namespace ME.BECS.FeaturesGraph {

    [CreateAssetMenu(menuName = "ME.BECS/Features Graph")]
    public class SystemsGraph : Extensions.GraphProcessor.BaseGraph {

        public SystemGroup runtimeRootSystemGroup;
        
        public override void InitializeValidation() {

            base.InitializeValidation();

            this.ValidateStartNode();

        }

        private void ValidateStartNode() {

            if (this.nodes.Any(x => x is ME.BECS.FeaturesGraph.Nodes.StartNode) == false) {
                this.AddNode(new Nodes.StartNode() {
                    position = new Rect(0f, 0f, 100f, 100f),
                    GUID = "StartNode",
                });
            }

            if (this.nodes.Any(x => x is ME.BECS.FeaturesGraph.Nodes.ExitNode) == false) {
                this.AddNode(new Nodes.ExitNode() {
                    position = new Rect(500f, 0f, 100f, 100f),
                    GUID = "EndNode",
                });
            }

        }

        public SystemGroup DoAwake(ref World world, ushort updateType) {
            
            var rootSystemGroup = SystemGroup.Create(updateType);

            if (SystemsStatic.RaiseInitialize(this.GetInstanceID(), ref rootSystemGroup) == false) {
                
                this.runtimeRootSystemGroup = rootSystemGroup;
                var processor = new Extensions.GraphProcessor.ProcessGraphProcessor(this);
                processor.Run();
                world.AssignRootSystemGroup(rootSystemGroup);
                return this.runtimeRootSystemGroup;

            }

            return rootSystemGroup;

        }

        public ME.BECS.Extensions.GraphProcessor.BaseNode GetStartNode() {
            foreach (var node in this.nodes) {
                if (node is ME.BECS.FeaturesGraph.Nodes.StartNode n) {
                    return n;
                }
            }
            return null;
        }

        public ME.BECS.Extensions.GraphProcessor.BaseNode GetEndNode() {
            foreach (var node in this.nodes) {
                if (node is ME.BECS.FeaturesGraph.Nodes.ExitNode n) {
                    return n;
                }
            }
            return null;
        }

    }

}