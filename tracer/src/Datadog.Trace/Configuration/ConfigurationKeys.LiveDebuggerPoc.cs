// <copyright file="ConfigurationKeys.LiveDebuggerPoc.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Configuration;

internal static partial class ConfigurationKeys
{
    /// <summary>
    /// Internal POC flag for enabling the live debugger flow recorder callbacks.
    /// </summary>
    public const string InternalDebuggerFlowRecorderEnabled = "DD_INTERNAL_DEBUGGER_FLOW_RECORDER_ENABLED";

    /// <summary>
    /// Internal POC file path used when flushing live debugger flow recorder events.
    /// </summary>
    public const string InternalDebuggerFlowRecorderOutputPath = "DD_INTERNAL_DEBUGGER_FLOW_RECORDER_OUTPUT_PATH";

    /// <summary>
    /// Internal POC maximum number of live debugger flow recorder events buffered in memory.
    /// </summary>
    public const string InternalDebuggerFlowRecorderBufferSize = "DD_INTERNAL_DEBUGGER_FLOW_RECORDER_BUFFER_SIZE";

    /// <summary>
    /// Internal POC maximum number of events captured for a recorder operation.
    /// </summary>
    public const string InternalDebuggerFlowRecorderMaxEventsPerOperation = "DD_INTERNAL_DEBUGGER_FLOW_RECORDER_MAX_EVENTS_PER_OPERATION";

    /// <summary>
    /// Internal POC maximum frame depth captured for a recorder operation.
    /// </summary>
    public const string InternalDebuggerFlowRecorderMaxDepth = "DD_INTERNAL_DEBUGGER_FLOW_RECORDER_MAX_DEPTH";

    /// <summary>
    /// Internal POC maximum operation duration in milliseconds.
    /// </summary>
    public const string InternalDebuggerFlowRecorderMaxDurationMs = "DD_INTERNAL_DEBUGGER_FLOW_RECORDER_MAX_DURATION_MS";

    /// <summary>
    /// Internal POC maximum unique method count captured for a recorder operation.
    /// </summary>
    public const string InternalDebuggerFlowRecorderMaxUniqueMethodsPerOperation = "DD_INTERNAL_DEBUGGER_FLOW_RECORDER_MAX_UNIQUE_METHODS_PER_OPERATION";

    /// <summary>
    /// Internal POC fault-injection flag for forcing the live debugger flow recorder enter callback to throw.
    /// </summary>
    public const string InternalDebuggerFlowRecorderThrowOnEnter = "DD_INTERNAL_DEBUGGER_FLOW_RECORDER_THROW_ON_ENTER";

    /// <summary>
    /// Internal POC fault-injection flag for forcing the live debugger flow recorder exit callback to throw.
    /// </summary>
    public const string InternalDebuggerFlowRecorderThrowOnExit = "DD_INTERNAL_DEBUGGER_FLOW_RECORDER_THROW_ON_EXIT";

    /// <summary>
    /// Internal POC value capture mode for the live debugger flow recorder.
    /// </summary>
    public const string InternalDebuggerFlowRecorderCaptureValues = "DD_INTERNAL_DEBUGGER_FLOW_RECORDER_CAPTURE_VALUES";

    /// <summary>
    /// Internal POC method/type/name filter for value-capable live debugger flow recorder instrumentation.
    /// </summary>
    public const string InternalDebuggerFlowRecorderCaptureValueMethods = "DD_INTERNAL_DEBUGGER_FLOW_RECORDER_CAPTURE_VALUE_METHODS";

    /// <summary>
    /// Internal POC maximum number of value records buffered in memory.
    /// </summary>
    public const string InternalDebuggerFlowRecorderValueBufferSize = "DD_INTERNAL_DEBUGGER_FLOW_RECORDER_VALUE_BUFFER_SIZE";

    /// <summary>
    /// Internal POC maximum string length captured by the flow recorder.
    /// </summary>
    public const string InternalDebuggerFlowRecorderMaxStringLength = "DD_INTERNAL_DEBUGGER_FLOW_RECORDER_MAX_STRING_LENGTH";

    /// <summary>
    /// Internal POC maximum collection items captured by the flow recorder.
    /// </summary>
    public const string InternalDebuggerFlowRecorderMaxCollectionItems = "DD_INTERNAL_DEBUGGER_FLOW_RECORDER_MAX_COLLECTION_ITEMS";

    /// <summary>
    /// Internal POC value preview mode for the live debugger flow recorder.
    /// </summary>
    public const string InternalDebuggerFlowRecorderValuePreview = "DD_INTERNAL_DEBUGGER_FLOW_RECORDER_VALUE_PREVIEW";

    /// <summary>
    /// Internal POC maximum object fields previewed by the flow recorder.
    /// </summary>
    public const string InternalDebuggerFlowRecorderMaxObjectFields = "DD_INTERNAL_DEBUGGER_FLOW_RECORDER_MAX_OBJECT_FIELDS";

    /// <summary>
    /// Internal POC maximum child value records captured for one root value by the flow recorder.
    /// </summary>
    public const string InternalDebuggerFlowRecorderMaxChildValuesPerValue = "DD_INTERNAL_DEBUGGER_FLOW_RECORDER_MAX_CHILD_VALUES_PER_VALUE";

    /// <summary>
    /// Internal POC maximum stack trace length captured by the flow recorder.
    /// </summary>
    public const string InternalDebuggerFlowRecorderMaxStackLength = "DD_INTERNAL_DEBUGGER_FLOW_RECORDER_MAX_STACK_LENGTH";

    /// <summary>
    /// Internal POC benchmark-only flag for measuring recorder cost without queue enqueue.
    /// </summary>
    public const string InternalDebuggerFlowRecorderSkipEventEnqueue = "DD_INTERNAL_DEBUGGER_FLOW_RECORDER_SKIP_EVENT_ENQUEUE";

    /// <summary>
    /// Internal POC benchmark-only flag for measuring recorder cost without trace/span correlation lookups.
    /// </summary>
    public const string InternalDebuggerFlowRecorderSkipTraceCorrelation = "DD_INTERNAL_DEBUGGER_FLOW_RECORDER_SKIP_TRACE_CORRELATION";

    /// <summary>
    /// Internal POC benchmark-only flag for measuring recorder cost without flowing frame context through AsyncLocal.
    /// </summary>
    public const string InternalDebuggerFlowRecorderDisableFlowContext = "DD_INTERNAL_DEBUGGER_FLOW_RECORDER_DISABLE_FLOW_CONTEXT";

    /// <summary>
    /// Internal POC benchmark-only flag for measuring recorder cost without runtime method metadata registration.
    /// </summary>
    public const string InternalDebuggerFlowRecorderSkipMethodRegistration = "DD_INTERNAL_DEBUGGER_FLOW_RECORDER_SKIP_METHOD_REGISTRATION";

    /// <summary>
    /// Internal POC flag that allows broad recording without an active recorder operation.
    /// </summary>
    public const string InternalDebuggerFlowRecorderAllowRecordingWithoutOperation = "DD_INTERNAL_DEBUGGER_FLOW_RECORDER_ALLOW_RECORDING_WITHOUT_OPERATION";

    /// <summary>
    /// Internal POC trigger reason used when a recorder operation is manually armed.
    /// </summary>
    public const string InternalDebuggerFlowRecorderTriggerReason = "DD_INTERNAL_DEBUGGER_FLOW_RECORDER_TRIGGER_REASON";

    /// <summary>
    /// Internal POC user-directed root pattern used when a recorder operation is manually armed.
    /// </summary>
    public const string InternalDebuggerFlowRecorderRoot = "DD_INTERNAL_DEBUGGER_FLOW_RECORDER_ROOT";

    /// <summary>
    /// Internal POC benchmark-only native rewrite mode for measuring exception handling overhead.
    /// </summary>
    public const string InternalDebuggerFlowRecorderRewriteMode = "DD_INTERNAL_DEBUGGER_FLOW_RECORDER_REWRITE_MODE";
}
