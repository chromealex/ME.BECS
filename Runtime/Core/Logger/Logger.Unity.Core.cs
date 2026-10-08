namespace ME.BECS {

    using HIC = UnityEngine.HideInCallstackAttribute;
    using CND = System.Diagnostics.ConditionalAttribute;
    using str = Unity.Collections.FixedString512Bytes;

    /// <summary>
    /// Routes BECS diagnostics to the configured logging implementation.
    /// </summary>
    public static unsafe partial class Logger {

        /// <summary>
        /// Provides core operations within <c>Logger</c>.
        /// </summary>
        public class Core : BaseLogger<UnityLogger> {

            /// <summary>
            /// Writes an informational diagnostic through the configured logger.
            /// </summary>
            [HIC][CND("LOGS_CORE_INFO")]
            new public static void Log(str text, bool showCallstack = false) {
                BaseLogger<UnityLogger>.Log(text, showCallstack);
            }

            /// <summary>
            /// Writes a warning through the configured logger.
            /// </summary>
            [HIC][CND("LOGS_CORE_WARNING")]
            new public static void Warning(str text, bool showCallstack = false) {
                BaseLogger<UnityLogger>.Warning(text, showCallstack);
            }

            /// <summary>
            /// Writes an error through the configured logger.
            /// </summary>
            [HIC][CND("LOGS_CORE_ERROR")]
            new public static void Error(str text, bool showCallstack = false) {
                BaseLogger<UnityLogger>.Error(text, showCallstack);
            }

            /// <summary>
            /// Reports an exception through the configured logger.
            /// </summary>
            [HIC]
            new public static void Exception(System.Exception ex, bool showCallstack = true) {
                BaseLogger<UnityLogger>.Exception(ex, showCallstack);
            }

        }
        
    }

}