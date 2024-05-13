// <copyright file="IpcServer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.Ci.Ipc;

internal class IpcServer : IpcDualChannel
{
    public IpcServer(string name)
        : base($"{name}.recv", $"{name}.send")
    {
    }
}
