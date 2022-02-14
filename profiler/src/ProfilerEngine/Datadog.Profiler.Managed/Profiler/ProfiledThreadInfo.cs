// <copyright file="ProfiledThreadInfo.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;

namespace Datadog.Profiler
{
    public class ProfiledThreadInfo : ProfiledEntityInfoBase
    {
        private readonly uint _profilerThreadInfoId;

        public ProfiledThreadInfo(uint profilerThreadInfoId, int providerSessionId)
            : base(providerSessionId)
        {
            _profilerThreadInfoId = profilerThreadInfoId;

            this.ClrThreadId = 0;
            this.OsThreadId = 0;
            this.OsThreadHandle = IntPtr.Zero;
            this.ThreadName = string.Empty;
            this.ThreadDescription = string.Empty;
        }

        public uint ProfilerThreadInfoId
        {
            get { return _profilerThreadInfoId; }
        }

        public ulong ClrThreadId { get; private set; }
        public uint OsThreadId { get; private set; }
        public string ThreadIdString { get; private set; }
        public IntPtr OsThreadHandle { get; private set; }
        public string ThreadName { get; private set; }
        public string ThreadDescription { get; private set; }

        public static string FormatIdForUnknownThread(uint profilerThreadInfoId)
        {
            return FormatId(profilerThreadInfoId, osThreadId: 0);
        }

        public static string FormatDescriptionForUnknownThread(uint profilerThreadInfoId)
        {
            string profilerThreadInfoIdStr = profilerThreadInfoId.ToString("D");
            return "Unknown or short-lived thread [<" + profilerThreadInfoIdStr + ">]";
        }

        public void UpdateProperties(int providerSessionId, ulong clrThreadId, uint osThreadId, IntPtr osThreadHandle, string threadName)
        {
            ProviderSessionId = providerSessionId;

            ClrThreadId = clrThreadId;
            OsThreadId = osThreadId;
            ThreadIdString = FormatId(_profilerThreadInfoId, osThreadId);
            OsThreadHandle = osThreadHandle;
            ThreadName = threadName;
            ThreadDescription = FormatDescription(threadName, osThreadId);
        }

        private static string FormatId(uint profilerThreadInfoId, uint osThreadId)
        {
            string profilerThreadInfoIdStr = profilerThreadInfoId.ToString("D");
            string osThreadIdStr = osThreadId.ToString("D");

            return $"<{profilerThreadInfoIdStr}> [#{osThreadIdStr}]";
        }

        private static string FormatDescription(string threadName, uint osThreadId)
        {
            string sanitizedThreadName = string.IsNullOrEmpty(threadName)
                                                    // Need to make this more nuanced, when we start collecting stacks for non-managed threads!
                                                    ? "Managed thread (name unknown)"
                                                    : threadName;

            string osThreadIdStr = osThreadId.ToString("D");

            return $"{sanitizedThreadName} [#{osThreadIdStr}]";
        }
    }
}