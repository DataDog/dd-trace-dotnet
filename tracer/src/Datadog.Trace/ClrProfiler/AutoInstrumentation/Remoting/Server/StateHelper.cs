// <copyright file="StateHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK

#nullable enable

using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Messaging;
using System.Threading;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Remoting.Server
{
    internal static class StateHelper
    {
        internal static readonly AsyncLocal<ITransportHeaders?> ActiveRequestHeaders = new();
    }
}
#endif
