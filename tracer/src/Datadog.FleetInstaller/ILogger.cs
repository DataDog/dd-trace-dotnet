// <copyright file="ILogger.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;

namespace Datadog.FleetInstaller;

internal interface ILogger
{
    void WriteInfo(string message);

    void WriteWarning(string message);

    void WriteError(string message);

    void WriteError(Exception ex, string message);
}
