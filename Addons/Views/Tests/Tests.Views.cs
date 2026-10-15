using NUnit.Framework;

namespace ME.BECS.Tests {
    
    using BECS.Views;
    using BECS.Transforms;

    public unsafe class Tests_Views {

        [Test]
        public void ParticlesInterpolationWithEqualTicksKeepsFinitePose() {
            var go = new UnityEngine.GameObject("Particle interpolation");
            var world = World.Create(); TestInitialize(in world);
            var properties = ViewsModuleProperties.Default;
            properties.interpolateState = true;
            var views = UnsafeViewsModule<EntityView>.Create(3u, ref world, new ParticlesProvider(), WorldProperties.Default.stateProperties.EntitiesCapacity, properties);
            try {
                var source = views.RegisterViewSource(go.AddComponent<DefaultView>(), checkPrefab: false);
                var ent = world.NewEnt(); ent.Set<TransformAspect>(); SetTestPosition(ent, 12);
                ent.Get<WorldMatrixComponent>().isTickCalculated = true;
                ent.InstantiateView(source); Batches.Apply(world);
                views.data.ptr->beginFrameState.ptr->state = world.state;
                views.data.ptr->beginFrameState.ptr->tickTime = 0.1f;
                views.data.ptr->beginFrameState.ptr->timeSinceStart = world.CurrentTick * 0.1d;
                views.Update(0.016f).Complete();
                Assert.AreEqual(0f, views.data.ptr->interpolationFactor);
                AssertProviderOwner(views, 3u, ent, 12);
                var system = (ParticlesProvider.ParticleSystemInfo)ProviderMap(views, "systemForPrefab")[source.prefabId];
                var particles = new UnityEngine.ParticleSystem.Particle[1];
                Assert.AreEqual(1, system.particleSystem.GetParticles(particles));
                Assert.AreEqual(new UnityEngine.Vector3(12, 0, 0), particles[0].position);
                Assert.IsFalse(float.IsNaN(particles[0].rotation3D.x));
            } finally {
                views.Dispose(); world.Dispose(); UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [TestCase(0f, 90f, 0f)]
        [TestCase(20f, 35f, 10f)]
        [TestCase(-75f, 140f, 55f)]
        public void ParticlesProxyRotationMatchesWorldOrientation(float x, float y, float z) {
            var go = new UnityEngine.GameObject("Particle rotation");
            var world = World.Create(); TestInitialize(in world);
            var properties = ViewsModuleProperties.Default; properties.interpolateState = false;
            var views = UnsafeViewsModule<EntityView>.Create(3u, ref world, new ParticlesProvider(), WorldProperties.Default.stateProperties.EntitiesCapacity, properties);
            try {
                var source = views.RegisterViewSource(go.AddComponent<DefaultView>(), checkPrefab: false);
                var ent = world.NewEnt(); ent.Set<TransformAspect>();
                var expected = UnityEngine.Quaternion.Euler(x, y, z);
                var matrix = Unity.Mathematics.float4x4.TRS(new Unity.Mathematics.float3(5, 0, 0), expected, new Unity.Mathematics.float3(1));
                #if FIXED_POINT
                ent.Get<WorldMatrixComponent>().value = (ME.BECS.FixedPoint.float4x4)matrix;
                #else
                ent.Get<WorldMatrixComponent>().value = matrix;
                #endif
                ent.InstantiateView(source); Batches.Apply(world); views.Update(0.016f).Complete();
                var system = (ParticlesProvider.ParticleSystemInfo)ProviderMap(views, "systemForPrefab")[source.prefabId];
                var particles = new UnityEngine.ParticleSystem.Particle[1];
                Assert.AreEqual(1, system.particleSystem.GetParticles(particles));
                Assert.Less(UnityEngine.Quaternion.Angle(expected, UnityEngine.Quaternion.Euler(particles[0].rotation3D)), 0.1f);
            } finally {
                views.Dispose(); world.Dispose(); UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [TestCase(0f, 0f, 0f, 0f, true)]
        [TestCase(15f, 25f, 5f, 0f, true)]
        [TestCase(15f, 25f, 5f, 40f, true)]
        [TestCase(0f, 0f, 35f, 40f, false)]
        public void ParticlesMeshSubEmitterInheritsOwnerRotation(float startX, float startY, float startZ, float localYaw, bool rotation3D) {
            var go = new UnityEngine.GameObject("Mesh particle source");
            var sourceGo = new UnityEngine.GameObject("Authored emitter");
            sourceGo.transform.SetParent(go.transform, worldPositionStays: false);
            sourceGo.transform.localRotation = UnityEngine.Quaternion.Euler(0f, localYaw, 0f);
            sourceGo.transform.localPosition = new UnityEngine.Vector3(2f, 0f, 3f);
            var sourceSystem = sourceGo.AddComponent<UnityEngine.ParticleSystem>();
            sourceSystem.Stop(true, UnityEngine.ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = sourceSystem.main;
            main.playOnAwake = false; main.loop = false; main.startLifetime = 10f; main.startSpeed = 0f;
            main.startRotation3D = rotation3D;
            main.startRotationX = startX * UnityEngine.Mathf.Deg2Rad;
            main.startRotationY = startY * UnityEngine.Mathf.Deg2Rad;
            main.startRotationZ = startZ * UnityEngine.Mathf.Deg2Rad;
            var shape = sourceSystem.shape; shape.enabled = false;
            var emission = sourceSystem.emission;
            emission.rateOverTime = 0f; emission.SetBursts(new[] { new UnityEngine.ParticleSystem.Burst(rotation3D == true ? 0f : 0.01f, 1) });
            var renderer = sourceSystem.GetComponent<UnityEngine.ParticleSystemRenderer>();
            renderer.renderMode = UnityEngine.ParticleSystemRenderMode.Mesh;
            renderer.alignment = UnityEngine.ParticleSystemRenderSpace.Local;
            var sourceMesh = new UnityEngine.Mesh();
            sourceMesh.vertices = new[] { UnityEngine.Vector3.zero, UnityEngine.Vector3.right, UnityEngine.Vector3.up * 2f, UnityEngine.Vector3.forward * 3f };
            sourceMesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            sourceMesh.RecalculateNormals(); renderer.mesh = sourceMesh;
            var actualMesh = new UnityEngine.Mesh(); var referenceMesh = new UnityEngine.Mesh();
            var cameraGo = new UnityEngine.GameObject("Particle bake camera");
            var camera = cameraGo.AddComponent<UnityEngine.Camera>(); camera.enabled = false;
            UnityEngine.GameObject referenceGo = null; UnityEngine.GameObject bakedParticleGo = null;
            var world = World.Create(); TestInitialize(in world);
            var properties = ViewsModuleProperties.Default; properties.interpolateState = false;
            var views = UnsafeViewsModule<EntityView>.Create(3u, ref world, new ParticlesProvider(), WorldProperties.Default.stateProperties.EntitiesCapacity, properties);
            try {
                var source = views.RegisterViewSource(go.AddComponent<DefaultView>(), checkPrefab: false);
                var ent = world.NewEnt(); ent.Set<TransformAspect>();
                var expected = UnityEngine.Quaternion.Euler(20f, 35f, 10f);
                referenceGo = UnityEngine.Object.Instantiate(go, UnityEngine.Vector3.zero, expected);
                var referenceSystem = referenceGo.GetComponentInChildren<UnityEngine.ParticleSystem>();
                referenceSystem.Play(withChildren: true);
                referenceSystem.Simulate(0.02f, withChildren: true, restart: false, fixedTimeStep: false);
                var matrix = Unity.Mathematics.float4x4.TRS(Unity.Mathematics.float3.zero, expected, new Unity.Mathematics.float3(1));
                #if FIXED_POINT
                ent.Get<WorldMatrixComponent>().value = (ME.BECS.FixedPoint.float4x4)matrix;
                #else
                ent.Get<WorldMatrixComponent>().value = matrix;
                #endif
                ent.InstantiateView(source); Batches.Apply(world); views.Update(0.016f).Complete();
                var system = (ParticlesProvider.ParticleSystemInfo)ProviderMap(views, "systemForPrefab")[source.prefabId];
                system.particleSystem.Simulate(0.02f, withChildren: true, restart: false, fixedTimeStep: false);
                var child = System.Array.Find(system.particleSystem.GetComponentsInChildren<UnityEngine.ParticleSystem>(),
                    candidate => candidate != system.particleSystem && candidate.GetComponent<UnityEngine.ParticleSystemRenderer>().renderMode == UnityEngine.ParticleSystemRenderMode.Mesh);
                var particles = new UnityEngine.ParticleSystem.Particle[4];
                Assert.AreEqual(1, child.GetParticles(particles));
                var expectedParticleRotation = expected * sourceGo.transform.localRotation * UnityEngine.Quaternion.Euler(startX, startY, startZ);
                Assert.Less(UnityEngine.Quaternion.Angle(expectedParticleRotation, UnityEngine.Quaternion.Euler(particles[0].rotation3D)), 0.1f);
                Assert.Less(UnityEngine.Vector3.Distance(expected * sourceGo.transform.localPosition, particles[0].position), 0.01f);
                var bakeOptions = UnityEngine.ParticleSystemBakeMeshOptions.BakeRotationAndScale | UnityEngine.ParticleSystemBakeMeshOptions.BakePosition;
                var childRenderer = child.GetComponent<UnityEngine.ParticleSystemRenderer>();
                Assert.IsNotNull(childRenderer.mesh, "Shared mesh source must retain its mesh.");
                Assert.IsTrue(childRenderer.enabled, "Shared mesh renderer must remain enabled.");
                Assert.Greater(particles[0].GetCurrentSize(child), 0f, "Shared mesh particle size.");
                referenceSystem.GetComponent<UnityEngine.ParticleSystemRenderer>().BakeMesh(referenceMesh, camera, bakeOptions);
                // Compare the actual returned particle through an equivalent standalone renderer.
                bakedParticleGo = UnityEngine.Object.Instantiate(child.gameObject, UnityEngine.Vector3.zero, UnityEngine.Quaternion.identity);
                var bakedSystem = bakedParticleGo.GetComponent<UnityEngine.ParticleSystem>();
                bakedSystem.Stop(true, UnityEngine.ParticleSystemStopBehavior.StopEmittingAndClear);
                var bakedEmission = bakedSystem.emission; bakedEmission.enabled = false;
                bakedSystem.Play(withChildren: false); bakedSystem.SetParticles(particles, 1);
                bakedSystem.GetComponent<UnityEngine.ParticleSystemRenderer>().BakeMesh(actualMesh, camera, bakeOptions);
                Assert.AreEqual(referenceMesh.vertexCount, actualMesh.vertexCount,
                    $"Shared mesh: playing={child.isPlaying}, stopped={child.isStopped}, meshCount={childRenderer.meshCount}, size={particles[0].GetCurrentSize(child)}");
                Assert.Greater(actualMesh.vertexCount, 0);
                var expectedVertices = referenceMesh.vertices; var actualVertices = actualMesh.vertices;
                for (int i = 0; i < actualVertices.Length; ++i) {
                    Assert.Less(UnityEngine.Vector3.Distance(expectedVertices[i], actualVertices[i]), 0.01f, $"Baked vertex {i}");
                }
                system.particleSystem.Simulate(0.02f, withChildren: true, restart: false, fixedTimeStep: false);
                views.Update(0.016f).Complete();
                Assert.AreEqual(1, child.GetParticles(particles), "Updating a retained proxy must not replay the birth burst.");
                var nextRotation = expected * UnityEngine.Quaternion.Euler(0f, 40f, 0f);
                matrix = Unity.Mathematics.float4x4.TRS(Unity.Mathematics.float3.zero, nextRotation, new Unity.Mathematics.float3(1));
                #if FIXED_POINT
                ent.Get<WorldMatrixComponent>().value = (ME.BECS.FixedPoint.float4x4)matrix;
                #else
                ent.Get<WorldMatrixComponent>().value = matrix;
                #endif
                views.Update(0.02f).Complete();
                Assert.AreEqual(1, system.particleSystem.GetParticles(particles));
                var expectedVelocity = (nextRotation * sourceGo.transform.localPosition - expected * sourceGo.transform.localPosition) / 0.02f;
                Assert.Less(UnityEngine.Vector3.Distance(expectedVelocity, particles[0].velocity), 0.02f,
                    "An offset emitter must inherit angular movement even when the owner center is stationary.");
            } finally {
                views.Dispose(); world.Dispose(); UnityEngine.Object.DestroyImmediate(go);
                if (referenceGo != null) UnityEngine.Object.DestroyImmediate(referenceGo);
                if (bakedParticleGo != null) UnityEngine.Object.DestroyImmediate(bakedParticleGo);
                UnityEngine.Object.DestroyImmediate(sourceMesh); UnityEngine.Object.DestroyImmediate(actualMesh); UnityEngine.Object.DestroyImmediate(referenceMesh);
                UnityEngine.Object.DestroyImmediate(cameraGo);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ParticlesMeshBurstComposesEachNativeRotationAndKeepsExistingOwners(bool rotation3D) {
            var go = new UnityEngine.GameObject("Burst mesh source");
            var emitterGo = new UnityEngine.GameObject("Mesh emitter"); emitterGo.transform.SetParent(go.transform, false);
            emitterGo.transform.localRotation = UnityEngine.Quaternion.Euler(0f, 270f, 270f);
            var sourceSystem = emitterGo.AddComponent<UnityEngine.ParticleSystem>();
            sourceSystem.Stop(true, UnityEngine.ParticleSystemStopBehavior.StopEmittingAndClear);
            sourceSystem.useAutoRandomSeed = false; sourceSystem.randomSeed = 123u;
            var main = sourceSystem.main; main.playOnAwake = false; main.loop = false; main.startLifetime = 10f; main.startSpeed = 0f;
            main.startLifetime = new UnityEngine.ParticleSystem.MinMaxCurve(1f, UnityEngine.AnimationCurve.Linear(0f, 10f, 1f, 1f));
            main.startRotation3D = rotation3D;
            // In 1D mode these authored X/Y curves must remain inactive.
            main.startRotationX = new UnityEngine.ParticleSystem.MinMaxCurve(-1f, 1f);
            main.startRotationY = new UnityEngine.ParticleSystem.MinMaxCurve(0f, UnityEngine.Mathf.PI);
            main.startRotationZ = new UnityEngine.ParticleSystem.MinMaxCurve(-2f, 2f);
            var emission = sourceSystem.emission; emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new UnityEngine.ParticleSystem.Burst(0f, 8) });
            var shape = sourceSystem.shape; shape.enabled = true; shape.radius = 0f;
            sourceSystem.GetComponent<UnityEngine.ParticleSystemRenderer>().renderMode = UnityEngine.ParticleSystemRenderMode.Mesh;
            sourceSystem.GetComponent<UnityEngine.ParticleSystemRenderer>().alignment = UnityEngine.ParticleSystemRenderSpace.Local;
            var referenceGo = UnityEngine.Object.Instantiate(go);
            var reference = referenceGo.GetComponentInChildren<UnityEngine.ParticleSystem>();
            var referenceEmission = reference.emission; referenceEmission.enabled = false; reference.Play(false); reference.Emit(8);
            var expectedParticles = new UnityEngine.ParticleSystem.Particle[8];
            Assert.AreEqual(8, reference.GetParticles(expectedParticles));
            var world = World.Create(); TestInitialize(in world);
            var properties = ViewsModuleProperties.Default; properties.interpolateState = false;
            var views = UnsafeViewsModule<EntityView>.Create(3u, ref world, new ParticlesProvider(), WorldProperties.Default.stateProperties.EntitiesCapacity, properties);
            try {
                var source = views.RegisterViewSource(go.AddComponent<DefaultView>(), checkPrefab: false);
                var firstRotation = UnityEngine.Quaternion.Euler(20f, 35f, 10f);
                var first = world.NewEnt(); first.Set<TransformAspect>();
                var matrix = Unity.Mathematics.float4x4.TRS(Unity.Mathematics.float3.zero, firstRotation, new Unity.Mathematics.float3(1));
                #if FIXED_POINT
                first.Get<WorldMatrixComponent>().value = (ME.BECS.FixedPoint.float4x4)matrix;
                #else
                first.Get<WorldMatrixComponent>().value = matrix;
                #endif
                first.InstantiateView(source); Batches.Apply(world); views.Update(0.02f).Complete();
                var system = (ParticlesProvider.ParticleSystemInfo)ProviderMap(views, "systemForPrefab")[source.prefabId];
                var child = System.Array.Find(system.particleSystem.GetComponentsInChildren<UnityEngine.ParticleSystem>(), candidate => candidate.name == "Mesh emitter");
                var actual = new UnityEngine.ParticleSystem.Particle[16];
                Assert.AreEqual(8, child.GetParticles(actual));
                for (int i = 0; i < 8; ++i) {
                    var expected = firstRotation * emitterGo.transform.localRotation * UnityEngine.Quaternion.Euler(expectedParticles[i].rotation3D);
                    Assert.Less(UnityEngine.Quaternion.Angle(expected, UnityEngine.Quaternion.Euler(actual[i].rotation3D)), 0.1f, $"Native sample {i}");
                }
                child.time = 2.5f;
                var second = world.NewEnt(); second.Set<TransformAspect>(); SetTestPosition(second, 20); second.InstantiateView(source);
                Batches.Apply(world); views.Update(0.02f).Complete();
                var retained = new UnityEngine.ParticleSystem.Particle[16];
                Assert.AreEqual(16, child.GetParticles(retained));
                for (int i = 8; i < 16; ++i) Assert.AreEqual(10f, retained[i].startLifetime, 0.01f, "Each burst samples Main curves at effect time zero.");
                for (int i = 0; i < 8; ++i) {
                    Assert.AreEqual(actual[i].rotation3D, retained[i].rotation3D, "A new owner must not rotate existing particles.");
                    Assert.AreEqual(actual[i].randomSeed, retained[i].randomSeed);
                }
                views.Update(0.02f).Complete();
                Assert.AreEqual(16, child.GetParticles(retained), "A retained owner must not emit another initial burst.");
                // Assignment and despawn must not restart or move a finished world-space burst.
                var destination = world.NewEnt(); destination.Set<TransformAspect>(); SetTestPosition(destination, 1);
                Assert.IsTrue(destination.AssignView(first)); Batches.Apply(world); views.Update(0.02f).Complete();
                Assert.AreEqual(16, child.GetParticles(retained));
                for (int i = 0; i < 8; ++i) {
                    Assert.AreEqual(actual[i].position, retained[i].position, "AssignView must preserve emitted world-space particles.");
                }
                destination.DestroyView(); Batches.Apply(world); views.Update(0.02f).Complete();
                var afterDespawn = new UnityEngine.ParticleSystem.Particle[16];
                Assert.AreEqual(16, child.GetParticles(afterDespawn));
                for (int i = 0; i < 8; ++i) Assert.AreEqual(retained[i].position, afterDespawn[i].position, "Despawn must stop inherited owner movement.");
            } finally {
                views.Dispose(); world.Dispose(); UnityEngine.Object.DestroyImmediate(go); UnityEngine.Object.DestroyImmediate(referenceGo);
            }
        }

        [Test]
        public void ParticlesMeshKeepsPerParticleRandomZRotation() {
            var go = new UnityEngine.GameObject("Random mesh rotation");
            var sourceSystem = go.AddComponent<UnityEngine.ParticleSystem>();
            sourceSystem.Stop(true, UnityEngine.ParticleSystemStopBehavior.StopEmittingAndClear);
            sourceSystem.useAutoRandomSeed = false; sourceSystem.randomSeed = 123u;
            var main = sourceSystem.main; main.playOnAwake = false; main.startLifetime = 10f; main.startSpeed = 0f;
            main.startRotation3D = true;
            main.startRotationX = 15f * UnityEngine.Mathf.Deg2Rad; main.startRotationY = 25f * UnityEngine.Mathf.Deg2Rad;
            main.startRotationZ = new UnityEngine.ParticleSystem.MinMaxCurve(-1f, 1f);
            var shape = sourceSystem.shape; shape.enabled = false;
            var emission = sourceSystem.emission; emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new UnityEngine.ParticleSystem.Burst(0f, 8) });
            sourceSystem.GetComponent<UnityEngine.ParticleSystemRenderer>().renderMode = UnityEngine.ParticleSystemRenderMode.Mesh;
            var world = World.Create(); TestInitialize(in world);
            var properties = ViewsModuleProperties.Default; properties.interpolateState = false;
            var views = UnsafeViewsModule<EntityView>.Create(3u, ref world, new ParticlesProvider(), WorldProperties.Default.stateProperties.EntitiesCapacity, properties);
            try {
                var source = views.RegisterViewSource(go.AddComponent<DefaultView>(), checkPrefab: false);
                var ent = world.NewEnt(); ent.Set<TransformAspect>();
                var rotation = UnityEngine.Quaternion.Euler(20f, 35f, 10f);
                var matrix = Unity.Mathematics.float4x4.TRS(Unity.Mathematics.float3.zero, rotation, new Unity.Mathematics.float3(1));
                #if FIXED_POINT
                ent.Get<WorldMatrixComponent>().value = (ME.BECS.FixedPoint.float4x4)matrix;
                #else
                ent.Get<WorldMatrixComponent>().value = matrix;
                #endif
                ent.InstantiateView(source); Batches.Apply(world); views.Update(0.02f).Complete();
                var system = (ParticlesProvider.ParticleSystemInfo)ProviderMap(views, "systemForPrefab")[source.prefabId];
                system.particleSystem.Simulate(0.02f, withChildren: true, restart: false, fixedTimeStep: false);
                var child = System.Array.Find(system.particleSystem.GetComponentsInChildren<UnityEngine.ParticleSystem>(),
                    candidate => candidate != system.particleSystem && candidate.GetComponent<UnityEngine.ParticleSystemRenderer>().renderMode == UnityEngine.ParticleSystemRenderMode.Mesh);
                var particles = new UnityEngine.ParticleSystem.Particle[8];
                Assert.AreEqual(8, child.GetParticles(particles));
                var expectedForward = rotation * UnityEngine.Quaternion.Euler(15f, 25f, 0f) * UnityEngine.Vector3.forward;
                var differentRotations = false;
                for (int i = 0; i < particles.Length; ++i) {
                    var particleRotation = UnityEngine.Quaternion.Euler(particles[i].rotation3D);
                    Assert.Less(UnityEngine.Vector3.Distance(expectedForward, particleRotation * UnityEngine.Vector3.forward), 0.001f);
                    if (UnityEngine.Mathf.Abs(UnityEngine.Mathf.DeltaAngle(particles[0].rotation3D.z, particles[i].rotation3D.z)) > 1f) differentRotations = true;
                }
                Assert.IsTrue(differentRotations, "A burst must retain per-particle randomness instead of one angle per owner.");
            } finally {
                views.Dispose(); world.Dispose(); UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void ParticlesBirthPreservesAgeForDelayedBurst() {
            var go = new UnityEngine.GameObject("Delayed particle source");
            var sourceSystem = go.AddComponent<UnityEngine.ParticleSystem>();
            sourceSystem.Stop(true, UnityEngine.ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = sourceSystem.main; main.playOnAwake = false; main.loop = false;
            main.startLifetime = 10f; main.startSpeed = 0f;
            var emission = sourceSystem.emission; emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new UnityEngine.ParticleSystem.Burst(0.1f, 1) });
            var shape = sourceSystem.shape; shape.enabled = false;
            var world = World.Create(); TestInitialize(in world);
            var properties = ViewsModuleProperties.Default; properties.interpolateState = false;
            var views = UnsafeViewsModule<EntityView>.Create(3u, ref world, new ParticlesProvider(), WorldProperties.Default.stateProperties.EntitiesCapacity, properties);
            try {
                var source = views.RegisterViewSource(go.AddComponent<DefaultView>(), checkPrefab: false);
                var ent = world.NewEnt(); ent.Set<TransformAspect>(); ent.InstantiateView(source);
                Batches.Apply(world); views.Update(0.02f).Complete();
                var system = (ParticlesProvider.ParticleSystemInfo)ProviderMap(views, "systemForPrefab")[source.prefabId];
                var child = system.particleSystem.subEmitters.GetSubEmitterSystem(0);
                for (int i = 0; i < 15; ++i) {
                    system.particleSystem.Simulate(0.02f, withChildren: true, restart: false, fixedTimeStep: false);
                    views.Update(0.02f).Complete();
                }
                Assert.AreEqual(1, child.particleCount, "The delayed burst must fire once while the proxy is retained.");
                var particles = new UnityEngine.ParticleSystem.Particle[1];
                Assert.AreEqual(1, system.particleSystem.GetParticles(particles));
                Assert.Greater(particles[0].startLifetime - particles[0].remainingLifetime, 0.25f);
            } finally {
                views.Dispose(); world.Dispose(); UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void ParticlesDisabledSubEmitterLinkDoesNotHideActiveChild() {
            var go = new UnityEngine.GameObject("Disabled sub emitter link");
            var parent = go.AddComponent<UnityEngine.ParticleSystem>();
            var childGo = new UnityEngine.GameObject("Active child"); childGo.transform.SetParent(go.transform, false);
            var child = childGo.AddComponent<UnityEngine.ParticleSystem>();
            parent.Stop(true, UnityEngine.ParticleSystemStopBehavior.StopEmittingAndClear);
            var sub = parent.subEmitters;
            sub.AddSubEmitter(child, UnityEngine.ParticleSystemSubEmitterType.Birth, UnityEngine.ParticleSystemSubEmitterProperties.InheritNothing);
            sub.enabled = false;
            var world = World.Create(); TestInitialize(in world);
            var views = UnsafeViewsModule<EntityView>.Create(3u, ref world, new ParticlesProvider(), WorldProperties.Default.stateProperties.EntitiesCapacity, ViewsModuleProperties.Default);
            try {
                var source = views.RegisterViewSource(go.AddComponent<DefaultView>(), checkPrefab: false);
                var system = (ParticlesProvider.ParticleSystemInfo)ProviderMap(views, "systemForPrefab")[source.prefabId];
                var childCanEmit = false;
                foreach (var candidate in system.particleSystem.GetComponentsInChildren<UnityEngine.ParticleSystem>()) {
                    var links = candidate.subEmitters;
                    if (links.enabled == false) continue;
                    for (int i = 0; i < links.subEmittersCount; ++i) {
                        if (links.GetSubEmitterSystem(i).name == "Active child") childCanEmit = true;
                    }
                }
                Assert.IsTrue(childCanEmit, "A disabled parent module must not suppress the active child's independent emission.");
            } finally {
                views.Dispose(); world.Dispose(); UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void ParticlesSwapBackPreservesSeedAndEmitsNewOwnerAtItsPose() {
            var go = new UnityEngine.GameObject("Particle swap back");
            var world = World.Create(); TestInitialize(in world);
            var properties = ViewsModuleProperties.Default; properties.interpolateState = false;
            var views = UnsafeViewsModule<EntityView>.Create(3u, ref world, new ParticlesProvider(), WorldProperties.Default.stateProperties.EntitiesCapacity, properties);
            try {
                var source = views.RegisterViewSource(go.AddComponent<DefaultView>(), checkPrefab: false);
                var first = world.NewEnt(); first.Set<TransformAspect>(); SetTestPosition(first, 10); first.InstantiateView(source);
                var second = world.NewEnt(); second.Set<TransformAspect>(); SetTestPosition(second, 20); second.InstantiateView(source);
                Batches.Apply(world); views.Update(0.016f).Complete();
                var system = (ParticlesProvider.ParticleSystemInfo)ProviderMap(views, "systemForPrefab")[source.prefabId];
                var particles = new UnityEngine.ParticleSystem.Particle[2];
                Assert.AreEqual(2, system.particleSystem.GetParticles(particles));
                var secondSeed = particles[0].position.x == 20f ? particles[0].randomSeed : particles[1].randomSeed;
                var capacity = system.particles.Capacity;
                first.DestroyView();
                var third = world.NewEnt(); third.Set<TransformAspect>(); SetTestPosition(third, 30); third.InstantiateView(source);
                Batches.Apply(world); views.Update(0.016f).Complete();
                Assert.AreEqual(2, system.particleSystem.GetParticles(particles));
                Assert.AreEqual(20f, particles[0].position.x);
                Assert.AreEqual(secondSeed, particles[0].randomSeed);
                Assert.AreEqual(30f, particles[1].position.x);
                Assert.AreNotEqual(secondSeed, particles[1].randomSeed);
                Assert.AreEqual(UnityEngine.Vector3.zero, particles[1].velocity);
                views.Update(0.016f).Complete();
                Assert.AreEqual(2, system.particleSystem.GetParticles(particles));
                Assert.AreEqual(capacity, system.particles.Capacity);
            } finally {
                views.Dispose(); world.Dispose(); UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [UnityEngine.TestTools.UnitySetUpAttribute]
        public System.Collections.IEnumerator SetUp() {
            AllTests.Start();
            yield return null;
        }

        [UnityEngine.TestTools.UnityTearDownAttribute]
        public System.Collections.IEnumerator TearDown() {
            AllTests.Dispose();
            yield return null;
        }

        [Test]
        public void PooledViewMovingBetweenRootsKeepsCountsBalanced() {
            var go = new UnityEngine.GameObject("Root accounting");
            var prefab = go.AddComponent<DefaultView>();
            var world = World.Create(); TestInitialize(in world);
            var views = UnsafeViewsModule<EntityView>.Create(1u, ref world, new EntityViewProvider(), WorldProperties.Default.stateProperties.EntitiesCapacity, ViewsModuleProperties.Default);
            try {
                var provider = views.provider.Value;
                provider.GetType().GetField("batchPerRoot", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(provider, 1);
                var source = views.RegisterViewSource(prefab, checkPrefab: false);
                var a = world.NewEnt(); a.Set<ME.BECS.Transforms.TransformAspect>(); a.InstantiateView(source);
                var b = world.NewEnt(); b.Set<ME.BECS.Transforms.TransformAspect>(); b.InstantiateView(source);
                Batches.Apply(world); views.Update(0.01f).Complete();
                var secondInstance = (EntityView)views.GetViewByEntity(b);
                Assert.AreEqual(1, secondInstance.rootInfo.index);
                a.DestroyView(); Batches.Apply(world); views.Update(0.01f).Complete();
                b.DestroyView(); Batches.Apply(world); views.Update(0.01f).Complete();
                b.InstantiateView(source); Batches.Apply(world); views.Update(0.01f).Complete();
                Assert.AreSame(secondInstance, views.GetViewByEntity(b));
                Assert.AreEqual(0, secondInstance.rootInfo.index);
                b.DestroyView(); Batches.Apply(world); views.Update(0.01f).Complete();
                var roots = (System.Collections.IEnumerable)provider.GetType().GetField("roots", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(provider);
                foreach (ViewRoot root in roots) Assert.AreEqual(0, root.Count);
            } finally {
                views.Dispose(); world.Dispose(); UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [UnityEngine.TestTools.UnityTest]
        public System.Collections.IEnumerator DisposedActiveProviderDoesNotRetainModules() => VerifyDisposedModuleCollection(pooled: false);

        [UnityEngine.TestTools.UnityTest]
        public System.Collections.IEnumerator DisposedPooledProviderDoesNotRetainModules() => VerifyDisposedModuleCollection(pooled: true);

        private static System.Collections.IEnumerator VerifyDisposedModuleCollection(bool pooled) {
            using var name = new TrackerNameScope(typeof(LifecycleProbe));
            LifecycleProbe.ResetCounts();
            var references = CreateAndDisposeTrackedViews(pooled);
            // Leave the destruction call stack and let the Editor release its per-frame references.
            yield return null;
            System.GC.Collect();
            System.GC.WaitForPendingFinalizers();
            System.GC.Collect();
            Assert.IsFalse(references[0].IsAlive, "Disposed provider retains the prefab module.");
            Assert.IsFalse(references[1].IsAlive, "Disposed provider retains the instance module.");
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static System.WeakReference[] CreateAndDisposeTrackedViews(bool pooled) {
            var go = new UnityEngine.GameObject("Handle lifetime");
            var prefab = CreateLifecyclePrefab(go);
            var world = World.Create(); TestInitialize(in world);
            var views = UnsafeViewsModule<EntityView>.Create(1u, ref world, new EntityViewProvider(), WorldProperties.Default.stateProperties.EntitiesCapacity, ViewsModuleProperties.Default);
            try {
                var source = views.RegisterViewSource(prefab, checkPrefab: false);
                var ent = world.NewEnt(); ent.Set<ME.BECS.Transforms.TransformAspect>(); ent.InstantiateView(source);
                Batches.Apply(world); views.Update(0.01f).Complete();
                var instance = (EntityView)views.GetViewByEntity(ent);
                var refs = new[] { new System.WeakReference(prefab.GetModule<LifecycleProbe>()), new System.WeakReference(instance.GetModule<LifecycleProbe>()) };
                if (pooled == true) { ent.DestroyView(); Batches.Apply(world); views.Update(0.01f).Complete(); }
                return refs;
            } finally {
                views.Dispose(); world.Dispose(); UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void GameObjectAssignmentChainKeepsInstancesAndDoesNotReenable(bool reverseIds) {
            using var name = new TrackerNameScope(typeof(LifecycleProbe));
            LifecycleProbe.ResetCounts();
            var go = new UnityEngine.GameObject("Assignment chain");
            var prefab = CreateLifecyclePrefab(go);
            var world = World.Create(); TestInitialize(in world);
            var properties = ViewsModuleProperties.Default; properties.spawnLimitPerFrame = 1;
            var views = UnsafeViewsModule<EntityView>.Create(1u, ref world, new EntityViewProvider(), WorldProperties.Default.stateProperties.EntitiesCapacity, properties);
            try {
                var low = world.NewEnt(); var high = world.NewEnt(); var b = world.NewEnt();
                var a = reverseIds == true ? low : high; var x = reverseIds == true ? high : low;
                var source = views.RegisterViewSource(prefab, checkPrefab: false);
                foreach (var ent in new[] { a, b, x }) ent.Set<ME.BECS.Transforms.TransformAspect>();
                a.InstantiateView(source); b.InstantiateView(source); Batches.Apply(world);
                views.Update(0.01f).Complete(); views.Update(0.01f).Complete();
                var aView = views.GetViewByEntity(a); var bView = views.GetViewByEntity(b);
                var aLocal = aView.GetViewData().localViewEnt; var bLocal = bView.GetViewData().localViewEnt;
                Assert.IsTrue(x.AssignView(a)); Assert.IsTrue(a.AssignView(b)); Batches.Apply(world);
                views.Update(0.01f).Complete();
                Assert.AreSame(aView, views.GetViewByEntity(x)); Assert.AreSame(bView, views.GetViewByEntity(a));
                Assert.AreEqual(aLocal, views.GetViewByEntity(x).GetViewData().localViewEnt);
                Assert.AreEqual(bLocal, views.GetViewByEntity(a).GetViewData().localViewEnt);
                Assert.AreEqual(2, LifecycleProbe.enabledCount); Assert.AreEqual(0, LifecycleProbe.disabledCount);
            } finally {
                views.Dispose(); world.Dispose(); UnityEngine.Object.DestroyImmediate(go);
            }
        }

        private sealed class TrackerNameScope : System.IDisposable {
            private readonly System.Type type;
            private readonly bool existed;
            private readonly string oldName;
            public TrackerNameScope(System.Type type) {
                this.type = type;
                this.existed = ViewsTracker.Tracker.names.TryGetValue(type, out this.oldName);
                ViewsTracker.Tracker.names[type] = type.Name;
            }
            public void Dispose() {
                if (this.existed == true) {
                    ViewsTracker.Tracker.names[this.type] = this.oldName;
                } else {
                    ViewsTracker.Tracker.names.Remove(this.type);
                }
            }
        }

        [System.Serializable]
        public class LifecycleProbe : IViewInitialize, IViewEnableFromPool, IViewDisableToPool, IViewDeInitialize, IViewUpdateParallel {
            public static int initialized, enabledCount, disabledCount, deinitialized, parallelCalls;
            private bool initializedState;
            private bool enabledState;
            public static void ResetCounts() { initialized = enabledCount = disabledCount = deinitialized = parallelCalls = 0; }
            public void OnInitialize() { Assert.IsFalse(this.initializedState); this.initializedState = true; ++initialized; }
            public void OnEnableFromPool(in ViewData data) {
                Assert.IsTrue(this.initializedState);
                Assert.IsFalse(this.enabledState, "Enable must be paired with Disable.");
                this.enabledState = true;
                ++enabledCount;
            }
            public void OnDisableToPool() {
                Assert.IsTrue(this.enabledState, "Disable must not run before Enable or run twice.");
                this.enabledState = false;
                ++disabledCount;
            }
            public void OnDeInitialize() {
                Assert.IsTrue(this.initializedState);
                Assert.IsFalse(this.enabledState);
                this.initializedState = false;
                ++deinitialized;
            }
            public void OnUpdateParallel(in ViewData data, float dt) { System.Threading.Interlocked.Increment(ref parallelCalls); }
        }

        private static DefaultView CreateLifecyclePrefab(UnityEngine.GameObject go) {
            var prefab = go.AddComponent<DefaultView>();
            prefab.modules.items = new[] { new ViewModules.Module() { enabled = true, module = new LifecycleProbe() } };
            prefab.OnValidate();
            return prefab;
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void ModuleLifecycleAndParallelUpdateSurvivePooling(int scenario) {
            using var name = new TrackerNameScope(typeof(LifecycleProbe));
            LifecycleProbe.ResetCounts();
            var go = new UnityEngine.GameObject("Lifecycle");
            var prefab = CreateLifecyclePrefab(go);
            var world = World.Create();
            TestInitialize(in world);
            var views = UnsafeViewsModule<EntityView>.Create(1u, ref world, new EntityViewProvider(), WorldProperties.Default.stateProperties.EntitiesCapacity, ViewsModuleProperties.Default);
            try {
                var source = views.RegisterViewSource(prefab, checkPrefab: false);
                var ent = world.NewEnt();
                ent.Set<ME.BECS.Transforms.TransformAspect>();
                ent.InstantiateView(source);
                Batches.Apply(world);
                views.Update(0.01f).Complete();
                Assert.AreEqual(1, LifecycleProbe.parallelCalls, "Parallel-only modules must run.");
                if (scenario != 2) {
                    ent.DestroyView();
                    Batches.Apply(world);
                    views.Update(0.01f).Complete();
                }
                if (scenario == 1) {
                    ent.InstantiateView(source);
                    Batches.Apply(world);
                    views.Update(0.01f).Complete();
                }
            } finally {
                views.Dispose();
                world.Dispose();
                UnityEngine.Object.DestroyImmediate(go);
            }
            Assert.AreEqual(1, LifecycleProbe.initialized);
            Assert.AreEqual(1, LifecycleProbe.deinitialized);
            Assert.AreEqual(scenario == 1 ? 2 : 1, LifecycleProbe.enabledCount);
            Assert.AreEqual(LifecycleProbe.enabledCount, LifecycleProbe.disabledCount);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void GenerationReplacementDoesNotDisableReplacementBeforeEnable(bool newCustomPool) {
            using var name = new TrackerNameScope(typeof(LifecycleProbe));
            LifecycleProbe.ResetCounts();
            var go = new UnityEngine.GameObject("Generation lifecycle");
            var prefab = CreateLifecyclePrefab(go);
            var world = World.Create();
            TestInitialize(in world);
            var views = UnsafeViewsModule<EntityView>.Create(1u, ref world, new EntityViewProvider(), WorldProperties.Default.stateProperties.EntitiesCapacity, ViewsModuleProperties.Default);
            try {
                var source = views.RegisterViewSource(prefab, checkPrefab: false);
                var first = world.NewEnt();
                first.Set<ME.BECS.Transforms.TransformAspect>();
                first.InstantiateView(source);
                Batches.Apply(world);
                views.Update(0.01f).Complete();
                first.Destroy();
                Batches.Apply(world);
                var replacement = world.NewEnt();
                Assert.AreEqual(first.id, replacement.id);
                Assert.AreNotEqual(first.gen, replacement.gen);
                replacement.Set<ME.BECS.Transforms.TransformAspect>();
                if (newCustomPool == true) replacement.Set(new ViewCustomIdComponent() { uniqueId = 99u });
                replacement.InstantiateView(source);
                Batches.Apply(world);
                views.Update(0.01f).Complete();
                Assert.AreEqual(2, LifecycleProbe.enabledCount);
                Assert.AreEqual(1, LifecycleProbe.disabledCount);
            } finally {
                views.Dispose();
                world.Dispose();
                UnityEngine.Object.DestroyImmediate(go);
            }
            Assert.AreEqual(newCustomPool == true ? 2 : 1, LifecycleProbe.initialized);
            Assert.AreEqual(LifecycleProbe.initialized, LifecycleProbe.deinitialized);
            Assert.AreEqual(2, LifecycleProbe.disabledCount);
        }

        [Test]
        public void SwitchingProviderWithEqualPrefabIdsRemovesPreviousInstance() {
            var go = UnityEngine.GameObject.CreatePrimitive(UnityEngine.PrimitiveType.Cube);
            var prefab = go.AddComponent<DefaultView>();
            var world = World.Create();
            TestInitialize(in world);
            var a = UnsafeViewsModule<EntityView>.Create(1u, ref world, new EntityViewProvider(), WorldProperties.Default.stateProperties.EntitiesCapacity, ViewsModuleProperties.Default);
            var b = UnsafeViewsModule<EntityView>.Create(3u, ref world, new ParticlesProvider(), WorldProperties.Default.stateProperties.EntitiesCapacity, ViewsModuleProperties.Default);
            try {
                var first = a.provider.Value.Register(a.data, prefab, prefabId: 700u, checkPrefab: false);
                var second = b.provider.Value.Register(b.data, prefab, prefabId: 700u, checkPrefab: false);
                var ent = world.NewEnt();
                ent.Set<ME.BECS.Transforms.TransformAspect>();
                ent.InstantiateView(first);
                Batches.Apply(world);
                a.Update(0.01f).Complete();
                var oldLocal = a.GetViewByEntity(ent).GetViewData().localViewEnt;
                ent.InstantiateView(second);
                Batches.Apply(world);
                b.Update(0.01f).Complete();
                a.Update(0.01f).Complete();
                Assert.AreEqual(0u, a.data.ptr->renderingOnSceneCount);
                Assert.AreEqual(1u, b.data.ptr->renderingOnSceneCount);
                Assert.IsFalse(oldLocal.IsAlive());
                Assert.IsFalse(ent.Has<EntityViewProviderTag>());
                AssertProviderOwner(b, 3u, ent, 0);
            } finally {
                a.Dispose(); b.Dispose(); world.Dispose();
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void ParticlesRegisterUsesEachExplicitPrefabId() {
            var goA = UnityEngine.GameObject.CreatePrimitive(UnityEngine.PrimitiveType.Cube);
            var goB = UnityEngine.GameObject.CreatePrimitive(UnityEngine.PrimitiveType.Sphere);
            var world = World.Create();
            TestInitialize(in world);
            var views = UnsafeViewsModule<EntityView>.Create(3u, ref world, new ParticlesProvider(), WorldProperties.Default.stateProperties.EntitiesCapacity, ViewsModuleProperties.Default);
            try {
                views.data.ptr->prefabId = 999u;
                var a = views.provider.Value.Register(views.data, goA.AddComponent<DefaultView>(), prefabId: 701u, checkPrefab: false);
                var b = views.provider.Value.Register(views.data, goB.AddComponent<DefaultView>(), prefabId: 702u, checkPrefab: false);
                Assert.IsTrue(ProviderMap(views, "systemForPrefab").Contains(701u));
                Assert.IsTrue(ProviderMap(views, "systemForPrefab").Contains(702u));
                var first = world.NewEnt(); first.Set<ME.BECS.Transforms.TransformAspect>(); first.InstantiateView(a);
                var second = world.NewEnt(); second.Set<ME.BECS.Transforms.TransformAspect>(); second.InstantiateView(b);
                Batches.Apply(world);
                views.Update(0.01f).Complete();
                Assert.AreEqual(2u, views.data.ptr->renderingOnSceneCount);
                Assert.AreEqual(701u, ProviderMap(views, "entityToPrefabId")[first]);
                Assert.AreEqual(702u, ProviderMap(views, "entityToPrefabId")[second]);
            } finally {
                views.Dispose(); world.Dispose();
                UnityEngine.Object.DestroyImmediate(goA); UnityEngine.Object.DestroyImmediate(goB);
            }
        }

        [Test]
        public void ChildWithoutUpdateReparentsWhenParentViewArrives() {
            var go = new UnityEngine.GameObject("Hierarchy");
            var prefab = go.AddComponent<DefaultView>();
            var world = World.Create(); TestInitialize(in world);
            var properties = ViewsModuleProperties.Default; properties.useUnityHierarchy = true; properties.interpolateState = false;
            var views = UnsafeViewsModule<EntityView>.Create(1u, ref world, new EntityViewProvider(), WorldProperties.Default.stateProperties.EntitiesCapacity, properties);
            try {
                var source = views.RegisterViewSource(prefab, checkPrefab: false);
                var parent = world.NewEnt(); parent.Set<ME.BECS.Transforms.TransformAspect>(); SetTestPosition(parent, 10);
                var child = world.NewEnt(); child.Set<ME.BECS.Transforms.TransformAspect>();
                child.SetParent(parent);
                #if FIXED_POINT
                child.Get<ME.BECS.Transforms.LocalMatrixComponent>().value = ME.BECS.FixedPoint.float4x4.Translate(new ME.BECS.FixedPoint.float3(2, 0, 0));
                #else
                child.Get<ME.BECS.Transforms.LocalMatrixComponent>().value = Unity.Mathematics.float4x4.Translate(new Unity.Mathematics.float3(2, 0, 0));
                #endif
                child.InstantiateView(source); Batches.Apply(world); views.Update(0.01f).Complete();
                var childView = (EntityView)views.GetViewByEntity(child);
                Assert.AreEqual(0u, views.data.ptr->renderingOnSceneUpdate.Count);
                parent.InstantiateView(source); Batches.Apply(world); views.Update(0.01f).Complete();
                Assert.AreSame(((EntityView)views.GetViewByEntity(parent)).transform, childView.transform.parent);
                Assert.AreEqual(12f, childView.transform.position.x, 0.001f);
            } finally {
                views.Dispose(); world.Dispose(); UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void DrawMeshKeepsMaterialSlotsAndAuthoredVisibility() {
            var go = UnityEngine.GameObject.CreatePrimitive(UnityEngine.PrimitiveType.Cube);
            var prefab = go.AddComponent<DefaultView>();
            var mesh = UnityEngine.Object.Instantiate(go.GetComponent<UnityEngine.MeshFilter>().sharedMesh);
            var triangles = mesh.GetTriangles(0); mesh.subMeshCount = 2; mesh.SetTriangles(triangles, 0); mesh.SetTriangles(triangles, 1);
            go.GetComponent<UnityEngine.MeshFilter>().sharedMesh = mesh;
            var material = go.GetComponent<UnityEngine.MeshRenderer>().sharedMaterial;
            var otherMaterial = new UnityEngine.Material(material);
            go.GetComponent<UnityEngine.MeshRenderer>().sharedMaterials = new[] { material, otherMaterial };
            var hidden = UnityEngine.GameObject.CreatePrimitive(UnityEngine.PrimitiveType.Cube); hidden.transform.SetParent(go.transform); hidden.SetActive(false);
            var disabled = UnityEngine.GameObject.CreatePrimitive(UnityEngine.PrimitiveType.Cube); disabled.transform.SetParent(go.transform); disabled.GetComponent<UnityEngine.MeshRenderer>().enabled = false;
            var world = World.Create(); TestInitialize(in world);
            var views = UnsafeViewsModule<EntityView>.Create(2u, ref world, new DrawMeshProvider(), WorldProperties.Default.stateProperties.EntitiesCapacity, ViewsModuleProperties.Default);
            try {
                var source = views.RegisterViewSource(prefab, checkPrefab: false);
                var ent = world.NewEnt(); ent.Set<ME.BECS.Transforms.TransformAspect>(); ent.InstantiateView(source);
                Batches.Apply(world); views.Update(0.01f).Complete();
                var map = ProviderMap(views, "objectsPerMeshAndMaterial");
                Assert.AreEqual(2, map.Count);
                var slots = new System.Collections.Generic.HashSet<int>();
                foreach (System.Collections.DictionaryEntry pair in map) {
                    var key = (DrawMeshProvider.Info)pair.Key; slots.Add(key.submeshIndex);
                    Assert.AreSame(key.submeshIndex == 0 ? material : otherMaterial, key.renderParams.material);
                    Assert.AreEqual(1, ((DrawMeshProvider.ObjectsPerInfo)pair.Value).entities.Length);
                }
                CollectionAssert.AreEquivalent(new[] { 0, 1 }, slots);
                ent.DestroyView(); Batches.Apply(world); views.Update(0.01f).Complete(); AssertProviderEmpty(views, 2u);
            } finally {
                views.Dispose(); world.Dispose(); UnityEngine.Object.DestroyImmediate(go);
                UnityEngine.Object.DestroyImmediate(mesh); UnityEngine.Object.DestroyImmediate(otherMaterial);
            }
        }

        public class SpawnPoseView : EntityView {
            public int enableCalls;
            public UnityEngine.Vector3 enablePosition;
            protected internal override void OnEnableFromPool(in ViewData viewData) {
                ++this.enableCalls;
                this.enablePosition = this.transform.position;
            }
        }

        [System.Serializable]
        public class SpawnPoseProbe : IViewEnableFromPool {
            public UnityEngine.Transform target;
            public int calls;
            public UnityEngine.Vector3 position;
            public void OnEnableFromPool(in ViewData viewData) {
                ++this.calls;
                this.position = this.target.position;
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void EnableFromPoolRunsAfterPoseWithoutApplyState(bool sameFrameReuse) {
            var go = new UnityEngine.GameObject("Spawn pose probe");
            var prefab = go.AddComponent<SpawnPoseView>();
            var hadType = ViewsTypeInfo.types.TryGetValue(typeof(SpawnPoseView), out var oldType);
            ViewsTypeInfo.types[typeof(SpawnPoseView)] = new ViewTypeInfo() { flags = TypeFlags.EnableFromPool };
            var hadName = ViewsTracker.Tracker.names.TryGetValue(typeof(SpawnPoseProbe), out var oldName);
            ViewsTracker.Tracker.names[typeof(SpawnPoseProbe)] = nameof(SpawnPoseProbe);
            prefab.modules.items = new[] {
                new ViewModules.Module() { enabled = true, module = new SpawnPoseProbe() { target = go.transform } },
                new ViewModules.Module() { enabled = false, module = new SpawnPoseProbe() { target = go.transform } },
            };
            prefab.OnValidate();
            var world = World.Create();
            TestInitialize(in world);
            var views = UnsafeViewsModule<EntityView>.Create(ViewsModule.GAMEOBJECT_PROVIDER_ID, ref world, new EntityViewProvider(), WorldProperties.Default.stateProperties.EntitiesCapacity, ViewsModuleProperties.Default);
            try {
                var viewId = views.RegisterViewSource(prefab, checkPrefab: false, sceneSource: false);
                var first = world.NewEnt();
                first.Set<ME.BECS.Transforms.TransformAspect>();
                SetTestPosition(first, 10);
                first.InstantiateView(viewId);
                Batches.Apply(world);
                views.Update(0.01f).Complete();
                var instance = (SpawnPoseView)views.GetViewByEntity(in first);
                var probe = instance.GetModule<SpawnPoseProbe>();
                Assert.AreEqual(1, instance.enableCalls);
                Assert.AreEqual(10f, instance.enablePosition.x);
                Assert.AreEqual(1, probe.calls);
                Assert.AreEqual(10f, probe.position.x);
                Assert.AreEqual(0, views.data.ptr->renderingOnSceneApplyState.Count);
                Assert.AreEqual(0, ((SpawnPoseProbe)instance.modules.items[1].module).calls);
                first.DestroyView();
                Batches.Apply(world);
                if (sameFrameReuse == false) views.Update(0.01f).Complete();
                var second = world.NewEnt();
                second.Set<ME.BECS.Transforms.TransformAspect>();
                SetTestPosition(second, 100);
                second.InstantiateView(viewId);
                Batches.Apply(world);
                views.Update(0.01f).Complete();
                Assert.AreSame(instance, views.GetViewByEntity(in second));
                Assert.AreEqual(2, instance.enableCalls);
                Assert.AreEqual(100f, instance.enablePosition.x);
                Assert.AreEqual(2, probe.calls);
                Assert.AreEqual(100f, probe.position.x);
                views.Update(0.01f).Complete();
                Assert.AreEqual(2, probe.calls);
            } finally {
                views.Dispose();
                world.Dispose();
                UnityEngine.Object.DestroyImmediate(go);
                if (hadType == true) {
                    ViewsTypeInfo.types[typeof(SpawnPoseView)] = oldType;
                } else {
                    ViewsTypeInfo.types.Remove(typeof(SpawnPoseView));
                }
                if (hadName == true) {
                    ViewsTracker.Tracker.names[typeof(SpawnPoseProbe)] = oldName;
                } else {
                    ViewsTracker.Tracker.names.Remove(typeof(SpawnPoseProbe));
                }
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ResetTrailsPreservesSettingsAndDoesNotStartUnselectedChildren(bool emitting) {
            var go = new UnityEngine.GameObject("Trail reset");
            try {
                var ps = go.AddComponent<UnityEngine.ParticleSystem>();
                var child = new UnityEngine.GameObject("Unselected child");
                child.transform.SetParent(go.transform);
                var childPs = child.AddComponent<UnityEngine.ParticleSystem>();
                childPs.Stop(withChildren: false, stopBehavior: UnityEngine.ParticleSystemStopBehavior.StopEmittingAndClear);
                var trailObject = new UnityEngine.GameObject("Trail");
                trailObject.transform.SetParent(go.transform);
                var tr = trailObject.AddComponent<UnityEngine.TrailRenderer>();
                tr.time = 2f;
                tr.emitting = emitting;
                tr.AddPosition(UnityEngine.Vector3.zero);
                tr.AddPosition(UnityEngine.Vector3.right);
                ps.Emit(10);
                var module = new ResetTrailsModule() { particleSystems = new[] { ps }, trailRenderers = new[] { tr } };
                module.OnInitialize();
                module.Reset();
                Assert.IsFalse(tr.emitting);
                go.transform.position = UnityEngine.Vector3.right * 100f;
                module.OnEnableFromPool(default);
                Assert.AreEqual(0, tr.positionCount);
                Assert.AreEqual(2f, tr.time);
                Assert.AreEqual(emitting, tr.emitting);
                Assert.AreEqual(0, ps.particleCount);
                Assert.IsTrue(ps.isPlaying);
                Assert.IsTrue(childPs.isStopped);
                ps.Emit(1);
                module.ApplyState(default);
                Assert.AreEqual(1, ps.particleCount, "Regular ApplyState must not reset the effect again.");
            } finally {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void CreateEntityView() {

            {
                var go = new UnityEngine.GameObject("Test");
                var comp = go.AddComponent<DefaultView>();
                var dt = 0.01f;
                
                var world = World.Create();
                TestInitialize(in world);
                ME.BECS.Views.ViewsTypeInfo.RegisterType<ME.BECS.Views.DefaultView>(new ME.BECS.Views.ViewTypeInfo() {
                    flags = (ME.BECS.Views.TypeFlags)0,
                });
                var views = ME.BECS.Views.UnsafeViewsModule<EntityView>.Create(ViewsModule.GAMEOBJECT_PROVIDER_ID, ref world, new ME.BECS.Views.EntityViewProvider(), WorldProperties.Default.stateProperties.EntitiesCapacity, ME.BECS.Views.ViewsModuleProperties.Default);
                var viewId = views.RegisterViewSource(comp, checkPrefab: false, sceneSource: false);
                Ent firstEnt;
                {
                    var ent = world.NewEnt();
                    ent.Set<ME.BECS.Transforms.TransformAspect>();
                    ME.BECS.Views.UnsafeViewsModule.InstantiateView(in ent, viewId);
                    Batches.Apply(world);
                    firstEnt = ent;
                }
                {
                    views.Update(dt).Complete();
                    Assert.AreEqual(1, views.data.ptr->renderingOnScene.Count);
                    Assert.AreEqual(1, views.data.ptr->renderingOnSceneEntToRenderIndex.Count);
                    Assert.AreEqual(1, views.data.ptr->renderingOnSceneRenderIndexToEnt.Count);
                    views.Update(dt).Complete();
                    Assert.AreEqual(1, views.data.ptr->renderingOnScene.Count);
                    {
                        var ent = world.NewEnt();
                        ent.Set<ME.BECS.Transforms.TransformAspect>();
                        ME.BECS.Views.UnsafeViewsModule.InstantiateView(in ent, viewId);
                        Batches.Apply(world);
                    }
                    views.Update(dt).Complete();
                    Assert.AreEqual(2, views.data.ptr->renderingOnSceneEntToRenderIndex.Count);
                    Assert.AreEqual(2, views.data.ptr->renderingOnSceneRenderIndexToEnt.Count);
                    Assert.AreEqual(0, views.data.ptr->renderingOnSceneEntToRenderIndex[views.data.ptr->viewsWorld.state.ptr->allocator, 0]);
                    Assert.AreEqual(0, views.data.ptr->renderingOnSceneRenderIndexToEnt[views.data.ptr->viewsWorld.state.ptr->allocator, 0]);
                    Assert.AreEqual(1, views.data.ptr->renderingOnSceneEntToRenderIndex[views.data.ptr->viewsWorld.state.ptr->allocator, 1]);
                    Assert.AreEqual(1, views.data.ptr->renderingOnSceneRenderIndexToEnt[views.data.ptr->viewsWorld.state.ptr->allocator, 1]);
                    Assert.AreEqual(2, views.data.ptr->renderingOnScene.Count);
                    {
                        ME.BECS.Views.UnsafeViewsModule.DestroyView(firstEnt);
                        Batches.Apply(world);
                    }
                    views.Update(dt).Complete();
                    //Assert.IsFalse(firstEnt.Has<ViewComponent>());
                    Assert.IsFalse(firstEnt.Has<IsViewRequested>());
                    //Assert.IsFalse(firstEnt.Has<EntityViewProviderTag>());
                    Assert.AreEqual(1, views.data.ptr->renderingOnSceneEntToRenderIndex.Count);
                    Assert.AreEqual(1, views.data.ptr->renderingOnSceneRenderIndexToEnt.Count);
                    Assert.AreEqual(1, views.data.ptr->renderingOnScene.Count);
                    Assert.AreEqual(0, views.data.ptr->renderingOnSceneEntToRenderIndex[views.data.ptr->viewsWorld.state.ptr->allocator, 1]);
                    Assert.AreEqual(1, views.data.ptr->renderingOnSceneRenderIndexToEnt[views.data.ptr->viewsWorld.state.ptr->allocator, 0]);
                }
                views.Dispose();
                world.Dispose();
                UnityEngine.GameObject.DestroyImmediate(go);
            }

        }

        [TestCase(false)]
        [TestCase(true)]
        public void AssignView(bool requestNewSourceView) {

            {
                var go = new UnityEngine.GameObject("Test");
                var comp = go.AddComponent<DefaultView>();
                var dt = 0.01f;
                
                var world = World.Create();
                TestInitialize(in world);
                ME.BECS.Views.ViewsTypeInfo.RegisterType<ME.BECS.Views.DefaultView>(new ME.BECS.Views.ViewTypeInfo() {
                    flags = (ME.BECS.Views.TypeFlags)0,
                });
                var views = ME.BECS.Views.UnsafeViewsModule<EntityView>.Create(ViewsModule.GAMEOBJECT_PROVIDER_ID, ref world, new ME.BECS.Views.EntityViewProvider(), WorldProperties.Default.stateProperties.EntitiesCapacity, ME.BECS.Views.ViewsModuleProperties.Default);
                var viewId = views.RegisterViewSource(comp, checkPrefab: false, sceneSource: false);
                Ent firstEnt;
                {
                    var ent = world.NewEnt();
                    ent.Set<ME.BECS.Transforms.TransformAspect>();
                    ME.BECS.Views.UnsafeViewsModule.InstantiateView(in ent, viewId);
                    Batches.Apply(world);
                    firstEnt = ent;
                }
                {
                    views.Update(dt).Complete();
                    {
                        Assert.IsTrue(firstEnt.Has<ViewComponent>());
                        Assert.IsTrue(firstEnt.Has<IsViewRequested>());
                        Assert.IsTrue(firstEnt.Has<EntityViewProviderTag>());
                        Assert.IsTrue(views.data.ptr->renderingOnSceneEntToRenderIndex.ContainsKey(views.data.ptr->viewsWorld.state.ptr->allocator, firstEnt.id));
                        var idx = views.data.ptr->renderingOnSceneEntToRenderIndex[views.data.ptr->viewsWorld.state.ptr->allocator, firstEnt.id];
                        var instanceInfo = views.data.ptr->renderingOnScene[views.data.ptr->viewsWorld.state, idx];
                        var instance = (EntityView)System.Runtime.InteropServices.GCHandle.FromIntPtr(instanceInfo.obj).Target;
                        Assert.IsTrue(instance.viewData.logicEnt == firstEnt);
                    }

                    // Exercise all update lists without introducing a new generated view type.
                    var originalView = views.GetViewByEntity(in firstEnt);
                    var originalIndex = views.data.ptr->renderingOnSceneEntToRenderIndex[views.data.ptr->viewsWorld.state.ptr->allocator, firstEnt.id];
                    var prefabInfo = views.data.ptr->renderingOnScene[views.data.ptr->viewsWorld.state, originalIndex].prefabInfo;
                    prefabInfo.ptr->typeInfo.flags |= TypeFlags.ApplyState | TypeFlags.ApplyStateParallel | TypeFlags.Update | TypeFlags.UpdateParallel;
                    ViewsTracker.Tracker.names.TryAdd(typeof(DefaultView), nameof(DefaultView));
                    ref var allocator = ref views.data.ptr->viewsWorld.state.ptr->allocator;
                    views.data.ptr->renderingOnSceneApplyState.Add(ref allocator, firstEnt.id);
                    views.data.ptr->renderingOnSceneUpdate.Add(ref allocator, firstEnt.id);
                    views.data.ptr->renderingOnSceneApplyStateParallel.Add(ref allocator, firstEnt.id);
                    views.data.ptr->renderingOnSceneUpdateParallel.Add(ref allocator, firstEnt.id);
                    views.data.ptr->applyStateCounter.ptr->count = 1;
                    views.data.ptr->updateCounter.ptr->count = 1;
                    views.data.ptr->applyStateParallelCounter.ptr->count = 1;
                    views.data.ptr->updateParallelCounter.ptr->count = 1;

                    var newEnt = world.NewEnt();
                    newEnt.Set<ME.BECS.Transforms.TransformAspect>();
                    Assert.IsTrue(ME.BECS.Views.UnsafeViewsModule.AssignView(in newEnt, in firstEnt));
                    Assert.IsTrue(newEnt.Has<AssignViewComponent>());
                    Assert.IsFalse(newEnt.Read<AssignViewComponent>().isUsed);
                    if (requestNewSourceView == true) {
                        Assert.IsTrue(ME.BECS.Views.UnsafeViewsModule.InstantiateView(in firstEnt, viewId));
                    }
                    Batches.Apply(world);
                    views.Update(dt).Complete();
                    {
                        Assert.AreEqual(requestNewSourceView, firstEnt.Has<ViewComponent>());
                        Assert.AreEqual(requestNewSourceView, firstEnt.Has<IsViewRequested>());
                        Assert.AreEqual(requestNewSourceView, firstEnt.Has<EntityViewProviderTag>());
                        Assert.AreSame(originalView, views.GetViewByEntity(in newEnt));
                        if (requestNewSourceView == true) {
                            Assert.AreNotSame(originalView, views.GetViewByEntity(in firstEnt));
                            Assert.IsNotNull(views.GetViewByEntity(in firstEnt));
                        } else {
                            Assert.IsNull(views.GetViewByEntity(in firstEnt));
                            Assert.IsFalse(views.data.ptr->renderingOnSceneEntToRenderIndex.ContainsKey(in allocator, firstEnt.id));
                        }
                        Assert.IsTrue(views.data.ptr->renderingOnSceneApplyState.sparseSet.Has(in allocator, newEnt.id, out _));
                        Assert.IsTrue(views.data.ptr->renderingOnSceneUpdate.sparseSet.Has(in allocator, newEnt.id, out _));
                        Assert.AreEqual(requestNewSourceView, views.data.ptr->renderingOnSceneApplyState.sparseSet.Has(in allocator, firstEnt.id, out _));
                        Assert.AreEqual(requestNewSourceView, views.data.ptr->renderingOnSceneUpdate.sparseSet.Has(in allocator, firstEnt.id, out _));
                        Assert.IsTrue(views.data.ptr->renderingOnSceneApplyStateParallel.sparseSet.Has(in allocator, newEnt.id, out _));
                        Assert.IsTrue(views.data.ptr->renderingOnSceneUpdateParallel.sparseSet.Has(in allocator, newEnt.id, out _));
                        Assert.AreEqual(requestNewSourceView, views.data.ptr->renderingOnSceneApplyStateParallel.sparseSet.Has(in allocator, firstEnt.id, out _));
                        Assert.AreEqual(requestNewSourceView, views.data.ptr->renderingOnSceneUpdateParallel.sparseSet.Has(in allocator, firstEnt.id, out _));
                        Assert.IsTrue(newEnt.Has<ViewComponent>());
                        Assert.IsTrue(newEnt.Has<IsViewRequested>());
                        Assert.IsTrue(newEnt.Has<EntityViewProviderTag>());
                        Assert.IsTrue(newEnt.Read<AssignViewComponent>().isUsed);
                        Assert.IsTrue(views.data.ptr->renderingOnSceneEntToRenderIndex.ContainsKey(views.data.ptr->viewsWorld.state.ptr->allocator, newEnt.id));
                        var idx = views.data.ptr->renderingOnSceneEntToRenderIndex[views.data.ptr->viewsWorld.state.ptr->allocator, newEnt.id];
                        var instanceInfo = views.data.ptr->renderingOnScene[views.data.ptr->viewsWorld.state, idx];
                        var instance = (EntityView)System.Runtime.InteropServices.GCHandle.FromIntPtr(instanceInfo.obj).Target;
                        Assert.IsTrue(instance.viewData.logicEnt == newEnt);
                    }
                    ME.BECS.Views.UnsafeViewsModule.DestroyView(in newEnt);
                    Batches.Apply(world);
                    views.Update(dt).Complete();
                    Assert.IsNull(views.GetViewByEntity(in newEnt));
                    Assert.IsFalse(views.data.ptr->renderingOnSceneApplyState.sparseSet.Has(in allocator, newEnt.id, out _));
                    Assert.IsFalse(views.data.ptr->renderingOnSceneUpdate.sparseSet.Has(in allocator, newEnt.id, out _));
                    Assert.AreEqual(requestNewSourceView == true ? 1u : 0u, views.data.ptr->renderingOnSceneApplyState.Count);
                    Assert.AreEqual(requestNewSourceView == true ? 1u : 0u, views.data.ptr->renderingOnSceneUpdate.Count);
                    Assert.IsFalse(views.data.ptr->renderingOnSceneApplyStateParallel.sparseSet.Has(in allocator, newEnt.id, out _));
                    Assert.IsFalse(views.data.ptr->renderingOnSceneUpdateParallel.sparseSet.Has(in allocator, newEnt.id, out _));
                    Assert.AreEqual(requestNewSourceView == true ? 1u : 0u, views.data.ptr->renderingOnSceneApplyStateParallel.Count);
                    Assert.AreEqual(requestNewSourceView == true ? 1u : 0u, views.data.ptr->renderingOnSceneUpdateParallel.Count);
                    views.Update(dt).Complete();
                }
                views.Dispose();
                world.Dispose();
                UnityEngine.GameObject.DestroyImmediate(go);
            }

        }

        [Test]
        public void RegisterProviderPreservesHigherIds() {
            ME.BECS.Views.UnsafeViewsModule.RegisterProviderType<ParticlesProviderTag>(ViewsModule.PARTICLES_PROVIDER_ID);
            ME.BECS.Views.UnsafeViewsModule.RegisterProviderType<DrawMeshProviderTag>(ViewsModule.DRAW_MESH_PROVIDER_ID);
            ME.BECS.Views.UnsafeViewsModule.RegisterProviderType<EntityViewProviderTag>(ViewsModule.GAMEOBJECT_PROVIDER_ID);
            using var world = World.Create();
            var drawMeshEnt = world.NewEnt();
            var particlesEnt = world.NewEnt();
            Assert.IsTrue(drawMeshEnt.InstantiateView(new ViewSource() { providerId = ViewsModule.DRAW_MESH_PROVIDER_ID, prefabId = 1u }));
            Assert.IsTrue(particlesEnt.InstantiateView(new ViewSource() { providerId = ViewsModule.PARTICLES_PROVIDER_ID, prefabId = 1u }));
            Assert.IsTrue(drawMeshEnt.Has<DrawMeshProviderTag>());
            Assert.IsTrue(particlesEnt.Has<ParticlesProviderTag>());
        }

        [TestCase(false)]
        [TestCase(true)]
        public void AssignBeforeFirstSpawnResolvesOnce(bool requestNewSourceView) {
            var go = new UnityEngine.GameObject("Pending view");
            var prefab = go.AddComponent<DefaultView>();
            var world = World.Create();
            TestInitialize(in world);
            var views = UnsafeViewsModule<EntityView>.Create(ViewsModule.GAMEOBJECT_PROVIDER_ID, ref world, new EntityViewProvider(), WorldProperties.Default.stateProperties.EntitiesCapacity, ViewsModuleProperties.Default);
            try {
                var source = world.NewEnt();
                var destination = world.NewEnt();
                source.Set<ME.BECS.Transforms.TransformAspect>();
                destination.Set<ME.BECS.Transforms.TransformAspect>();
                var view = views.RegisterViewSource(prefab, checkPrefab: false);
                source.InstantiateView(view);
                Assert.IsTrue(destination.AssignView(source));
                if (requestNewSourceView == true) source.InstantiateView(view);
                Batches.Apply(world);
                views.Update(0.01f).Complete();
                var instance = views.GetViewByEntity(destination);
                Assert.IsNotNull(instance);
                Assert.IsTrue(destination.Read<AssignViewComponent>().isUsed);
                for (int frame = 0; frame < 3; ++frame) {
                    views.Update(0.01f).Complete();
                    Assert.AreSame(instance, views.GetViewByEntity(destination));
                    Assert.AreEqual(requestNewSourceView == true ? 2u : 1u, views.data.ptr->renderingOnSceneCount);
                    Assert.AreEqual(views.data.ptr->renderingOnSceneCount, views.data.ptr->renderingOnSceneEntToRenderIndex.Count);
                    if (requestNewSourceView == true) Assert.AreNotSame(instance, views.GetViewByEntity(source));
                }
            } finally {
                views.Dispose();
                world.Dispose();
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [TestCase(2u, 0)]
        [TestCase(2u, 1)]
        [TestCase(2u, 2)]
        [TestCase(3u, 0)]
        [TestCase(3u, 1)]
        [TestCase(3u, 2)]
        public void ProviderTransfersOwnership(uint providerId, int scenario) {
            var go = UnityEngine.GameObject.CreatePrimitive(UnityEngine.PrimitiveType.Cube);
            var prefab = go.AddComponent<DefaultView>();
            var world = World.Create();
            TestInitialize(in world);
            var properties = ViewsModuleProperties.Default;
            properties.interpolateState = false;
            IViewProvider<EntityView> provider = providerId == ViewsModule.DRAW_MESH_PROVIDER_ID ? new DrawMeshProvider() : new ParticlesProvider();
            var views = UnsafeViewsModule<EntityView>.Create(providerId, ref world, provider, WorldProperties.Default.stateProperties.EntitiesCapacity, properties);
            try {
                var source = world.NewEnt();
                var destination = world.NewEnt();
                source.Set<ME.BECS.Transforms.TransformAspect>();
                destination.Set<ME.BECS.Transforms.TransformAspect>();
                var view = views.RegisterViewSource(prefab, checkPrefab: false);
                source.InstantiateView(view);
                Batches.Apply(world);
                views.Update(0.01f).Complete();
                Assert.IsTrue(destination.AssignView(source));
                if (scenario == 1) {
                    source.Destroy();
                } else {
                    source.InstantiateView(view);
                }
                if (scenario == 2) destination.DestroyView();
                Batches.Apply(world);
                views.Update(0.01f).Complete();
                Assert.AreEqual(scenario == 0 ? 2u : 1u, views.data.ptr->renderingOnSceneCount);
                if (scenario != 2) {
                    SetTestPosition(destination, 13);
                    views.Update(0.01f).Complete();
                    AssertProviderOwner(views, providerId, destination, 13);
                    destination.DestroyView();
                }
                if (scenario != 1) source.DestroyView();
                Batches.Apply(world);
                views.Update(0.01f).Complete();
                Assert.AreEqual(0u, views.data.ptr->renderingOnSceneCount);
                AssertProviderEmpty(views, providerId);
            } finally {
                views.Dispose();
                world.Dispose();
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [TestCase(2u, false, false)]
        [TestCase(2u, true, false)]
        [TestCase(2u, false, true)]
        [TestCase(2u, true, true)]
        [TestCase(3u, false, false)]
        [TestCase(3u, true, false)]
        [TestCase(3u, false, true)]
        [TestCase(3u, true, true)]
        public void ProviderTransfersBatch(uint providerId, bool reverseIds, bool removeFirstDestination) {
            var go = UnityEngine.GameObject.CreatePrimitive(UnityEngine.PrimitiveType.Cube);
            var prefab = go.AddComponent<DefaultView>();
            var world = World.Create();
            TestInitialize(in world);
            var properties = ViewsModuleProperties.Default;
            properties.interpolateState = false;
            IViewProvider<EntityView> provider = providerId == 2u ? new DrawMeshProvider() : new ParticlesProvider();
            var views = UnsafeViewsModule<EntityView>.Create(providerId, ref world, provider, WorldProperties.Default.stateProperties.EntitiesCapacity, properties);
            try {
                var x = world.NewEnt();
                var first = world.NewEnt();
                var second = world.NewEnt();
                var a = reverseIds == true ? first : second;
                var b = reverseIds == true ? second : first;
                x.Set<ME.BECS.Transforms.TransformAspect>();
                a.Set<ME.BECS.Transforms.TransformAspect>();
                b.Set<ME.BECS.Transforms.TransformAspect>();
                var view = views.RegisterViewSource(prefab, checkPrefab: false);
                a.InstantiateView(view);
                b.InstantiateView(view);
                Batches.Apply(world);
                views.Update(0.01f).Complete();
                Assert.IsTrue(x.AssignView(a));
                Assert.IsTrue(a.AssignView(b));
                Batches.Apply(world);
                SetTestPosition(x, 11);
                SetTestPosition(a, 22);
                if (removeFirstDestination == true) x.DestroyView();
                Batches.Apply(world);
                views.Update(0.01f).Complete();
                AssertProviderOwner(views, providerId, a, 22);
                if (removeFirstDestination == false) AssertProviderOwner(views, providerId, x, 11);
                Assert.AreEqual(removeFirstDestination == true ? 1u : 2u, views.data.ptr->renderingOnSceneCount);
                a.DestroyView();
                if (removeFirstDestination == false) x.DestroyView();
                Batches.Apply(world);
                views.Update(0.01f).Complete();
                AssertProviderEmpty(views, providerId);
            } finally {
                views.Dispose();
                world.Dispose();
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        public class DualPhaseTestView : EntityView {
            public int mainCalls;
            public int parallelCalls;
            protected internal override void ApplyState(in ViewData viewData) { ++this.mainCalls; }
            protected internal override void ApplyStateParallel(in ViewData viewData) { ++this.parallelCalls; }
        }

        [Test]
        public void BothApplyStatePhasesObserveChangesAndTransfer() {
            var go = new UnityEngine.GameObject("Dual phase view");
            var prefab = go.AddComponent<DualPhaseTestView>();
            var trackerInfo = new ViewsTracker.ViewInfo();
            trackerInfo.tracker.Resize(1u);
            trackerInfo.tracker.Get(0u) = StaticTypes<TestComponent>.trackerIndex;
            var type = typeof(DualPhaseTestView);
            var hadType = ViewsTypeInfo.types.TryGetValue(type, out var oldTypeInfo);
            var hadName = ViewsTracker.Tracker.names.TryGetValue(type, out var oldName);
            ViewsTypeInfo.types[type] = new ViewTypeInfo() {
                flags = TypeFlags.ApplyState | TypeFlags.ApplyStateParallel,
                tracker = trackerInfo,
            };
            ViewsTracker.Tracker.names[type] = nameof(DualPhaseTestView);
            var world = World.Create();
            TestInitialize(in world);
            var views = UnsafeViewsModule<EntityView>.Create(ViewsModule.GAMEOBJECT_PROVIDER_ID, ref world, new EntityViewProvider(), WorldProperties.Default.stateProperties.EntitiesCapacity, ViewsModuleProperties.Default);
            try {
                var a = world.NewEnt();
                var b = world.NewEnt();
                a.Set<ME.BECS.Transforms.TransformAspect>();
                b.Set<ME.BECS.Transforms.TransformAspect>();
                a.Set(new TestComponent() { data = 1 });
                var view = views.RegisterViewSource(prefab, checkPrefab: false);
                a.InstantiateView(view);
                Batches.Apply(world);
                views.Update(0.01f).Complete();
                var instance = (DualPhaseTestView)views.GetViewByEntity(a);
                Assert.AreEqual(1, instance.mainCalls);
                Assert.AreEqual(1, instance.parallelCalls);
                a.Get<TestComponent>().data = 2;
                views.Update(0.01f).Complete();
                Assert.AreEqual(2, instance.mainCalls);
                Assert.AreEqual(2, instance.parallelCalls);
                b.Set(new TestComponent() { data = 3 });
                b.Get<TestComponent>().data = 4;
                Assert.AreEqual(a.GetVersion(StaticTypes<TestComponent>.trackerIndex), b.GetVersion(StaticTypes<TestComponent>.trackerIndex));
                Assert.IsTrue(b.AssignView(a));
                Batches.Apply(world);
                views.Update(0.01f).Complete();
                Assert.AreSame(instance, views.GetViewByEntity(b));
                Assert.AreEqual(3, instance.mainCalls);
                Assert.AreEqual(3, instance.parallelCalls);
                views.Update(0.01f).Complete();
                Assert.AreEqual(3, instance.mainCalls);
                Assert.AreEqual(3, instance.parallelCalls);
                b.DestroyView();
                Batches.Apply(world);
                views.Update(0.01f).Complete();
                a.InstantiateView(view);
                Batches.Apply(world);
                views.Update(0.01f).Complete();
                Assert.AreSame(instance, views.GetViewByEntity(a));
                Assert.AreEqual(4, instance.mainCalls);
                Assert.AreEqual(4, instance.parallelCalls);
            } finally {
                views.Dispose();
                world.Dispose();
                UnityEngine.Object.DestroyImmediate(go);
                if (hadType == true) {
                    ViewsTypeInfo.types[type] = oldTypeInfo;
                } else {
                    ViewsTypeInfo.types.Remove(type);
                }
                if (hadName == true) {
                    ViewsTracker.Tracker.names[type] = oldName;
                } else {
                    ViewsTracker.Tracker.names.Remove(type);
                }
                trackerInfo.tracker.Dispose();
            }
        }

        private static void SetTestPosition(in Ent ent, int x) {
            #if FIXED_POINT
            ent.Get<ME.BECS.Transforms.WorldMatrixComponent>().value = ME.BECS.FixedPoint.float4x4.Translate(new ME.BECS.FixedPoint.float3(x, 0, 0));
            #else
            ent.Get<ME.BECS.Transforms.WorldMatrixComponent>().value = Unity.Mathematics.float4x4.Translate(new Unity.Mathematics.float3(x, 0, 0));
            #endif
        }

        private static System.Collections.IDictionary ProviderMap(UnsafeViewsModule<EntityView> views, string name) {
            var provider = views.provider.Value;
            return (System.Collections.IDictionary)provider.GetType().GetField(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(provider);
        }

        private static void AssertProviderOwner(UnsafeViewsModule<EntityView> views, uint providerId, in Ent owner, int x) {
            if (providerId == ViewsModule.PARTICLES_PROVIDER_ID) {
                var prefabId = (uint)ProviderMap(views, "entityToPrefabId")[owner];
                var index = (int)ProviderMap(views, "entityToInstanceIndex")[owner];
                var objects = (ParticlesProvider.ObjectsPerPrefab)ProviderMap(views, "objectsPerPrefab")[prefabId];
                Assert.AreEqual(owner, objects.entities[index]);
                Assert.AreEqual((float)x, (float)objects.instances[index].position.x);
            } else {
                var found = false;
                foreach (DrawMeshProvider.ObjectsPerInfo objects in ProviderMap(views, "objectsPerMeshAndMaterial").Values) {
                    for (int i = 0; i < objects.entities.Length; ++i) {
                        if (objects.entities[i] != owner) continue;
                        found = true;
                        Assert.AreEqual((float)x, objects.matrices[i].m03);
                    }
                }
                Assert.IsTrue(found);
            }
        }

        private static void AssertProviderEmpty(UnsafeViewsModule<EntityView> views, uint providerId) {
            if (providerId == ViewsModule.PARTICLES_PROVIDER_ID) {
                Assert.AreEqual(0, ProviderMap(views, "entityToPrefabId").Count);
                Assert.AreEqual(0, ProviderMap(views, "entityToInstanceIndex").Count);
                foreach (ParticlesProvider.ObjectsPerPrefab objects in ProviderMap(views, "objectsPerPrefab").Values) Assert.AreEqual(0, objects.entities.Length);
            } else {
                foreach (DrawMeshProvider.ObjectsPerInfo objects in ProviderMap(views, "objectsPerMeshAndMaterial").Values) Assert.AreEqual(0, objects.entities.Length);
            }
        }

        [Test]
        public void AssignInvalidatesModuleTrackerWithEqualVersions() {
            var go = new UnityEngine.GameObject("Tracked view");
            var prefab = go.AddComponent<DefaultView>();
            prefab.modules.items = new[] { new ViewModules.Module() { enabled = true, module = new TestViewModule() } };
            prefab.OnValidate();
            var world = World.Create();
            TestInitialize(in world);
            var views = UnsafeViewsModule<EntityView>.Create(ViewsModule.GAMEOBJECT_PROVIDER_ID, ref world, new EntityViewProvider(), WorldProperties.Default.stateProperties.EntitiesCapacity, ViewsModuleProperties.Default);
            // The editor publication may omit unused test modules. Install an explicit
            // tracker so equal versions cannot make this regression pass vacuously.
            var trackerInfo = new ViewsTracker.ViewInfo();
            trackerInfo.tracker.Resize(1u);
            trackerInfo.tracker.Get(0u) = StaticTypes<TestComponent>.trackerIndex;
            var moduleType = typeof(TestViewModule);
            var hadIndex = ViewsTracker.typeToIndex.TryGetValue(moduleType, out var oldIndex);
            var hadName = ViewsTracker.Tracker.names.TryGetValue(moduleType, out var oldName);
            var oldTrackerInfos = ViewsTracker.info;
            var trackerIndex = (uint)ViewsTracker.info.Length;
            System.Array.Resize(ref ViewsTracker.info, (int)trackerIndex + 1);
            ViewsTracker.info[trackerIndex] = trackerInfo;
            ViewsTracker.typeToIndex[moduleType] = trackerIndex;
            ViewsTracker.Tracker.names[moduleType] = nameof(TestViewModule);
            try {
                Assert.AreEqual(1u, ViewsTracker.GetTracker(new TestViewModule()).tracker.Length);
                var source = world.NewEnt();
                var destination = world.NewEnt();
                source.Set<ME.BECS.Transforms.TransformAspect>();
                destination.Set<ME.BECS.Transforms.TransformAspect>();
                source.Set(new TestComponent() { data = 10 });
                destination.Set(new TestComponent() { data = 20 });
                Assert.AreEqual(source.GetVersion(StaticTypes<TestComponent>.trackerIndex), destination.GetVersion(StaticTypes<TestComponent>.trackerIndex));
                var view = views.RegisterViewSource(prefab, checkPrefab: false);
                source.InstantiateView(view);
                Batches.Apply(world);
                views.Update(0.01f).Complete();
                var instance = (EntityView)views.GetViewByEntity(source);
                var module = (TestViewModule)instance.modules.items[0].module;
                Assert.AreEqual(10, module.lastValue);
                var calls = module.calls;
                Assert.IsTrue(destination.AssignView(source));
                Batches.Apply(world);
                views.Update(0.01f).Complete();
                Assert.AreSame(instance, views.GetViewByEntity(destination));
                Assert.AreEqual(calls + 1, module.calls);
                Assert.AreEqual(20, module.lastValue);
            } finally {
                views.Dispose();
                world.Dispose();
                UnityEngine.Object.DestroyImmediate(go);
                ViewsTracker.info = oldTrackerInfos;
                if (hadIndex == true) {
                    ViewsTracker.typeToIndex[moduleType] = oldIndex;
                } else {
                    ViewsTracker.typeToIndex.Remove(moduleType);
                }
                if (hadName == true) {
                    ViewsTracker.Tracker.names[moduleType] = oldName;
                } else {
                    ViewsTracker.Tracker.names.Remove(moduleType);
                }
                trackerInfo.tracker.Dispose();
            }
        }

        public static void TestInitialize(in World world) {
            ref var tr = ref world.InitializeAspect<ME.BECS.Transforms.TransformAspect>();
            tr.localPositionData = new AspectDataPtr<ME.BECS.Transforms.LocalPositionComponent>(in world);
            tr.localRotationData = new AspectDataPtr<ME.BECS.Transforms.LocalRotationComponent>(in world);
            tr.localScaleData = new AspectDataPtr<ME.BECS.Transforms.LocalScaleComponent>(in world);
            tr.parentData = new AspectDataPtr<ME.BECS.Transforms.ParentComponent>(in world);
            tr.childrenData = new AspectDataPtr<ME.BECS.Transforms.ChildrenComponent>(in world);
            tr.worldMatrixData = new AspectDataPtr<ME.BECS.Transforms.WorldMatrixComponent>(in world);
        }

        [System.Serializable]
        public class TestViewModule : IViewModule, IViewApplyState {

            public int calls;
            public int lastValue;

            public void ApplyState(in ViewData viewData) {
                EntRO ent = viewData;
                var test = ent.Read<TestComponent>();
                ++this.calls;
                this.lastValue = test.data;
            }

        }

        [Test]
        public void EntityGroupVersionUp() {

            {
                using var world = World.Create();
                var ent = Ent.New(world);
                Assert.AreEqual(1, ent.Version);
                ent.Set(new TestComponent());
                Assert.AreEqual(2, ent.Version);
                Assert.AreEqual(1, ent.GetVersion(StaticTypes<TestComponent>.trackerIndex));
                ent.Set(new Test2Component());
                Assert.AreEqual(3, ent.Version);
                Assert.AreEqual(1, ent.GetVersion(StaticTypes<TestComponent>.trackerIndex));
                ++ent.Get<TestComponent>().data;
                Assert.AreEqual(4, ent.Version);
                Assert.AreEqual(2, ent.GetVersion(StaticTypes<TestComponent>.trackerIndex));
            }

        }

    }

}
