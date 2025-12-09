// <copyright file="Log.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;

namespace Datadog.FleetInstaller;

internal static class Log
{
    public static ILogger Instance { get; } = new SimpleLogger();

    private sealed class SimpleLogger : ILogger
    {
        public void WriteInfo(string message)
        {
            Console.WriteLine($"INFO:  {message}");
        }

        public void WriteWarning(string message)
        {
            Console.WriteLine($"WARNING:  {message}");
        }

        public void WriteError(string message)
        {
            Console.Error.WriteLine($"ERROR: {message}");
        }

        public void WriteError(Exception ex, string message)
        {
            WriteError($"{message}: {ex}");
        }
    }
}
