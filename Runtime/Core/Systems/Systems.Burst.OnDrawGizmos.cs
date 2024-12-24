namespace ME.BECS {

    using static Cuts;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Burst;
    using UnityEngine.Scripting;
    using BURST = Unity.Burst.BurstCompileAttribute;
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;

    internal static unsafe class BurstCompileOnDrawGizmosNoBurst<T> where T : unmanaged, IDrawGizmos {

        [Preserve]
        [AOT.MonoPInvokeCallbackAttribute(typeof(FunctionPointerDelegate))]
        public static void CallNoBurst(void* systemData, ref SystemContext context) {

            _ptrToStruct(systemData, out T tempData);
            tempData.OnDrawGizmos(ref context);
            _structToPtr(ref tempData, systemData);

        }

        [INLINE(256)]
        [Preserve]
        public static void MakeMethod(Node* node) {

            BurstCompileMethods.MakeMethodNoBurst<T, FunctionPointerDelegate>(nameof(IDrawGizmos.OnDrawGizmos), node->dataDrawGizmos.ptr, CallNoBurst);
            
        }

    }

    [BURST(FloatPrecision.High, FloatMode.Deterministic, CompileSynchronously = true, Debug = false)]
    internal static unsafe class BurstCompileOnDrawGizmos<T> where T : unmanaged, IDrawGizmos {
   
        [Preserve]
        [BURST(FloatPrecision.High, FloatMode.Deterministic, CompileSynchronously = true, Debug = false)]
        [AOT.MonoPInvokeCallbackAttribute(typeof(FunctionPointerDelegate))]
        public static void Call(void* systemData, ref SystemContext context) {

            _ptrToStruct(systemData, out T tempData);
            tempData.OnDrawGizmos(ref context);
            _structToPtr(ref tempData, systemData);

        }
        
        [INLINE(256)]
        [Preserve]
        public static void MakeMethod(Node* node) {

            BurstCompileMethods.MakeMethod<T, FunctionPointerDelegate>(nameof(IDrawGizmos.OnDrawGizmos), node->dataDrawGizmos.ptr, Call, BurstCompileOnDrawGizmosNoBurst<T>.CallNoBurst);
            
        }

    }

}