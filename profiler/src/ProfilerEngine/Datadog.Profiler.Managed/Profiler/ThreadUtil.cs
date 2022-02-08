// <copyright file="ThreadUtil.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Runtime.InteropServices;
using System.Threading;

using Datadog.Util;

namespace Datadog.Profiler
{
    internal static class ThreadUtil
    {
        private static NativeInterop.ManagedCallbackRegistry.SetCurrentManagedThreadName.Delegate_t _setCurrentManagedThreadName;

        public static void EnsureSetCurrentManagedThreadNameNativeCallbackInitialized()
        {
            NativeInterop.ManagedCallbackRegistry.SetCurrentManagedThreadName.Delegate_t setCurrentManagedThreadName = ThreadUtil.SetCurrentManagedThreadName;

            NativeInterop.ManagedCallbackRegistry.SetCurrentManagedThreadName.Delegate_t prevCallback
                                    = Interlocked.CompareExchange(ref _setCurrentManagedThreadName, setCurrentManagedThreadName, null);

            if (prevCallback == null)
            {
                NativeInterop.ManagedCallbackRegistry.SetCurrentManagedThreadName.Set(_setCurrentManagedThreadName);
            }
        }

        public static uint SetCurrentManagedThreadName(IntPtr pThreadNameCharArr)
        {
            try
            {
                if (pThreadNameCharArr == IntPtr.Zero)
                {
                    return HResult.E_INVALIDARG;
                }

                string threadName = Marshal.PtrToStringAnsi(pThreadNameCharArr);
                SetCurrentManagedThreadName(threadName);
                return HResult.S_OK;
            }
            catch (Exception ex)
            {
                return HResult.GetFailureCode(ex);
            }
        }

        public static void SetCurrentManagedThreadName(string threadName)
        {
            if (!string.IsNullOrWhiteSpace(threadName))
            {
                Thread.CurrentThread.Name = threadName.Trim();
            }
        }
    }
}