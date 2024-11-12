namespace ME.BECS {

    using HIC = UnityEngine.HideInCallstackAttribute;
    using str = Unity.Collections.FixedString512Bytes;

    public struct UnityLogger : ILogger {

        [UnityEngine.RuntimeInitializeOnLoadMethodAttribute]
        public static void Initialize() {
            Logger.SetLogger(new UnityLogger());
        }
        
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