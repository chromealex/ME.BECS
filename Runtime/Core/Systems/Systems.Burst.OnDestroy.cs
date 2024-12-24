namespace ME.BECS {

    using static Cuts;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Burst;
    using UnityEngine.Scripting;
    using BURST = Unity.Burst.BurstCompileAttribute;
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;

    internal static unsafe class BurstCompileOnDestroyNoBurst<T> where T : unmanaged, IDestroy {

        [Preserve]
        [AOT.MonoPInvokeCallbackAttribute(typeof(FunctionPointerDelegate))]
        public static void CallNoBurst(void* systemData, ref SystemContext context) {

            _ptrToStruct(systemData, out T tempData);
            tempData.OnDestroy(ref context);
            _structToPtr(ref tempData, systemData);

        }

        [INLINE(256)]
        [Preserve]
        public static void MakeMethod(Node* node) {

            BurstCompileMethods.MakeMethodNoBurst<T, FunctionPointerDelegate>(nameof(IAwake.OnAwake), node->dataAwake.ptr, CallNoBurst);
            
        }

    }

    [BURST(FloatPrecision.High, FloatMode.Deterministic, CompileSynchronously = true, Debug = false)]
    internal static unsafe class BurstCompileOnDestroy<T> where T : unmanaged, IDestroy {
   
        [Preserve]
        [BURST(FloatPrecision.High, FloatMode.Deterministic, CompileSynchronously = true, Debug = false)]
        [AOT.MonoPInvokeCallbackAttribute(typeof(FunctionPointerDelegate))]
        private static void Call(void* systemData, ref SystemContext context) {

            _ptrToStruct(systemData, out T tempData);
            tempData.OnDestroy(ref context);
            _structToPtr(ref tempData, systemData);

        }
        
        [INLINE(256)]
        [Preserve]
        public static void MakeMethod(Node* node) {

            BurstCompileMethods.MakeMethod<T, FunctionPointerDelegate>(nameof(IDestroy.OnDestroy), node->dataDestroy.ptr, Call, BurstCompileOnDestroyNoBurst<T>.CallNoBurst);

        }

    }

}