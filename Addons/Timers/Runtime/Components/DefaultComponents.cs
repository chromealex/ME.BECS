#if FIXED_POINT
using tfloat = sfloat;
using ME.BECS.FixedPoint;
#else
using tfloat = System.Single;
using Unity.Mathematics;
#endif

namespace ME.BECS.Timers {

    public struct DefaultTimerComponent : ITimer {
        
        public tfloat timer { get; set; }

    }
    
    public struct DefaultTimerMsComponent : ITimerMs {
        
        public uint timer { get; set; }

    }
    
    public struct DefaultTimerAutoDestroyComponent : ITimerAutoDestroy {
        
        public tfloat timer { get; set; }

    }
    
    public struct DefaultTimerMsAutoDestroyComponent : ITimerMsAutoDestroy {
        
        public uint timer { get; set; }

    }

}