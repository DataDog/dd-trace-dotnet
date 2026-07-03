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
        internal const int DefaultValueBufferSize = 10_000;
        internal const int DefaultMaxStringLength = 256;
        internal const int DefaultMaxCollectionItems = 3;
        internal const int DefaultMaxStackLength = 2_048;
        internal const int DefaultMaxEventsPerOperation = 25_000;
        internal const int DefaultMaxDepth = 128;
        internal const int DefaultMaxDurationMs = 30_000;
        internal const int DefaultMaxUniqueMethodsPerOperation = 2_000;

        internal FlowRecorderSettings(
            bool enabled,
            string? outputPath,
            int bufferSize,
            string? triggerReason = null,
            string? root = null,
            FlowValueCaptureMode valueCaptureMode = FlowValueCaptureMode.Off,
            string? valueCaptureMethodFilter = null,
            int valueBufferSize = DefaultValueBufferSize,
            int maxStringLength = DefaultMaxStringLength,
            int maxCollectionItems = DefaultMaxCollectionItems,
            int maxStackLength = DefaultMaxStackLength,
            int maxEventsPerOperation = DefaultMaxEventsPerOperation,
            int maxDepth = DefaultMaxDepth,
            int maxDurationMs = DefaultMaxDurationMs,
            int maxUniqueMethodsPerOperation = DefaultMaxUniqueMethodsPerOperation,
            bool throwOnEnter = false,
            bool throwOnExit = false,
            bool skipEventEnqueue = false,
            bool skipTraceCorrelation = false,
            bool disableFlowContext = false,
            bool skipMethodRegistration = false,
            bool allowRecordingWithoutOperation = false)
        {
            Enabled = enabled;
            OutputPath = outputPath;
            BufferSize = bufferSize;
            TriggerReason = triggerReason;
            Root = root;
            ValueCaptureMode = valueCaptureMode;
            ValueCaptureMethodFilter = valueCaptureMethodFilter;
            ValueBufferSize = valueBufferSize;
            MaxStringLength = maxStringLength;
            MaxCollectionItems = maxCollectionItems;
            MaxStackLength = maxStackLength;
            MaxEventsPerOperation = maxEventsPerOperation;
            MaxDepth = maxDepth;
            MaxDurationMs = maxDurationMs;
            MaxUniqueMethodsPerOperation = maxUniqueMethodsPerOperation;
            ThrowOnEnter = throwOnEnter;
            ThrowOnExit = throwOnExit;
            SkipEventEnqueue = skipEventEnqueue;
            SkipTraceCorrelation = skipTraceCorrelation;
            DisableFlowContext = disableFlowContext;
            SkipMethodRegistration = skipMethodRegistration;
            AllowRecordingWithoutOperation = allowRecordingWithoutOperation;
        }

        public bool Enabled { get; }

        public string? OutputPath { get; }

        public int BufferSize { get; }

        public string? TriggerReason { get; }

        public string? Root { get; }

        public FlowValueCaptureMode ValueCaptureMode { get; }

        public string? ValueCaptureMethodFilter { get; }

        public int ValueBufferSize { get; }

        public int MaxStringLength { get; }

        public int MaxCollectionItems { get; }

        public int MaxStackLength { get; }

        public int MaxEventsPerOperation { get; }

        public int MaxDepth { get; }

        public int MaxDurationMs { get; }

        public int MaxUniqueMethodsPerOperation { get; }

        public bool ThrowOnEnter { get; }

        public bool ThrowOnExit { get; }

        public bool SkipEventEnqueue { get; }

        public bool SkipTraceCorrelation { get; }

        public bool DisableFlowContext { get; }

        public bool SkipMethodRegistration { get; }

        public bool AllowRecordingWithoutOperation { get; }

        public static FlowRecorderSettings FromEnvironment()
        {
            var enabled = IsTrue(EnvironmentHelpers.GetEnvironmentVariable(ConfigurationKeys.InternalDebuggerFlowRecorderEnabled));
            var outputPath = EnvironmentHelpers.GetEnvironmentVariable(ConfigurationKeys.InternalDebuggerFlowRecorderOutputPath);
            var triggerReason = EnvironmentHelpers.GetEnvironmentVariable(ConfigurationKeys.InternalDebuggerFlowRecorderTriggerReason);
            var root = EnvironmentHelpers.GetEnvironmentVariable(ConfigurationKeys.InternalDebuggerFlowRecorderRoot);
            var bufferSizeValue = EnvironmentHelpers.GetEnvironmentVariable(ConfigurationKeys.InternalDebuggerFlowRecorderBufferSize);
            var valueCaptureModeValue = EnvironmentHelpers.GetEnvironmentVariable(ConfigurationKeys.InternalDebuggerFlowRecorderCaptureValues);
            var valueCaptureMethodFilter = EnvironmentHelpers.GetEnvironmentVariable(ConfigurationKeys.InternalDebuggerFlowRecorderCaptureValueMethods);
            var valueBufferSizeValue = EnvironmentHelpers.GetEnvironmentVariable(ConfigurationKeys.InternalDebuggerFlowRecorderValueBufferSize);
            var maxStringLengthValue = EnvironmentHelpers.GetEnvironmentVariable(ConfigurationKeys.InternalDebuggerFlowRecorderMaxStringLength);
            var maxCollectionItemsValue = EnvironmentHelpers.GetEnvironmentVariable(ConfigurationKeys.InternalDebuggerFlowRecorderMaxCollectionItems);
            var maxStackLengthValue = EnvironmentHelpers.GetEnvironmentVariable(ConfigurationKeys.InternalDebuggerFlowRecorderMaxStackLength);
            var maxEventsPerOperationValue = EnvironmentHelpers.GetEnvironmentVariable(ConfigurationKeys.InternalDebuggerFlowRecorderMaxEventsPerOperation);
            var maxDepthValue = EnvironmentHelpers.GetEnvironmentVariable(ConfigurationKeys.InternalDebuggerFlowRecorderMaxDepth);
            var maxDurationMsValue = EnvironmentHelpers.GetEnvironmentVariable(ConfigurationKeys.InternalDebuggerFlowRecorderMaxDurationMs);
            var maxUniqueMethodsPerOperationValue = EnvironmentHelpers.GetEnvironmentVariable(ConfigurationKeys.InternalDebuggerFlowRecorderMaxUniqueMethodsPerOperation);
            var throwOnEnter = IsTrue(EnvironmentHelpers.GetEnvironmentVariable(ConfigurationKeys.InternalDebuggerFlowRecorderThrowOnEnter));
            var throwOnExit = IsTrue(EnvironmentHelpers.GetEnvironmentVariable(ConfigurationKeys.InternalDebuggerFlowRecorderThrowOnExit));
            var skipEventEnqueue = IsTrue(EnvironmentHelpers.GetEnvironmentVariable(ConfigurationKeys.InternalDebuggerFlowRecorderSkipEventEnqueue));
            var skipTraceCorrelation = IsTrue(EnvironmentHelpers.GetEnvironmentVariable(ConfigurationKeys.InternalDebuggerFlowRecorderSkipTraceCorrelation));
            var disableFlowContext = IsTrue(EnvironmentHelpers.GetEnvironmentVariable(ConfigurationKeys.InternalDebuggerFlowRecorderDisableFlowContext));
            var skipMethodRegistration = IsTrue(EnvironmentHelpers.GetEnvironmentVariable(ConfigurationKeys.InternalDebuggerFlowRecorderSkipMethodRegistration));
            var allowRecordingWithoutOperation = IsTrue(EnvironmentHelpers.GetEnvironmentVariable(ConfigurationKeys.InternalDebuggerFlowRecorderAllowRecordingWithoutOperation));

            return new FlowRecorderSettings(
                enabled,
                StringUtil.IsNullOrEmpty(outputPath) ? null : outputPath,
                ParsePositiveInt(bufferSizeValue, DefaultBufferSize),
                StringUtil.IsNullOrEmpty(triggerReason) ? null : triggerReason,
                StringUtil.IsNullOrEmpty(root) ? null : root,
                ParseValueCaptureMode(valueCaptureModeValue),
                StringUtil.IsNullOrEmpty(valueCaptureMethodFilter) ? null : valueCaptureMethodFilter,
                ParsePositiveInt(valueBufferSizeValue, DefaultValueBufferSize),
                ParsePositiveInt(maxStringLengthValue, DefaultMaxStringLength),
                ParsePositiveInt(maxCollectionItemsValue, DefaultMaxCollectionItems),
                ParsePositiveInt(maxStackLengthValue, DefaultMaxStackLength),
                ParsePositiveInt(maxEventsPerOperationValue, DefaultMaxEventsPerOperation),
                ParsePositiveInt(maxDepthValue, DefaultMaxDepth),
                ParsePositiveInt(maxDurationMsValue, DefaultMaxDurationMs),
                ParsePositiveInt(maxUniqueMethodsPerOperationValue, DefaultMaxUniqueMethodsPerOperation),
                throwOnEnter,
                throwOnExit,
                skipEventEnqueue,
                skipTraceCorrelation,
                disableFlowContext,
                skipMethodRegistration,
                allowRecordingWithoutOperation);
        }

        private static bool IsTrue(string? value)
        {
            return value is not null &&
                   (string.Equals(value, "1", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(value, "true", StringComparison.OrdinalIgnoreCase));
        }

        private static int ParsePositiveInt(string? value, int defaultValue)
        {
            return int.TryParse(value, out var parsed) && parsed > 0 ? parsed : defaultValue;
        }

        private static FlowValueCaptureMode ParseValueCaptureMode(string? value)
        {
            if (StringUtil.IsNullOrEmpty(value))
            {
                return FlowValueCaptureMode.Off;
            }

            if (string.Equals(value, "exceptions", StringComparison.OrdinalIgnoreCase))
            {
                return FlowValueCaptureMode.Exceptions;
            }

            if (string.Equals(value, "entry", StringComparison.OrdinalIgnoreCase))
            {
                return FlowValueCaptureMode.Entry;
            }

            if (string.Equals(value, "exit", StringComparison.OrdinalIgnoreCase))
            {
                return FlowValueCaptureMode.Exit;
            }

            if (string.Equals(value, "all", StringComparison.OrdinalIgnoreCase))
            {
                return FlowValueCaptureMode.All;
            }

            return FlowValueCaptureMode.Off;
        }
    }
}
