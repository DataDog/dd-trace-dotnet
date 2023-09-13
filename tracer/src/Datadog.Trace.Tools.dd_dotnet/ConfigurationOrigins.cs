// <copyright file="ConfigurationOrigins.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

// ReSharper disable once CheckNamespace - Needed for compatibility with linked files
namespace Datadog.Trace.Configuration.Telemetry;

public enum ConfigurationOrigins
{
    Default,
    AgentUri
}
