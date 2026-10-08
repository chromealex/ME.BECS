#if FIXED_POINT
using tfloat = sfloat;
using ME.BECS.FixedPoint;
#else
using tfloat = System.Single;
using Unity.Mathematics;
#endif

namespace ME.BECS.Timers {

    /// <summary>
    /// Defines default timer component data used by entity processing.
    /// </summary>
    public struct DefaultTimerComponent : ITimer {
        
        /// <summary>
        /// Timer in the time units used by the containing API.
        /// </summary>
        public tfloat timer { get; set; }

    }
    
    /// <summary>
    /// Defines default timer ms component data used by entity processing.
    /// </summary>
    public struct DefaultTimerMsComponent : ITimerMs {
        
        /// <summary>
        /// Timer in the time units used by the containing API.
        /// </summary>
        public uint timer { get; set; }

    }
    
    /// <summary>
    /// Defines default timer auto destroy component data used by entity processing.
    /// </summary>
    public struct DefaultTimerAutoDestroyComponent : ITimerAutoDestroy {
        
        /// <summary>
        /// Timer in the time units used by the containing API.
        /// </summary>
        public tfloat timer { get; set; }

    }
    
    /// <summary>
    /// Defines default timer ms auto destroy component data used by entity processing.
    /// </summary>
    public struct DefaultTimerMsAutoDestroyComponent : ITimerMsAutoDestroy {
        
        /// <summary>
        /// Timer in the time units used by the containing API.
        /// </summary>
        public uint timer { get; set; }

    }

}