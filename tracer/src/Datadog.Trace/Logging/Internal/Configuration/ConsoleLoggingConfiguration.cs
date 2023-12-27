// <copyright file="ConsoleLoggingConfiguration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Logging.Internal.Configuration;

internal readonly struct ConsoleLoggingConfiguration
{
    public readonly string MessageTemplate;
    public readonly int BufferSize;

    public ConsoleLoggingConfiguration(string formatString, int bufferSize)
    {
        MessageTemplate = formatString;
        BufferSize = bufferSize;
    }
}
