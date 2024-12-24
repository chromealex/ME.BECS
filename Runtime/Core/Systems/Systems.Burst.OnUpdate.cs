namespace ME.BECS {

    using static Cuts;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Burst;
    using UnityEngine.Scripting;
    using BURST = Unity.Burst.BurstCompileAttribute;
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;

    internal static unsafe class BurstCompileOnUpdateNoBurst<T> where T : unmanaged, IUpdate {

        [Preserve]
        [AOT.MonoPInvokeCallbackAttribute(typeof(FunctionPointerDelegate))]
        public static void CallNoBurst(void* systemData, ref SystemContext context) {

            _ptrToStruct(systemData, out T tempData);
            tempData.OnUpdate(ref context);
            _structToPtr(ref tempData, systemData);

        }

        [INLINE(256)]
        [Preserve]
        public static void MakeMethod(Node* node) {

            BurstCompileMethods.MakeMethodNoBurst<T, FunctionPointerDelegate>(nameof(IAwake.OnAwake), node->dataAwake.ptr, CallNoBurst);
            
        }

    }

    [BURST(FloatPrecision.High, FloatMode.Deterministic, CompileSynchronously = true, Debug = false)]
    internal static unsafe class BurstCompileOnUpdate<T> where T : unmanaged, IUpdate {
   
        [Preserve]
        [BURST(FloatPrecision.High, FloatMode.Deterministic, CompileSynchronously = true, Debug = false)]
        [AOT.MonoPInvokeCallbackAttribute(typeof(FunctionPointerDelegate))]
        private static void Call(void* systemData, ref SystemContext context) {

            _ptrToStruct(systemData, out T tempData);
            tempData.OnUpdate(ref context);
            _structToPtr(ref tempData, systemData);

        }
        
        [INLINE(256)]
        [Preserve]
        public static void MakeMethod(Node* node) {

            BurstCompileMethods.MakeMethod<T, FunctionPointerDelegate>(nameof(IUpdate.OnUpdate), node->dataUpdate.ptr, Call, BurstCompileOnUpdateNoBurst<T>.CallNoBurst);

        }
        
    }

}