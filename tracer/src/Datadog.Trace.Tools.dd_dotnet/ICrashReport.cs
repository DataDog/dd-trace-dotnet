// <copyright file="ICrashReport.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Datadog.Trace.Tools.dd_dotnet;

[NativeObject]
internal unsafe interface ICrashReport : IUnknown
{
    public static Guid Guid = new Guid(0x3B3BA8A9u, 0xF807, 0x43BF, 0xA3, 0xA9, 0x55, 0xE3, 0x69, 0xC0, 0xC5, 0x32); // 3B3BA8A9-F807-43BF-A3A9-55E369C0C532

    int Initialize();

    void GetLastError(out IntPtr message, out int length);

    void Panic();

    int SetSignalInfo(int signal, int code);

    int ResolveStacks(int crashingThreadId, IntPtr resolveCallback, IntPtr context, out bool isSuspicious);

    int SetMetadata(IntPtr libraryName, IntPtr libraryVersion, IntPtr family, Tag* tags, int tagsCount);

    int Send();

    int WriteToFile(IntPtr url);

    [Obsolete("You're aware it's going to crash the process, right?")]
    int CrashProcess();

    int SetCrashMessage(IntPtr message);

    [StructLayout(LayoutKind.Sequential)]
    internal struct Tag
    {
        public IntPtr Key;
        public IntPtr Value;
    }
}
