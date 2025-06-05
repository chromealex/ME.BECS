#if FIXED_POINT
using tfloat = sfloat;
using ME.BECS.FixedPoint;
#else
using tfloat = System.Single;
using Unity.Mathematics;
#endif

namespace ME.BECS.Timers {

    public interface ITimerMs : IComponent {
        uint timer { get; set; }
    }
    
    public interface ITimer : IComponent {
        tfloat timer { get; set; }
    }

    /// <summary>
    /// Component will be destroyed automatically when timer reaches 0
    /// </summary>
    public interface ITimerMsAutoDestroy : IComponent {
        uint timer { get; set; }
    }
    
    /// <summary>
    /// Component will be destroyed automatically when timer reaches 0
    /// </summary>
    public interface ITimerAutoDestroy : IComponent {
        tfloat timer { get; set; }
    }

}