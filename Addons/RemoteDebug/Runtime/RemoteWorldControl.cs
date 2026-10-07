#if BECS_REMOTE_DEBUG && !UNITY_WEBGL
using System;
using scg = System.Collections.Generic;
using UnityEngine;

namespace ME.BECS.RemoteDebug {
    // Pause automatic Unity callbacks of a world's owner, while the debug server keeps updating.
    internal sealed unsafe class RemoteWorldControl {
        private struct PausedWorld {
            public World world;
            public BaseWorldInitializer initializer;
        }
        private readonly scg.Dictionary<ushort, PausedWorld> paused = new();

        private static BaseWorldInitializer FindOwner(World world) {
            var initializer = WorldInitializers.GetByWorldName(world.Name);
            if (Matches(initializer, world)) return initializer;
            foreach (var item in UnityEngine.Object.FindObjectsByType<BaseWorldInitializer>(FindObjectsInactive.Include, FindObjectsSortMode.None)) {
                if (Matches(item, world)) return item;
            }
            return null;
        }
        private static bool Matches(BaseWorldInitializer initializer, World world) {
            return initializer != null && initializer.world.isCreated && initializer.world.id == world.id && initializer.world.state.ptr == world.state.ptr;
        }
        private bool OwnsPause(World world, BaseWorldInitializer initializer) {
            return this.paused.TryGetValue(world.id, out var entry) && entry.initializer == initializer && entry.world.state.ptr == world.state.ptr;
        }
        internal void GetStatus(World world, out bool isPaused, out bool canPause, out bool canPlay) {
            var initializer = FindOwner(world);
            isPaused = initializer != null && !initializer.isActiveAndEnabled;
            canPause = initializer != null && initializer.isActiveAndEnabled;
            canPlay = initializer != null && initializer.gameObject.activeInHierarchy && !initializer.enabled && this.OwnsPause(world, initializer);
        }
        internal void Execute(World world, string action) {
            if (!world.isCreated) throw new ArgumentException("World no longer exists");
            var initializer = FindOwner(world);
            if (initializer == null) throw new ArgumentException("This world has no initializer to pause");
            if (action == "pause") {
                if (this.OwnsPause(world, initializer) && !initializer.enabled) return;
                if (!initializer.isActiveAndEnabled) throw new ArgumentException("World initializer is already inactive");
                Worlds.GetEndTickHandle(world.id).Complete();
                this.paused[world.id] = new PausedWorld { world = world, initializer = initializer };
                initializer.enabled = false;
            } else if (action == "play") {
                if (!this.OwnsPause(world, initializer)) throw new ArgumentException("World was not paused by remote debug");
                if (!initializer.gameObject.activeInHierarchy) throw new ArgumentException("World GameObject is inactive");
                initializer.enabled = true;
                this.paused.Remove(world.id);
            } else throw new ArgumentException("Unknown world action");
        }
        internal void Restore() {
            foreach (var entry in this.paused.Values) {
                if (Matches(entry.initializer, entry.world) && !entry.initializer.enabled) entry.initializer.enabled = true;
            }
            this.paused.Clear();
        }
    }
}
#endif
