namespace ME.BECS {
    
    public static class CustomModules {

        private static event InitializeResetPass resetPass; 
        private static event InitializeFirstPass firstPass; 
        private static event InitializeSecondPass secondPass; 

        public delegate void InitializeResetPass();
        public delegate void InitializeFirstPass();
        public delegate void InitializeSecondPass();
        
        static CustomModules() {

            resetPass = null;
            firstPass = null;
            secondPass = null;

        }

        public static void RegisterResetPass(InitializeResetPass initializeResetPassCallback) {

            resetPass += initializeResetPassCallback;

        }

        public static void RegisterFirstPass(InitializeFirstPass initializeFirstPassCallback) {

            firstPass += initializeFirstPassCallback;

        }

        public static void RegisterSecondPass(InitializeSecondPass initializeSecondPassCallback) {

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