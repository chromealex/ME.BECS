
namespace ME.BECS.Jobs.Scheduler {
    
    /*
    public static class Utils {
        
        public delegate void ExecuteJobFunction<T>(ref T jobData, System.IntPtr bufferPtr, System.IntPtr bufferRangePatchData, ref Unity.Jobs.LowLevel.Unsafe.JobRanges ranges, int jobIndex);

        public static JobHandle Schedule(ref JobScheduleParameters jobParameters) {
            return SchedulerCore.Schedule(ref jobParameters);
        }

        public static System.IntPtr CreateJobReflectionData<T>(System.Type wrapperJobType, System.Type userJobType, ExecuteJobFunction<T> jobFunction) {
            
        }
        
    }

    public static class SchedulerCore {

        public class ThreadData {

            public int index;

        }
        
        private static System.Threading.Thread[] threads;
        private static System.Collections.Concurrent.ConcurrentQueue<JobScheduleParameters> jobDataQueue;

        public static void Start() {
            threads = new System.Threading.Thread[Unity.Jobs.LowLevel.Unsafe.JobsUtility.MaxJobThreadCount];
            for (int i = 0; i < threads.Length; ++i) {
                threads[i] = new System.Threading.Thread(SchedulerCore.ThreadLogic);
                threads[i].Start(new ThreadData() {
                    index = i,
                });
            }
        }

        public static JobHandle Schedule(ref JobScheduleParameters jobParameters) {
            
            if (jobParameters.scheduleMode == Unity.Jobs.LowLevel.Unsafe.ScheduleMode.Run) {
                // Run in current thread
            } else if (jobParameters.scheduleMode == Unity.Jobs.LowLevel.Unsafe.ScheduleMode.Single) {
                // Looking for the idle thread
                jobDataQueue.Enqueue(jobParameters);
            } else if (jobParameters.scheduleMode == Unity.Jobs.LowLevel.Unsafe.ScheduleMode.Parallel) {
                // Looking for the idle thread
                jobDataQueue.Enqueue(jobParameters);
            }

        }

        private static void ThreadLogic(object data) {

            while (true) {
                if (jobDataQueue.TryDequeue(out var parameters) == true) {
                    
                }
            }
            
        }

        public static void Dispose() {
            for (int i = 0; i < threads.Length; ++i) {
                threads[i].Abort();
            }
        }

    }

    public struct JobHandle {

        

    }

    public unsafe struct JobScheduleParameters {
        
        public Unity.Jobs.JobHandle dependency;
        public Unity.Jobs.LowLevel.Unsafe.ScheduleMode scheduleMode;
        public System.IntPtr reflectionData;
        public System.IntPtr jobDataPtr;

        public JobScheduleParameters(void* jobDataPtr, System.IntPtr reflectionData, Unity.Jobs.JobHandle dependency, Unity.Jobs.LowLevel.Unsafe.ScheduleMode scheduleMode) {
            this.dependency = dependency;
            this.reflectionData = reflectionData;
            this.jobDataPtr = (System.IntPtr)jobDataPtr;
            this.scheduleMode = scheduleMode;
        }

    }
    */
    
}