namespace ME.BECS {

    using HIC = UnityEngine.HideInCallstackAttribute;
    using str = Unity.Collections.FixedString512Bytes;
    using static Cuts;

    public interface ILogger {

        void Log(str text, bool showCallstack = false);
        void Warning(str text, bool showCallstack = false);
        void Error(str text, bool showCallstack = true);
        void Exception(System.Exception ex, bool showCallstack = true);

    }

    public struct DummyLogger : ILogger {
        
        public void Log(str text, bool showCallstack = false) {
        }

        public void Warning(str text, bool showCallstack = false) {
        }

        public void Error(str text, bool showCallstack = true) {
        }

        public void Exception(System.Exception ex, bool showCallstack = true) {
        }

    }

    public abstract unsafe class BaseLogger<T> where T : unmanaged, ILogger {

        private static safe_ptr<T> logger => (safe_ptr<T>)Logger.logger;

        [HIC]
        public static void Log(str text, bool showCallstack = false) {
            logger.ptr->Log(text, showCallstack);
        }

        [HIC]
        public static void Warning(str text, bool showCallstack = false) {
            logger.ptr->Warning(text, showCallstack);
        }

        [HIC]
        public static void Error(str text, bool showCallstack = true) {
            logger.ptr->Error(text, showCallstack);
        }

        [HIC]
        public static void Exception(System.Exception ex, bool showCallstack = true) {
            logger.ptr->Exception(ex, showCallstack);
        }

    }

    public static unsafe partial class Logger {

        private static Unity.Collections.Allocator ALLOCATOR => Constants.ALLOCATOR_DOMAIN_REAL;

        internal static safe_ptr logger = (safe_ptr)_makeDefault(new DummyLogger(), ALLOCATOR);
        
        public static void SetLogger<T>(T logger) where T : unmanaged, ILogger {
            Logger.logger = _make(TSize<T>.size, TAlign<T>.alignInt, ALLOCATOR);
            _memcpy((safe_ptr)(&logger), Logger.logger, TSize<T>.size);
        }

    }

}