// <copyright file="LiveDebuggerProduct.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Debugger.Helpers;
using Datadog.Trace.RemoteConfigurationManagement;

namespace Datadog.Trace.Debugger;

internal class LiveDebuggerProduct : Product
{
    private readonly string _serviceName;
    private readonly string _uuid;

    public LiveDebuggerProduct(string serviceName)
    {
        _uuid = serviceName.ToUUID();
        _serviceName = serviceName;
    }

    public static string ProductName => "LIVE_DEBUGGING";

    public override string Name => ProductName;

    protected override bool RemoteConfigurationPredicate(RemoteConfiguration configuration)
    {
        return configuration.Path.Id == _serviceName || configuration.Path.Id == _uuid;
    }
}
