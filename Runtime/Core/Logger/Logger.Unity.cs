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
            var type = UnityEngine.Application.GetStackTraceLogType(UnityEngine.LogType.Log);
            UnityEngine.Application.SetStackTraceLogType(UnityEngine.LogType.Log, showCallstack == true ? UnityEngine.StackTraceLogType.Full : UnityEngine.StackTraceLogType.None);
            UnityEngine.Debug.Log(text);
            UnityEngine.Application.SetStackTraceLogType(UnityEngine.LogType.Log, type);
        }

        [HIC]
        public void Warning(str text, bool showCallstack = false) {
            var type = UnityEngine.Application.GetStackTraceLogType(UnityEngine.LogType.Warning);
            UnityEngine.Application.SetStackTraceLogType(UnityEngine.LogType.Warning, showCallstack == true ? UnityEngine.StackTraceLogType.Full : UnityEngine.StackTraceLogType.None);
            UnityEngine.Debug.LogWarning(text);
            UnityEngine.Application.SetStackTraceLogType(UnityEngine.LogType.Warning, type);
        }

        [HIC]
        public void Error(str text, bool showCallstack = true) {
            var type = UnityEngine.Application.GetStackTraceLogType(UnityEngine.LogType.Error);
            UnityEngine.Application.SetStackTraceLogType(UnityEngine.LogType.Error, showCallstack == true ? UnityEngine.StackTraceLogType.Full : UnityEngine.StackTraceLogType.None);
            UnityEngine.Debug.LogError(text);
            UnityEngine.Application.SetStackTraceLogType(UnityEngine.LogType.Error, type);
        }

        [HIC]
        public void Exception(System.Exception ex, bool showCallstack = true) {
            var type = UnityEngine.Application.GetStackTraceLogType(UnityEngine.LogType.Exception);
            UnityEngine.Application.SetStackTraceLogType(UnityEngine.LogType.Exception, showCallstack == true ? UnityEngine.StackTraceLogType.Full : UnityEngine.StackTraceLogType.None);
            UnityEngine.Debug.LogException(ex);
            UnityEngine.Application.SetStackTraceLogType(UnityEngine.LogType.Exception, type);
        }

    }
    
}