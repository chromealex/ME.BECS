namespace ME.BECS {

    using HIC = UnityEngine.HideInCallstackAttribute;
    using CND = System.Diagnostics.ConditionalAttribute;
    using str = Unity.Collections.FixedString512Bytes;

    public static unsafe partial class Logger {

        public class Features : BaseLogger<UnityLogger> {

            [HIC][CND("LOGS_FEATURES_INFO")]
            new public static void Log(str text, bool showCallstack = false) {
                BaseLogger<UnityLogger>.Log(text, showCallstack);
            }

            [HIC][CND("LOGS_FEATURES_WARNING")]
            new public static void Warning(str text, bool showCallstack = false) {
                BaseLogger<UnityLogger>.Warning(text, showCallstack);
            }

            [HIC][CND("LOGS_FEATURES_ERROR")]
            new public static void Error(str text, bool showCallstack = false) {
                BaseLogger<UnityLogger>.Error(text, showCallstack);
            }

            [HIC]
            new public static void Exception(System.Exception ex, bool showCallstack = false) {
                BaseLogger<UnityLogger>.Exception(ex, showCallstack);
            }

        }
        
    }

}