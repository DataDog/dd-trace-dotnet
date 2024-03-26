// <copyright file="ApplyStates.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.RemoteConfigurationManagement;

internal static class ApplyStates
{
    public const uint UNACKNOWLEDGED = 1;
    public const uint ACKNOWLEDGED = 2;
    public const uint ERROR = 3;
}
