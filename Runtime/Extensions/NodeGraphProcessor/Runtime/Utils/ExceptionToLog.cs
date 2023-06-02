using UnityEngine;
using System;

namespace ME.BECS.Extensions.GraphProcessor
{
    public static class ExceptionToLog
    {
        public static void Call<T>(Action<T> a, T state)
        {
#if UNITY_EDITOR
            try
            {
#endif
                a?.Invoke(state);
#if UNITY_EDITOR
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
            }
#endif
        }
    }
}
