namespace ME.BECS.Jobs {

    using System.Collections.Generic;
    using System.Threading;
    using static Cuts;

    public class DispatcherMono : UnityEngine.MonoBehaviour {

        public void Update() {
            MainThreadDispatcher.ExecuteTasks();
        }

    }

    public unsafe delegate void MainThreadCallback(void* ptr);

    public sealed unsafe class MainThreadDispatcher {

        public static MainThreadDispatcher instance;
        public static UnityEngine.GameObject instanceGo;
        private const int K_AWQ_INITIAL_CAPACITY = 20;
        private readonly List<WorkRequest> mAsyncWorkQueue;
        private readonly List<WorkRequest> mCurrentFrameWork = new(K_AWQ_INITIAL_CAPACITY);
        private readonly int mMainThreadID;
        
        private MainThreadDispatcher(int mainThreadID) {
            this.mAsyncWorkQueue = new List<WorkRequest>(K_AWQ_INITIAL_CAPACITY);
            this.mMainThreadID = mainThreadID;
        }

        private MainThreadDispatcher(List<WorkRequest> queue, int mainThreadID) {
            this.mAsyncWorkQueue = queue;
            this.mMainThreadID = mainThreadID;
        }

        public static void Send(MainThreadCallback callback, void* state) {
            instance.Send_INTERNAL(callback, state);
        }
        
        // Send will process the call synchronously. If the call is processed on the main thread, we'll invoke it
        // directly here. If the call is processed on another thread it will be queued up like POST to be executed
        // on the main thread and it will wait. Once the main thread processes the work we can continue
        public void Send_INTERNAL(MainThreadCallback callback, void* state) {
            if (this.mMainThreadID == System.Threading.Thread.CurrentThread.ManagedThreadId) {
                callback(state);
            } else {
                if (instanceGo == null) throw new System.Exception("Dispatcher instanceGo is required");
                //using (var waitHandle = new ManualResetEvent(false)) {
                var spinLock = _make(new LockSpinner());
                spinLock.ptr->LockWhile();
                lock (this.mAsyncWorkQueue) {
                    this.mAsyncWorkQueue.Add(new WorkRequest(callback, state, spinLock.ptr));
                }

                var max = 100_000;
                while (spinLock.ptr->IsLocked == true) {
                    // wait for unlock
                    if (--max == 0) {
                        UnityEngine.Debug.LogError("max iter");
                        break;
                    }
                }

                if (spinLock.ptr->IsLocked == true) {
                    spinLock.ptr->UnlockWhile();
                }
                _free(spinLock);

                //waitHandle.WaitOne();
                //}
            }
        }

        // Exec will execute tasks off the task list
        public void Exec() {
            lock (this.mAsyncWorkQueue) {
                this.mCurrentFrameWork.AddRange(this.mAsyncWorkQueue);
                this.mAsyncWorkQueue.Clear();
            }

            // When you invoke work, remove it from the list to stop it being triggered again (case 1213602)
            while (this.mCurrentFrameWork.Count > 0) {
                var work = this.mCurrentFrameWork[0];
                this.mCurrentFrameWork.RemoveAt(0);
                work.Invoke();
            }
        }
        
        [UnityEngine.RuntimeInitializeOnLoadMethodAttribute(UnityEngine.RuntimeInitializeLoadType.BeforeSplashScreen)]
        public static void Initialize() {
            
            CustomModules.RegisterResetPass(InitializeSynchronizationContext);
            
        }

        // SynchronizationContext must be set before any user code is executed. This is done on
        // Initial domain load and domain reload at MonoManager ReloadAssembly
        private static void InitializeSynchronizationContext() {
            instance = new MainThreadDispatcher(System.Threading.Thread.CurrentThread.ManagedThreadId);
            var go = new UnityEngine.GameObject("DispatcherMono", typeof(DispatcherMono));
            go.hideFlags = UnityEngine.HideFlags.HideAndDontSave;
            instanceGo = go;
        }

        public static void ExecuteTasks() {
            instance.Exec();
        }

        private readonly struct WorkRequest {

            private readonly MainThreadCallback mDelegateCallback;
            private readonly void* mDelegateState;
            private readonly LockSpinner* spinLock;

            public WorkRequest(MainThreadCallback callback, void* state, LockSpinner* spinLock) {
                this.mDelegateCallback = callback;
                this.mDelegateState = state;
                this.spinLock = spinLock;
            }

            public void Invoke() {
                try {
                    this.mDelegateCallback(this.mDelegateState);
                } finally {
                    if (this.spinLock->IsLocked == true) {
                        this.spinLock->UnlockWhile();
                    }
                }
            }

        }

    }

}