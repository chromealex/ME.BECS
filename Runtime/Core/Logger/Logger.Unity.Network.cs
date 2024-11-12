namespace ME.BECS {

    using HIC = UnityEngine.HideInCallstackAttribute;
    using CND = System.Diagnostics.ConditionalAttribute;
    using str = Unity.Collections.FixedString512Bytes;

    public static unsafe partial class Logger {

        public class Network : BaseLogger<UnityLogger> {

            [HIC][CND("LOGS_NETWORK_INFO_FULL")]
            public static void LogInfo(str text, bool showCallstack = false) {
                BaseLogger<UnityLogger>.Log(text, showCallstack);
            }

            [HIC][CND("LOGS_NETWORK_INFO")]
            new public static void Log(str text, bool showCallstack = false) {
                BaseLogger<UnityLogger>.Log(text, showCallstack);
            }

            [HIC][CND("LOGS_NETWORK_WARNING")]
            new public static void Warning(str text, bool showCallstack = false) {
                BaseLogger<UnityLogger>.Warning(text, showCallstack);
            }

            [HIC][CND("LOGS_NETWORK_ERROR")]
            new public static void Error(str text, bool showCallstack = true) {
                BaseLogger<UnityLogger>.Error(text, showCallstack);
            }

            [HIC]
            new public static void Exception(System.Exception ex, bool showCallstack = true) {
                BaseLogger<UnityLogger>.Exception(ex, showCallstack);
            }

        }
        
    }

}