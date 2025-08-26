using ME.BECS.Extensions.GraphProcessor;
using ME.BECS.FeaturesGraph;
using ME.BECS.FeaturesGraph.Nodes;
using UnityEditor;
using UnityEngine;

namespace ME.BECS.Editor.FeaturesGraph {

    using System.Collections.Generic;
    using System.Linq;

    internal enum AccessType {
        Read,
        Write,
    }

    internal struct TypeAccess {

        public System.Type TypeName { get; set; }
        public AccessType Access { get; set; }

    }

    internal class BuilderNode {

        public BECS.FeaturesGraph.Nodes.SystemNode SourceNode;
        public List<TypeAccess> Accesses { get; set; } = new();
        public List<BuilderNode> Children { get; set; } = new();

        // Для визуализации
        public int X { get; set; }
        public int Y { get; set; }

    }

    internal class GraphBuilder {

        /*[MenuItem("ME.BECS/Internal/Gen Graph")]
        public static void Test() {
            if (UnityEditor.Selection.activeObject is SystemsGraph g) {
                GraphBuilder.Build(g);
            }
        }*/

        public static void Build(SystemsGraph graph) {

            BECS.FeaturesGraph.Nodes.SystemNode[] FilterNodes(BaseNode x) {
                if (x is BECS.FeaturesGraph.Nodes.SystemNode systemNode) {
                    return new[] { systemNode };
                } else if (x is BECS.FeaturesGraph.Nodes.GraphNode graphNode) {
                    return graphNode.graphValue.nodes.SelectMany(y => FilterNodes(y)).ToArray();
                }
                return System.Array.Empty<BECS.FeaturesGraph.Nodes.SystemNode>();
            }
            
            var type = System.Type.GetType("ME.BECS.Editor.StaticMethods, ME.BECS.Gen.Editor");
            
            var arr = graph.nodes.SelectMany(x => {
                return FilterNodes(x);
            }).Select(x => {
                var list = (List<ComponentDependencyGraphInfo>)type.GetMethod("GetSystemComponentsDependencies").Invoke(null, new object[] { x.system.GetType() });
                var accesses = list.Select(c => new TypeAccess() {
                    Access = ((RefOp)c.op == RefOp.ReadOnly) ? AccessType.Read : AccessType.Write,
                    TypeName = c.type,
                }).ToList();
                accesses.Add(new TypeAccess() {
                    Access = AccessType.Write,
                    TypeName = x.system.GetType(),
                });
                /*try {
                    var listSystems = (HashSet<System.Type>)type.GetMethod("GetSystemDependencies").Invoke(null, new object[] { x.system.GetType() });
                    if (listSystems != null) {
                        foreach (var item in listSystems) {
                            accesses.Add(new TypeAccess() {
                                Access = AccessType.Read,
                                TypeName = item,
                            });
                        }
                    }
                } catch (System.Exception ex) {}*/

                return new BuilderNode() {
                    SourceNode = x,
                    Accesses = accesses,
                };
            }).ToList();

            var (sorted, deps) = Build(arr);
            var maxX = 0;
            foreach (var node in sorted) {
                if (node.X > maxX) {
                    maxX = node.X;
                }
            }

            var nodeSizeX = 400f;
            var nodeSizeY = 200f;

            var instance = ScriptableObject.CreateInstance<SystemsGraph>();
            instance.name = $"{graph.name}-Gen";
            instance.nodes = new List<BaseNode>();
            {
                var sysNode = new StartNode() {
                    position = new Rect(-nodeSizeX, 0f, 100f, 80f),
                    graph = instance,
                    GUID = "StartNode",
                };
                sysNode.OnNodeCreated();
                instance.AddNode(sysNode);
            }
            {
                var sysNode = new ExitNode() {
                    position = new Rect(maxX * nodeSizeX + nodeSizeX, 0f, 100f, 80f),
                    graph = instance,
                    GUID = "ExitNode",
                };
                sysNode.OnNodeCreated();
                instance.AddNode(sysNode);
            }
            var nodesKey = new Dictionary<Vector2Int, BaseNode>();
            var countColumns = new Dictionary<int, int>();
            foreach (var node in sorted) {
                var sysNode = new SystemNode() {
                    position = new Rect(node.X * nodeSizeX, node.Y * nodeSizeY, 100f, 80f),
                    system = node.SourceNode.system,
                    graph = instance,
                };
                sysNode.OnNodeCreated();
                instance.AddNode(sysNode);
                nodesKey.Add(new Vector2Int(node.X, node.Y), sysNode);
                if (countColumns.TryGetValue(node.X, out var count) == true) {
                    ++countColumns[node.X];
                } else {
                    countColumns.Add(node.X, 1);
                }
            }
            instance.OnAfterDeserialize();

            var relays = new Dictionary<int, RelayNode>();
            var startNode = instance.nodes[0];
            var exitNode = instance.nodes[1];
            foreach (var node in sorted) {
                var x = node.X;
                if (x == 0) {
                    instance.Connect(
                        nodesKey[new Vector2Int(node.X, node.Y)].GetPort("inputNodes", null),
                        startNode.GetPort("output", null)
                    );
                }
                if (x == maxX) {
                    instance.Connect(
                        exitNode.GetPort("inputNodes", null),
                        nodesKey[new Vector2Int(node.X, node.Y)].GetPort("outputNodes", null)
                    );
                }

                countColumns.TryGetValue(x - 1, out var prevCount);
                countColumns.TryGetValue(x, out var count);
                countColumns.TryGetValue(x + 1, out var nextCount);
                if (prevCount >= 2 && count >= 2) {
                    if (relays.TryGetValue(x - 1, out var relay) == true) {
                        
                    } else {
                        relay = new RelayNode() {
                            position = new Rect((x - 1) * nodeSizeX + nodeSizeX * 0.5f, 0f, 100f, 80f),
                            graph = instance,
                            GUID = $"RelayNode{x - 1}",
                        };
                        relay.OnNodeCreated();
                        instance.AddNode(relay);
                        relays.Add(x - 1, relay);
                    }
                    var kSource = nodesKey[new Vector2Int(node.X, node.Y)];
                    instance.Connect(
                        kSource.GetPort("inputNodes", null),
                        relay.GetPort("output", "0")
                    );
                }
                if (count >= 2 && nextCount >= 2) {
                    if (relays.TryGetValue(x, out var relay) == true) {
                        
                    } else {
                        relay = new RelayNode() {
                            position = new Rect(x * nodeSizeX + nodeSizeX * 0.5f, 0f, 100f, 80f),
                            graph = instance,
                            GUID = $"RelayNode{x}",
                        };
                        relay.OnNodeCreated();
                        instance.AddNode(relay);
                        relays.Add(x, relay);
                    }

                    var kSource = nodesKey[new Vector2Int(node.X, node.Y)];
                    instance.Connect(
                        relay.GetPort("input", "0"),
                        kSource.GetPort("outputNodes", null)
                    );
                } else {
                    var next = x + 1;
                    var kSource = nodesKey[new Vector2Int(node.X, node.Y)];
                    foreach (var n in sorted) {
                        if (n.X == next) {
                            var kTarget = nodesKey[new Vector2Int(n.X, n.Y)];
                            instance.Connect(
                                kTarget.GetPort("inputNodes", null),
                                kSource.GetPort("outputNodes", null)
                            );
                        }
                    }
                }
            }

            /*
            foreach (var node in sorted) {
                BuilderNode n = null;
                foreach (var dep in deps) {
                    if (dep.X == node.X && dep.Y == node.Y) {
                        n = node;
                        break;
                    }
                }
                if (n == null) continue;
                var kSource = nodesKey[new Vector2Int(node.X, node.Y)];
                foreach (var k in n.Children) {
                    var kTarget = nodesKey[new Vector2Int(k.X, k.Y)];
                    instance.Connect(
                        new NodePort(kTarget, "inputNodes", new PortData() { }),
                        new NodePort(kSource, "outputNodes", new PortData() { })
                    );
                }
            }*/

            var path = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(AssetDatabase.GetAssetPath(graph)), $"{instance.name}.asset");
            AssetDatabase.CreateAsset(instance, path);

        }

        public static (List<BuilderNode>, List<BuilderNode>) Build(List<BuilderNode> systems) {
            
            var builder = new GraphBuilder();
            var graph = builder.BuildGraph(systems);
            var sorted = builder.TopologicalSort(graph);
            builder.AssignCoordinates(graph);
            
            UnityEngine.Debug.Log("Порядок выполнения систем:");
            foreach (var sys in sorted) {
                UnityEngine.Debug.Log($"{sys.SourceNode.name} (X={sys.X}, Y={sys.Y})");
            }

            UnityEngine.Debug.Log("\nChildren (зависимости):");
            foreach (var sys in graph) {
                UnityEngine.Debug.Log($"{sys.SourceNode.name} -> [{string.Join(", ", sys.Children.Select(c => c.SourceNode.name))}]");
            }

            return (sorted, graph);

        }

        private bool CreatesCycle(BuilderNode from, BuilderNode to) {
            var stack = new Stack<BuilderNode>();
            var visited = new HashSet<BuilderNode>();
            stack.Push(to);

            while (stack.Count > 0) {
                var current = stack.Pop();
                if (current == from) {
                    return true;
                }

                if (!visited.Add(current)) {
                    continue;
                }

                foreach (var child in current.Children) {
                    stack.Push(child);
                }
            }

            return false;
        }

        public List<BuilderNode> BuildGraph(List<BuilderNode> systems) {
            foreach (var sys in systems) {
                foreach (var other in systems) {
                    if (sys == other) {
                        continue;
                    }

                    foreach (var a in sys.Accesses) {
                        foreach (var b in other.Accesses) {
                            if (a.TypeName == b.TypeName) {
                                if (a.Access == AccessType.Read && b.Access == AccessType.Write) {
                                    if (!other.Children.Contains(sys) && !this.CreatesCycle(other, sys)) {
                                        other.Children.Add(sys);
                                    }
                                } else if (a.Access == AccessType.Write && b.Access == AccessType.Read) {
                                    if (!sys.Children.Contains(other) && !this.CreatesCycle(sys, other)) {
                                        sys.Children.Add(other);
                                    }
                                }
                            }
                        }
                    }
                }
            }

            return systems;
        }

        public List<BuilderNode> TopologicalSort(List<BuilderNode> systems) {
            var result = new List<BuilderNode>();
            var visited = new HashSet<BuilderNode>();
            var temp = new HashSet<BuilderNode>();
            var hasCycle = false;

            void Visit(BuilderNode n) {
                if (temp.Contains(n)) {
                    hasCycle = true;
                    return;
                }

                if (!visited.Contains(n)) {
                    temp.Add(n);
                    foreach (var child in n.Children) {
                        Visit(child);
                    }

                    temp.Remove(n);
                    visited.Add(n);
                    result.Add(n);
                }
            }

            foreach (var sys in systems) {
                Visit(sys);
            }

            if (hasCycle) {
                return systems;
            }

            result.Reverse();
            return result;
        }

        public void AssignCoordinates(List<BuilderNode> systems) {
            // X = уровень глубины
            Dictionary<BuilderNode, int> depth = new();

            int GetDepth(BuilderNode node) {
                if (depth.ContainsKey(node)) {
                    return depth[node];
                }

                if (!systems.Any(s => s.Children.Contains(node))) // нет родителей
                {
                    depth[node] = 0;
                } else {
                    depth[node] = systems
                                  .Where(s => s.Children.Contains(node))
                                  .Max(s => GetDepth(s) + 1);
                }

                return depth[node];
            }

            foreach (var sys in systems) {
                GetDepth(sys);
            }

            // Y = порядковый номер внутри уровня
            var groups = systems.GroupBy(s => depth[s]).OrderBy(g => g.Key);
            foreach (var g in groups) {
                var y = 0;
                foreach (var sys in g) {
                    sys.X = g.Key;
                    sys.Y = y++;
                }
            }
        }

    }

}