// <copyright file="ProfiledThreadInfoProvider.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Text;

namespace Datadog.Profiler
{
    internal sealed class ProfiledThreadInfoProvider : ProfiledEntityInfoProviderBase<uint, ProfiledThreadInfo>
    {
        private const int DefaultThreadNameBuffSize = 260;
        private const int CacheCompactionTriggerAfterSessions = 10;
        private const int CacheCompactionTriggerWhenCacheGrewBy = 100;

        public ProfiledThreadInfoProvider()
            : base(CacheCompactionTriggerAfterSessions, CacheCompactionTriggerWhenCacheGrewBy)
        {
        }

        protected override bool TryGetEntityInfoFromNative(uint profilerThreadInfoId, ref ProfiledThreadInfo threadInfo)
        {
            int threadNameBuffSize = DefaultThreadNameBuffSize;
            bool isKnownThread = TryGetThreadInfoFromNativeCore(
                                    profilerThreadInfoId,
                                    threadNameBuffSize,
                                    out ulong clrThreadId,
                                    out uint osThreadId,
                                    out IntPtr osThreadHandle,
                                    out StringBuilder threadNameBuff,
                                    out uint actualThreadNameLen);

            while (isKnownThread && actualThreadNameLen > threadNameBuffSize - 1)
            {
                threadNameBuffSize = (int)actualThreadNameLen;
                isKnownThread = TryGetThreadInfoFromNativeCore(
                                    profilerThreadInfoId,
                                    threadNameBuffSize,
                                    out clrThreadId,
                                    out osThreadId,
                                    out osThreadHandle,
                                    out threadNameBuff,
                                    out actualThreadNameLen);
            }

            if (!(isKnownThread))
            {
                return false;
            }

            if (threadInfo == null)
            {
                threadInfo = new ProfiledThreadInfo(profilerThreadInfoId, SessionId);
            }

            threadInfo.UpdateProperties(SessionId, clrThreadId, osThreadId, osThreadHandle, threadNameBuff.ToString());
            return true;
        }

        private bool TryGetThreadInfoFromNativeCore(
                        uint profilerThreadInfoId,
                        int threadNameBuffSize,
                        out ulong clrThreadId,
                        out uint osThreadId,
                        out IntPtr osThreadHandle,
                        out StringBuilder threadNameBuff,
                        out uint actualThreadNameLen)
        {
            clrThreadId = 0;
            osThreadId = 0;
            osThreadHandle = IntPtr.Zero;
            threadNameBuff = new StringBuilder(capacity: threadNameBuffSize + 1);
            actualThreadNameLen = 0;

            try
            {
                return NativeInterop.GetThreadInfo(
                                        profilerThreadInfoId,
                                        ref clrThreadId,
                                        ref osThreadId,
                                        ref osThreadHandle,
                                        threadNameBuff,
                                        (uint)threadNameBuffSize,
                                        ref actualThreadNameLen);
            }
            catch (ClrShutdownException)
            {
                // this is expected when managed code runs AFTER the CLR shutdown
                throw;
            }
            catch (Exception ex)
            {
                Log.Error(Log.WithCallInfo(this.GetType().Name), ex);
                return false;
            }
        }
    }
}
