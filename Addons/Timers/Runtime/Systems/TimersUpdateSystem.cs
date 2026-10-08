#if FIXED_POINT
using tfloat = sfloat;
using ME.BECS.FixedPoint;
#else
using tfloat = System.Single;
using Unity.Mathematics;
#endif

namespace ME.BECS.Timers {

    using BURST = Unity.Burst.BurstCompileAttribute;
    using ME.BECS.Jobs;

    /// <summary>
    /// Coordinates timers update during the ECS system lifecycle.
    /// </summary>
    [BURST]
    [SystemGenericParallelMode]
    public partial struct TimersUpdateSystem<T> : IUpdate where T : unmanaged, ITimer {

        /// <summary>
        /// Executes timers update system work through the job scheduler.
        /// </summary>
        [BURST]
        public partial struct Job : IJobForComponents<T> {
            /// <summary>
            /// Time step supplied to this update.
            /// </summary>
            [InjectDeltaTime]
            public tfloat dt;
            /// <summary>
            /// Processes the job inputs for <c>TimersUpdateSystem</c>.
            /// </summary>
            public void Execute(in JobInfo jobInfo, in Ent ent, ref T component) {
                component.timer -= this.dt;
                if (component.timer <= 0f) {
                    component.timer = 0f;
                }
            }
        }

        /// <summary>
        /// Updates timers update system using the current inputs and execution context.
        /// </summary>
        public void OnUpdate(ref SystemContext context) {

            context.Query().AsParallel().Schedule<Job, T>().AddDependency(ref context);

        }

    }

    /// <summary>
    /// Coordinates timers ms update during the ECS system lifecycle.
    /// </summary>
    [BURST]
    [SystemGenericParallelMode]
    public partial struct TimersMsUpdateSystem<T> : IUpdate where T : unmanaged, ITimerMs {

        /// <summary>
        /// Executes timers ms update system work through the job scheduler.
        /// </summary>
        [BURST]
        public partial struct Job : IJobForComponents<T> {
            /// <summary>
            /// Time step supplied to this update.
            /// </summary>
            [InjectDeltaTime]
            public uint dt;
            /// <summary>
            /// Processes the job inputs for <c>TimersMsUpdateSystem</c>.
            /// </summary>
            public void Execute(in JobInfo jobInfo, in Ent ent, ref T component) {
                if (component.timer >= this.dt) {
                    component.timer -= this.dt;
                } else {
                    component.timer = 0u;
                }
            }
        }

        /// <summary>
        /// Updates timers ms update system using the current inputs and execution context.
        /// </summary>
        public void OnUpdate(ref SystemContext context) {

            context.Query().AsParallel().Schedule<Job, T>().AddDependency(ref context);

        }

    }

    /// <summary>
    /// Coordinates timers auto destroy update during the ECS system lifecycle.
    /// </summary>
    [BURST]
    [SystemGenericParallelMode]
    public partial struct TimersAutoDestroyUpdateSystem<T> : IUpdate where T : unmanaged, ITimerAutoDestroy {

        /// <summary>
        /// Executes timers auto destroy update system work through the job scheduler.
        /// </summary>
        [BURST]
        public partial struct Job : IJobForComponents<T> {
            /// <summary>
            /// Time step supplied to this update.
            /// </summary>
            [InjectDeltaTime]
            public tfloat dt;
            /// <summary>
            /// Processes the job inputs for <c>TimersAutoDestroyUpdateSystem</c>.
            /// </summary>
            public void Execute(in JobInfo jobInfo, in Ent ent, ref T component) {
                component.timer -= this.dt;
                if (component.timer <= 0f) {
                    ent.Remove<T>();
                }
            }
        }

        /// <summary>
        /// Updates timers auto destroy update system using the current inputs and execution context.
        /// </summary>
        public void OnUpdate(ref SystemContext context) {

            context.Query().AsParallel().Schedule<Job, T>().AddDependency(ref context);

        }

    }

    /// <summary>
    /// Coordinates timers ms auto destroy update during the ECS system lifecycle.
    /// </summary>
    [BURST]
    [SystemGenericParallelMode]
    public partial struct TimersMsAutoDestroyUpdateSystem<T> : IUpdate where T : unmanaged, ITimerMsAutoDestroy {

        /// <summary>
        /// Executes timers ms auto destroy update system work through the job scheduler.
        /// </summary>
        [BURST]
        public partial struct Job : IJobForComponents<T> {
            /// <summary>
            /// Time step supplied to this update.
            /// </summary>
            [InjectDeltaTime]
            public uint dt;
            /// <summary>
            /// Processes the job inputs for <c>TimersMsAutoDestroyUpdateSystem</c>.
            /// </summary>
            public void Execute(in JobInfo jobInfo, in Ent ent, ref T component) {
                if (component.timer >= this.dt) {
                    component.timer -= this.dt;
                } else {
                    ent.Remove<T>();
                }
            }
        }

        /// <summary>
        /// Updates timers ms auto destroy update system using the current inputs and execution context.
        /// </summary>
        public void OnUpdate(ref SystemContext context) {

            context.Query().AsParallel().Schedule<Job, T>().AddDependency(ref context);

        }

    }

}