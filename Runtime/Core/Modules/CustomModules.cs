namespace ME.BECS {
    
    /// <summary>
    /// Defines custom modules state and operations.
    /// </summary>
    public static class CustomModules {

        private static event InitializeResetPass resetPass; 
        private static event InitializeFirstPass firstPass; 
        private static event InitializeSecondPass secondPass; 

        /// <summary>
        /// Defines the callback signature for initialize reset pass.
        /// </summary>
        public delegate void InitializeResetPass();
        /// <summary>
        /// Defines the callback signature for initialize first pass.
        /// </summary>
        public delegate void InitializeFirstPass();
        /// <summary>
        /// Defines the callback signature for initialize second pass.
        /// </summary>
        public delegate void InitializeSecondPass();
        
        static CustomModules() {

            resetPass = null;
            firstPass = null;
            secondPass = null;

        }

        /// <summary>
        /// Registers reset pass.
        /// </summary>
        public static void RegisterResetPass(InitializeResetPass initializeResetPassCallback) {

            resetPass -= initializeResetPassCallback;
            resetPass += initializeResetPassCallback;

        }

        /// <summary>
        /// Registers first pass.
        /// </summary>
        public static void RegisterFirstPass(InitializeFirstPass initializeFirstPassCallback) {

            firstPass -= initializeFirstPassCallback;
            firstPass += initializeFirstPassCallback;

        }

        /// <summary>
        /// Registers second pass.
        /// </summary>
        public static void RegisterSecondPass(InitializeSecondPass initializeSecondPassCallback) {

            secondPass -= initializeSecondPassCallback;
            secondPass += initializeSecondPassCallback;

        }

        internal static void InvokeResetPass() {
         
            resetPass?.Invoke();
            
        }

        internal static void InvokeFirstPass() {
         
            firstPass?.Invoke();
            
        }

        internal static void InvokeSecondPass() {
            
            secondPass?.Invoke();
            
        }
        
    }
    
}