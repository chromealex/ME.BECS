namespace ME.BECS.Editor.FeaturesGraph {

    using System.Collections.Generic;
    using System.Linq;
    using UnityEngine;

    /// <summary>Deterministic longest-path layering. Parallel siblings share a column.</summary>
    internal static class FeaturesGraphStudioLayout {
        internal static Vector2 ClampScroll(Vector2 offset, Vector2 content, Vector2 viewport) {
            float Clamp(float value, float extent, float visible) {
                if (float.IsNaN(value) || float.IsInfinity(value) || float.IsNaN(extent) || float.IsNaN(visible)) return 0;
                return Mathf.Clamp(value, 0, Mathf.Max(0, extent - visible));
            }
            return new Vector2(Clamp(offset.x, content.x, viewport.x), Clamp(offset.y, content.y, viewport.y));
        }

        internal static void AlignDisconnectedToTargets(int[] layers, IEnumerable<(int source, int target)> edges, IEnumerable<int> starts) {
            var outgoing = new List<int>[layers.Length];
            for (var index = 0; index < layers.Length; ++index) outgoing[index] = new List<int>();
            foreach (var edge in edges.Distinct()) {
                if (edge.source >= 0 && edge.source < layers.Length && edge.target >= 0 && edge.target < layers.Length)
                    outgoing[edge.source].Add(edge.target);
            }
            var reachable = new HashSet<int>(); var pending = new Stack<int>(starts);
            while (pending.Count > 0) {
                var node = pending.Pop();
                if (node < 0 || node >= layers.Length || !reachable.Add(node)) continue;
                foreach (var target in outgoing[node]) pending.Push(target);
            }
            // Traverse backwards so detached chains stay immediately before their
            // destination, rather than being treated as extra graph entry points.
            var reverse = Enumerable.Range(0, layers.Length).OrderByDescending(index => layers[index]).ToArray();
            foreach (var node in reverse) {
                if (reachable.Contains(node) || outgoing[node].Count == 0) continue;
                layers[node] = System.Math.Max(0, outgoing[node].Min(target => layers[target]) - 1);
            }
        }

        internal static int[] GetLayers(int count, IEnumerable<(int source, int target)> edges, out int blocked) {
            var outgoing = new List<int>[count];
            var degree = new int[count];
            var layers = new int[count];
            for (var i = 0; i < count; ++i) outgoing[i] = new List<int>();
            var unique = new HashSet<(int, int)>();
            foreach (var edge in edges) {
                if (edge.source < 0 || edge.target < 0 || edge.source >= count || edge.target >= count || !unique.Add(edge)) continue;
                outgoing[edge.source].Add(edge.target);
                ++degree[edge.target];
            }
            var ready = new SortedSet<int>();
            for (var i = 0; i < count; ++i) if (degree[i] == 0) ready.Add(i);
            var processed = new bool[count];
            var max = 0;
            while (ready.Count > 0) {
                var source = ready.Min;
                ready.Remove(source);
                processed[source] = true;
                foreach (var target in outgoing[source]) {
                    if (layers[target] < layers[source] + 1) layers[target] = layers[source] + 1;
                    if (max < layers[target]) max = layers[target];
                    if (--degree[target] == 0) ready.Add(target);
                }
            }
            // Malformed/cyclic assets remain visible; never hang or silently drop their nodes.
            blocked = 0;
            for (var i = 0; i < count; ++i) if (!processed[i]) { layers[i] = max + 1; ++blocked; }
            return layers;
        }
    }
}
