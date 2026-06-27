// <copyright file="FlowRecorderSettings.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.Configuration;
using Datadog.Trace.Util;

namespace Datadog.Trace.Debugger.LiveDebuggerPoc
{
    internal class FlowRecorderSettings
    {
        internal const int DefaultBufferSize = 100_000;

        internal FlowRecorderSettings(bool enabled, string? outputPath, int bufferSize)
        {
            Enabled = enabled;
            OutputPath = outputPath;
            BufferSize = bufferSize;
        }

        public bool Enabled { get; }

        public string? OutputPath { get; }

        public int BufferSize { get; }

        public static FlowRecorderSettings FromEnvironment()
        {
            var enabled = IsTrue(EnvironmentHelpers.GetEnvironmentVariable(ConfigurationKeys.InternalDebuggerFlowRecorderEnabled));
            var outputPath = EnvironmentHelpers.GetEnvironmentVariable(ConfigurationKeys.InternalDebuggerFlowRecorderOutputPath);
            var bufferSizeValue = EnvironmentHelpers.GetEnvironmentVariable(ConfigurationKeys.InternalDebuggerFlowRecorderBufferSize);
            var bufferSize = DefaultBufferSize;

            if (int.TryParse(bufferSizeValue, out var parsedBufferSize) && parsedBufferSize > 0)
            {
                bufferSize = parsedBufferSize;
            }

            return new FlowRecorderSettings(enabled, StringUtil.IsNullOrEmpty(outputPath) ? null : outputPath, bufferSize);
        }

        private static bool IsTrue(string? value)
        {
            return value is not null &&
                   (string.Equals(value, "1", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(value, "true", StringComparison.OrdinalIgnoreCase));
        }
    }
}
