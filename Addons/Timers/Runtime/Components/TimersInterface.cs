#if FIXED_POINT
using tfloat = sfloat;
using ME.BECS.FixedPoint;
#else
using tfloat = System.Single;
using Unity.Mathematics;
#endif

namespace ME.BECS.Timers {

    /// <summary>
    /// Stores per-entity state for i timer ms.
    /// </summary>
    public interface ITimerMs : IComponent {
        /// <summary>
        /// Timer in the time units used by the containing API.
        /// </summary>
        uint timer { get; set; }
    }
    
    /// <summary>
    /// Stores per-entity state for i timer.
    /// </summary>
    public interface ITimer : IComponent {
        /// <summary>
        /// Timer in the time units used by the containing API.
        /// </summary>
        tfloat timer { get; set; }
    }

    /// <summary>
    /// Component will be destroyed automatically when timer reaches 0
    /// </summary>
    public interface ITimerMsAutoDestroy : IComponent {
        /// <summary>
        /// Timer in the time units used by the containing API.
        /// </summary>
        uint timer { get; set; }
    }
    
    /// <summary>
    /// Component will be destroyed automatically when timer reaches 0
    /// </summary>
    public interface ITimerAutoDestroy : IComponent {
        /// <summary>
        /// Timer in the time units used by the containing API.
        /// </summary>
        tfloat timer { get; set; }
    }

}