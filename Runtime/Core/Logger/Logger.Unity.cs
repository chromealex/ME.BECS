namespace ME.BECS {

    using HIC = UnityEngine.HideInCallstackAttribute;
    using str = Unity.Collections.FixedString512Bytes;

    /// <summary>
    /// Routes BECS diagnostic messages to the Unity console.
    /// </summary>
    public struct UnityLogger : ILogger {

        /// <summary>
        /// Initializes unity logger state from the supplied context.
        /// </summary>
        [UnityEngine.RuntimeInitializeOnLoadMethodAttribute]
        public static void Initialize() {
            Logger.SetLogger(new UnityLogger());
        }
        
        /// <summary>
        /// Writes an informational diagnostic through the configured logger.
        /// </summary>
        [HIC]
        public void Log(str text, bool showCallstack = false) {
            UnityEngine.StackTraceLogType type = UnityEngine.StackTraceLogType.None;
            if (showCallstack == false) {
                type = UnityEngine.Application.GetStackTraceLogType(UnityEngine.LogType.Log);
                UnityEngine.Application.SetStackTraceLogType(UnityEngine.LogType.Log, UnityEngine.StackTraceLogType.None);
            }
            UnityEngine.Debug.Log(text);
            if (showCallstack == false) {
                UnityEngine.Application.SetStackTraceLogType(UnityEngine.LogType.Log, type);
            }
        }

        /// <summary>
        /// Writes a warning through the configured logger.
        /// </summary>
        [HIC]
        public void Warning(str text, bool showCallstack = false) {
            UnityEngine.StackTraceLogType type = UnityEngine.StackTraceLogType.None;
            if (showCallstack == false) {
                type = UnityEngine.Application.GetStackTraceLogType(UnityEngine.LogType.Warning);
                UnityEngine.Application.SetStackTraceLogType(UnityEngine.LogType.Warning, UnityEngine.StackTraceLogType.None);
            }
            UnityEngine.Debug.LogWarning(text);
            if (showCallstack == false) {
                UnityEngine.Application.SetStackTraceLogType(UnityEngine.LogType.Warning, type);
            }
        }

        /// <summary>
        /// Writes an error through the configured logger.
        /// </summary>
        [HIC]
        public void Error(str text, bool showCallstack = true) {
            UnityEngine.StackTraceLogType type = UnityEngine.StackTraceLogType.None;
            if (showCallstack == false) {
                type = UnityEngine.Application.GetStackTraceLogType(UnityEngine.LogType.Error);
                UnityEngine.Application.SetStackTraceLogType(UnityEngine.LogType.Error, UnityEngine.StackTraceLogType.None);
            }
            UnityEngine.Debug.LogError(text);
            if (showCallstack == false) {
                UnityEngine.Application.SetStackTraceLogType(UnityEngine.LogType.Error, type);
            }
        }

        /// <summary>
        /// Reports an exception through the configured logger.
        /// </summary>
        [HIC]
        public void Exception(System.Exception ex, bool showCallstack = true) {
            UnityEngine.StackTraceLogType type = UnityEngine.StackTraceLogType.None;
            if (showCallstack == false) {
                type = UnityEngine.Application.GetStackTraceLogType(UnityEngine.LogType.Exception);
                UnityEngine.Application.SetStackTraceLogType(UnityEngine.LogType.Exception, UnityEngine.StackTraceLogType.None);
            }
            UnityEngine.Debug.LogException(ex);
            if (showCallstack == false) {
                UnityEngine.Application.SetStackTraceLogType(UnityEngine.LogType.Exception, type);
            }
        }

    }
    
}