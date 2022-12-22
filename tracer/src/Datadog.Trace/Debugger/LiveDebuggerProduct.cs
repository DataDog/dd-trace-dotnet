// <copyright file="LiveDebuggerProduct.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using Datadog.Trace.RemoteConfigurationManagement;

namespace Datadog.Trace.Debugger;

internal class LiveDebuggerProduct : Product
{
    public static string ProductName => "LIVE_DEBUGGING";

    public override string Name => ProductName;
}
