// <copyright file="IConfigurationPoller.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Threading.Tasks;

namespace Datadog.Trace.Debugger.Configurations;

internal interface IConfigurationPoller : IDisposable
{
    /// <summary>
    /// Start polling configurations asynchronously in an endless loop.
    /// </summary>
    Task StartPollingAsync();
}
