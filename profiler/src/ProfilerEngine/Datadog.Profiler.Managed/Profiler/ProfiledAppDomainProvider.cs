// <copyright file="ProfiledAppDomainProvider.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Text;

namespace Datadog.Profiler
{
    internal sealed class ProfiledAppDomainProvider : ProfiledEntityInfoProviderBase<ulong, ProfiledAppDomainInfo>
    {
        private const int DefaultAppDomainNameLength = 260;
        private const int CacheCompactionTriggerAfterSessions = 10;
        private const int CacheCompactionTriggerWhenCacheGrewBy = 100;

        public ProfiledAppDomainProvider()
            : base(CacheCompactionTriggerAfterSessions, CacheCompactionTriggerWhenCacheGrewBy)
        {
        }

        protected override bool TryGetEntityInfoFromNative(ulong profilerAppDomainId, ref ProfiledAppDomainInfo appDomainInfo)
        {
            int appDomainNameBuffSize = DefaultAppDomainNameLength;
            bool isKnownAppDomain = TryGetAppDomainFromNativeCore(
                                        profilerAppDomainId,
                                        appDomainNameBuffSize,
                                        out uint actualAppDomainNameLen,
                                        out StringBuilder appDomainNameBuff,
                                        out ulong appDomainProcessId);

            while (isKnownAppDomain && actualAppDomainNameLen > appDomainNameBuffSize - 1)
            {
                appDomainNameBuffSize = (int)actualAppDomainNameLen;
                isKnownAppDomain = TryGetAppDomainFromNativeCore(
                                        profilerAppDomainId,
                                        appDomainNameBuffSize,
                                        out actualAppDomainNameLen,
                                        out appDomainNameBuff,
                                        out appDomainProcessId);
            }

            if (!isKnownAppDomain)
            {
                return false;
            }

            if (appDomainInfo == null)
            {
                appDomainInfo = new ProfiledAppDomainInfo(profilerAppDomainId, SessionId);
            }

            appDomainInfo.UpdateProperties(SessionId, appDomainProcessId, appDomainNameBuff.ToString());
            return true;
        }

        private bool TryGetAppDomainFromNativeCore(
                        ulong profilerAppDomainId,
                        int appDomainNameBuffSize,
                        out uint actualAppDomainNameLen,
                        out StringBuilder appDomainNameBuff,
                        out ulong appDomainProcessId)
        {
            actualAppDomainNameLen = 0;
            appDomainNameBuff = new StringBuilder(capacity: appDomainNameBuffSize + 1);
            appDomainProcessId = 0;

            bool success = false;
            try
            {
                success = NativeInterop.ResolveAppDomainInfoSymbols(
                                            profilerAppDomainId,
                                            (uint)appDomainNameBuffSize,
                                            ref actualAppDomainNameLen,
                                            appDomainNameBuff,
                                            ref appDomainProcessId);
            }
            catch (ClrShutdownException)
            {
                // this is expected when managed code runs AFTER the CLR shutdown
                throw;
            }
            catch (Exception ex)
            {
                Log.Error(Log.WithCallInfo(this.GetType().Name), ex);
            }

            return success;
        }
    }
}