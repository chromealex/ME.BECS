namespace ME.BECS {

    using HIC = UnityEngine.HideInCallstackAttribute;
    using str = Unity.Collections.FixedString512Bytes;
    using static Cuts;

    /// <summary>
    /// Defines the operations required by logger.
    /// </summary>
    public interface ILogger {

        /// <summary>
        /// Writes an informational diagnostic through the configured logger.
        /// </summary>
        void Log(str text, bool showCallstack = false);
        /// <summary>
        /// Writes a warning through the configured logger.
        /// </summary>
        void Warning(str text, bool showCallstack = false);
        /// <summary>
        /// Writes an error through the configured logger.
        /// </summary>
        void Error(str text, bool showCallstack = true);
        /// <summary>
        /// Reports an exception through the configured logger.
        /// </summary>
        void Exception(System.Exception ex, bool showCallstack = true);

    }

    /// <summary>
    /// Implements logging without emitting diagnostic messages.
    /// </summary>
    public struct DummyLogger : ILogger {
        
        /// <summary>
        /// Provides the <c>Log</c> callback; this implementation performs no work.
        /// </summary>
        public void Log(str text, bool showCallstack = false) {
        }

        /// <summary>
        /// Provides the <c>Warning</c> callback; this implementation performs no work.
        /// </summary>
        public void Warning(str text, bool showCallstack = false) {
        }

        /// <summary>
        /// Provides the <c>Error</c> callback; this implementation performs no work.
        /// </summary>
        public void Error(str text, bool showCallstack = true) {
        }

        /// <summary>
        /// Provides the <c>Exception</c> callback; this implementation performs no work.
        /// </summary>
        public void Exception(System.Exception ex, bool showCallstack = true) {
        }

    }

    /// <summary>
    /// Defines the logging operations used by BECS diagnostics.
    /// </summary>
    public abstract unsafe class BaseLogger<T> where T : unmanaged, ILogger {

        private static safe_ptr<T> logger => (safe_ptr<T>)Logger.logger;

        /// <summary>
        /// Writes an informational diagnostic through the configured logger.
        /// </summary>
        [HIC]
        public static void Log(str text, bool showCallstack = false) {
            logger.ptr->Log(text, showCallstack);
        }

        /// <summary>
        /// Writes a warning through the configured logger.
        /// </summary>
        [HIC]
        public static void Warning(str text, bool showCallstack = false) {
            logger.ptr->Warning(text, showCallstack);
        }

        /// <summary>
        /// Writes an error through the configured logger.
        /// </summary>
        [HIC]
        public static void Error(str text, bool showCallstack = true) {
            logger.ptr->Error(text, showCallstack);
        }

        /// <summary>
        /// Reports an exception through the configured logger.
        /// </summary>
        [HIC]
        public static void Exception(System.Exception ex, bool showCallstack = true) {
            logger.ptr->Exception(ex, showCallstack);
        }

    }

    /// <summary>
    /// Routes BECS diagnostics to the configured logging implementation.
    /// </summary>
    public static unsafe partial class Logger {

        private static Unity.Collections.Allocator ALLOCATOR => Constants.ALLOCATOR_DOMAIN_REAL;

        internal static safe_ptr logger = (safe_ptr)_makeDefault(new DummyLogger(), ALLOCATOR);
        
        /// <summary>
        /// Sets logger.
        /// </summary>
        public static void SetLogger<T>(T logger) where T : unmanaged, ILogger {
            Logger.logger = _make(TSize<T>.size, TAlign<T>.alignInt, ALLOCATOR);
            _memcpy((safe_ptr)(&logger), Logger.logger, TSize<T>.size);
        }

    }

}