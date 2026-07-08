// <copyright file="LiveDebuggerPocNativeTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET10_0_OR_GREATER

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests;

public class LiveDebuggerPocNativeTests : TestHelper
{
    private const int FlowRecorderMagic = 0x44464c50;
    private const int FlowRecorderVersion = 6;

    public LiveDebuggerPocNativeTests(ITestOutputHelper output)
        : base("LiveDebuggerPoc.Console", "test/test-applications/debugger", output)
    {
    }

    private enum FlowEventKind : byte
    {
        Enter = 1,
        Exit = 2,
        Exception = 3,
        AsyncEdge = 4,
        Truncated = 5,
        Suppressed = 6
    }

    private enum FlowCapturePhase : byte
    {
        Entry = 1,
        Exit = 2,
        Exception = 3
    }

    private enum FlowValueKind : byte
    {
        This = 1,
        Argument = 2,
        Local = 3,
        Return = 4,
        Exception = 5
    }

    [SkippableFact]
    [Trait("RunOnWindows", "True")]
    public async Task NativeInstrumentAllRecordsBalancedNonAsyncFlowEvents()
    {
        var (capturePath, _) = await RunNativeRecorderSample();

        var capture = ReadFlowCapture(capturePath, requireEvents: true);
        var events = capture.Events;
        events.Should().NotBeEmpty();
        events.Should().OnlyContain(flowEvent => IsKnownKind(flowEvent.Kind));
        events.Should().NotContain(flowEvent => IsManualRecorderMethodId(flowEvent.MethodMetadataIndex), "manual recorder callback ids should be absent in native recording mode");
        capture.Methods.Should().Contain(method => method.DisplayName.Contains("Samples.LiveDebuggerPoc.Console.Program"));

        var enterEvents = events.Where(flowEvent => flowEvent.Kind == FlowEventKind.Enter).ToArray();
        var exitEvents = events.Where(flowEvent => flowEvent.Kind == FlowEventKind.Exit).ToArray();
        enterEvents.Should().NotBeEmpty();
        exitEvents.Should().HaveCount(enterEvents.Length);

        var eventsByFrame = events.Where(flowEvent => flowEvent.Kind != FlowEventKind.AsyncEdge)
                                  .GroupBy(flowEvent => (flowEvent.FlowId, flowEvent.FrameId));
        eventsByFrame.Should().OnlyContain(frameEvents => IsBalancedFrame(frameEvents.ToArray()));
        AssertParentFramesContainChildren(events);

        capture.Operations.Should().Contain(operation => operation.TraceIdLower != 0 || operation.TraceIdUpper != 0);
    }

    [SkippableFact]
    [Trait("RunOnWindows", "True")]
    public async Task NativeInstrumentAllRecordsBalancedAsyncMoveNextFlowEvents()
    {
        var (capturePath, logDirectory) = await RunNativeRecorderSample("async");

        var capture = ReadFlowCapture(capturePath, requireEvents: true);
        var events = capture.Events;
        events.Should().OnlyContain(flowEvent => IsKnownKind(flowEvent.Kind));
        events.Should().NotContain(flowEvent => IsManualRecorderMethodId(flowEvent.MethodMetadataIndex), "manual recorder callback ids should be absent in native recording mode");
        capture.Methods.Should().Contain(method => method.DisplayName.Contains("MoveNext"));
        events.Where(flowEvent => flowEvent.Kind != FlowEventKind.AsyncEdge)
              .GroupBy(flowEvent => (flowEvent.FlowId, flowEvent.FrameId))
              .Should()
              .OnlyContain(frameEvents => IsBalancedFrame(frameEvents.ToArray()));
        AssertParentFramesContainChildren(events);
        AssertAsyncMoveNextStepsShareOperationFlow(events, capture.Methods);
        AssertAsyncEdgesReferenceRecordedOperations(events);

        ReadNativeLogText(logDirectory).Should().Contain("Applying Async Flow Recorder instrumentation");

        var viewerOutput = RunViewer(capturePath);
        viewerOutput.Should().Contain("Async logical operations");
        viewerOutput.Should().Contain("AsyncValueAsync");
        viewerOutput.Should().Contain("AsyncLeafAsync");
    }

    [SkippableFact]
    [Trait("RunOnWindows", "True")]
    public async Task NativeInstrumentAllFastRewriteRecordsBalancedFlowEvents()
    {
        var (capturePath, _) = await RunNativeRecorderSample(rewriteMode: "fast");

        var capture = ReadFlowCapture(capturePath, requireEvents: true);
        var events = capture.Events;
        events.Should().OnlyContain(flowEvent => IsKnownKind(flowEvent.Kind));
        events.Should().NotContain(flowEvent => IsManualRecorderMethodId(flowEvent.MethodMetadataIndex), "manual recorder callback ids should be absent in native recording mode");
        capture.Methods.Should().Contain(method => method.DisplayName.Contains("Samples.LiveDebuggerPoc.Console.Program"));

        var enterEvents = events.Where(flowEvent => flowEvent.Kind == FlowEventKind.Enter).ToArray();
        var exitEvents = events.Where(flowEvent => flowEvent.Kind == FlowEventKind.Exit).ToArray();
        enterEvents.Should().NotBeEmpty();
        exitEvents.Should().HaveCount(enterEvents.Length);
        events.Where(flowEvent => flowEvent.Kind != FlowEventKind.AsyncEdge)
              .GroupBy(flowEvent => (flowEvent.FlowId, flowEvent.FrameId))
              .Should()
              .OnlyContain(frameEvents => IsBalancedFrame(frameEvents.ToArray()));
        AssertParentFramesContainChildren(events);
    }

    [SkippableTheory]
    [Trait("RunOnWindows", "True")]
    [InlineData(ConfigurationKeys.InternalDebuggerFlowRecorderThrowOnEnter)]
    [InlineData(ConfigurationKeys.InternalDebuggerFlowRecorderThrowOnExit)]
    public async Task NativeInstrumentAllSwallowsRecorderCallbackFailures(string faultInjectionKey)
    {
        SetEnvironmentVariable(faultInjectionKey, "true");

        var (capturePath, _) = await RunNativeRecorderSample();

        var events = ReadFlowCapture(capturePath, requireEvents: false).Events;
        AssertEventsHaveKnownKinds(events);
        events.Should().NotContain(flowEvent => IsManualRecorderMethodId(flowEvent.MethodMetadataIndex), "manual recorder callback ids should be absent in native recording mode");

        if (faultInjectionKey == ConfigurationKeys.InternalDebuggerFlowRecorderThrowOnEnter)
        {
            events.Should().BeEmpty("forced Enter failures happen before recorder state or events are published");
        }
        else
        {
            events.Should().NotBeEmpty("forced Exit failures happen after the recorder has cleaned up and enqueued exit events");
            events.Where(flowEvent => flowEvent.Kind != FlowEventKind.AsyncEdge)
                  .GroupBy(flowEvent => (flowEvent.FlowId, flowEvent.FrameId))
                  .Should()
                  .OnlyContain(frameEvents => IsBalancedFrame(frameEvents.ToArray()));
        }
    }

    [SkippableFact]
    [Trait("RunOnWindows", "True")]
    public async Task NativeInstrumentAllCanCaptureExceptionDetailsAndArmedValues()
    {
        SetEnvironmentVariable(ConfigurationKeys.InternalDebuggerFlowRecorderCaptureValues, "all");

        var (capturePath, _) = await RunNativeRecorderSample("exception");

        var capture = ReadFlowCapture(capturePath, requireEvents: true);
        capture.Exceptions.Should().NotBeEmpty();
        capture.Strings.Should().Contain("Payment declined in POC scenario.");
        capture.Values.Should().NotBeEmpty("all eligible flow recorder methods should include value-capable instrumentation");
        var methodNamesById = capture.Methods.ToDictionary(method => method.MethodMetadataIndex, method => method.DisplayName);
        var valueMethodNames = capture.Values
                                      .Select(value => capture.Events.FirstOrDefault(flowEvent => flowEvent.FlowId == value.FlowId && flowEvent.FrameId == value.FrameId && flowEvent.Kind == FlowEventKind.Enter))
                                      .Where(flowEvent => flowEvent.MethodMetadataIndex != 0)
                                      .Select(flowEvent => methodNamesById[flowEvent.MethodMetadataIndex]);
        valueMethodNames
               .Should()
               .Contain(methodName => methodName.Contains("ChargePayment", StringComparison.Ordinal))
               .And.Contain(methodName => methodName.Contains("ValidateCart", StringComparison.Ordinal));
        capture.Values.Select(value => capture.Types[value.TypeId])
               .Should()
               .Contain("System.Boolean")
               .And.NotContain("Datadog.Trace.Debugger.LiveDebuggerPoc.FlowRecorderState")
               .And.NotContain("System.Exception")
               .And.NotContain(typeName => typeName.Contains("TaskAwaiter", StringComparison.Ordinal));
    }

    [SkippableFact]
    [Trait("RunOnWindows", "True")]
    public async Task NativeInstrumentAllCanCaptureAsyncStateMachineValues()
    {
        SetEnvironmentVariable(ConfigurationKeys.InternalDebuggerFlowRecorderCaptureValues, "all");
        SetEnvironmentVariable(ConfigurationKeys.InternalDebuggerFlowRecorderCaptureValueMethods, "AsyncValueAsync,AsyncLeafAsync");

        var (capturePath, _) = await RunNativeRecorderSample("async");

        var capture = ReadFlowCapture(capturePath, requireEvents: true);
        var methodNamesById = capture.Methods.ToDictionary(method => method.MethodMetadataIndex, method => method.DisplayName);
        var valuesByMethod = capture.Values
                                    .Select(value => new
                                    {
                                        Value = value,
                                        Name = capture.Strings[value.NameId],
                                        Type = capture.Types[value.TypeId],
                                        MethodName = GetValueMethodName(capture.Events, methodNamesById, value)
                                    })
                                    .Where(item => item.MethodName.Contains("AsyncValueAsync", StringComparison.Ordinal) ||
                                                   item.MethodName.Contains("AsyncLeafAsync", StringComparison.Ordinal))
                                    .ToArray();

        valuesByMethod.Should().Contain(item => item.Name == "value" &&
                                                item.Value.Phase == (byte)FlowCapturePhase.Entry &&
                                                item.Value.Kind == (byte)FlowValueKind.Argument);
        valuesByMethod.Should().Contain(item => item.Name == "@return" &&
                                                item.Value.Phase == (byte)FlowCapturePhase.Exit &&
                                                item.Value.Kind == (byte)FlowValueKind.Return);
        valuesByMethod.Select(item => item.Type)
                      .Should()
                      .NotContain(typeName => typeName.Contains("AsyncTaskMethodBuilder", StringComparison.Ordinal))
                      .And.NotContain(typeName => typeName.Contains("TaskAwaiter", StringComparison.Ordinal))
                      .And.NotContain(typeName => typeName.Contains("ValueTaskAwaiter", StringComparison.Ordinal))
                      .And.NotContain(typeName => typeName.Contains("Enumerator", StringComparison.Ordinal));
    }

    private static FlowCapture ReadFlowCapture(string path, bool requireEvents)
    {
        using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);

        reader.ReadInt32().Should().Be(FlowRecorderMagic);
        var version = reader.ReadInt32();
        version.Should().BeLessThanOrEqualTo(FlowRecorderVersion);
        var count = reader.ReadInt32();
        count.Should().BeGreaterThanOrEqualTo(0);
        if (requireEvents)
        {
            count.Should().BeGreaterThan(0);
        }

        var events = new FlowEvent[count];
        for (var i = 0; i < count; i++)
        {
            var kind = (FlowEventKind)reader.ReadByte();
            var timestamp = reader.ReadInt64();
            var methodMetadataIndex = reader.ReadInt32();
            var flowId = reader.ReadUInt64();
            var frameId = reader.ReadUInt64();
            var parentFrameId = reader.ReadUInt64();
            var depth = reader.ReadInt32();
            var threadId = reader.ReadInt32();
            if (version < 6)
            {
                _ = reader.ReadUInt64();
                _ = reader.ReadUInt64();
                _ = reader.ReadUInt64();
                _ = reader.ReadUInt64();
            }

            events[i] = new FlowEvent(kind, timestamp, methodMetadataIndex, flowId, frameId, parentFrameId, depth, threadId, reader.ReadInt64(), version >= 5 ? reader.ReadUInt64() : 0);
        }

        var methodCount = reader.ReadInt32();
        methodCount.Should().BeGreaterThanOrEqualTo(0);
        var methods = new FlowMethodMetadata[methodCount];
        for (var i = 0; i < methodCount; i++)
        {
            methods[i] = new FlowMethodMetadata(reader.ReadInt32(), ReadString(reader, version));
        }

        var strings = Array.Empty<string>();
        var types = Array.Empty<string>();
        var exceptions = Array.Empty<FlowExceptionDetails>();
        var values = Array.Empty<FlowCapturedValue>();
        if (version >= 4)
        {
            strings = ReadStringArray(reader, version);
            types = ReadStringArray(reader, version);
            exceptions = new FlowExceptionDetails[reader.ReadInt32()];
            for (var i = 0; i < exceptions.Length; i++)
            {
                exceptions[i] = new FlowExceptionDetails(reader.ReadUInt64(), reader.ReadUInt64(), reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32());
            }

            values = new FlowCapturedValue[reader.ReadInt32()];
            for (var i = 0; i < values.Length; i++)
            {
                values[i] = new FlowCapturedValue(reader.ReadUInt64(), reader.ReadUInt64(), reader.ReadByte(), reader.ReadByte(), reader.ReadInt32(), reader.ReadInt32(), reader.ReadByte(), reader.ReadByte(), reader.ReadInt64(), reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32());
            }
        }

        if (version >= 5)
        {
            var operationCount = reader.ReadInt32();
            operationCount.Should().BeGreaterThanOrEqualTo(0);
            var operations = new FlowOperationMetadata[operationCount];
            for (var i = 0; i < operationCount; i++)
            {
                operations[i] = new FlowOperationMetadata(
                    reader.ReadUInt64(),
                    reader.ReadInt64(),
                    ReadString(reader, version),
                    ReadString(reader, version),
                    reader.ReadInt64(),
                    reader.ReadUInt64(),
                    reader.ReadUInt64(),
                    reader.ReadUInt64(),
                    reader.ReadUInt64());
            }

            return new FlowCapture(events, methods, strings, types, exceptions, values, operations);
        }

        stream.Position.Should().Be(stream.Length);
        return new FlowCapture(events, methods, strings, types, exceptions, values, Array.Empty<FlowOperationMetadata>());
    }

    private static string[] ReadStringArray(BinaryReader reader, int version)
    {
        var count = reader.ReadInt32();
        count.Should().BeGreaterThanOrEqualTo(0);
        var values = new string[count];
        for (var i = 0; i < count; i++)
        {
            values[i] = ReadString(reader, version);
        }

        return values;
    }

    private static string ReadString(BinaryReader reader, int version)
    {
        if (version < 6)
        {
            return reader.ReadString();
        }

        var byteCount = reader.ReadInt32();
        byteCount.Should().BeGreaterThanOrEqualTo(0);
        var bytes = reader.ReadBytes(byteCount);
        bytes.Should().HaveCount(byteCount);
        return Encoding.UTF8.GetString(bytes, 0, bytes.Length);
    }

    private static bool IsKnownKind(FlowEventKind kind)
    {
        return kind == FlowEventKind.Enter ||
               kind == FlowEventKind.Exit ||
               kind == FlowEventKind.Exception ||
               kind == FlowEventKind.AsyncEdge ||
               kind == FlowEventKind.Truncated ||
               kind == FlowEventKind.Suppressed;
    }

    private static void AssertEventsHaveKnownKinds(FlowEvent[] events)
    {
        if (events.Length > 0)
        {
            events.Should().OnlyContain(flowEvent => IsKnownKind(flowEvent.Kind));
        }
    }

    private static bool IsManualRecorderMethodId(int methodMetadataIndex)
    {
        return methodMetadataIndex >= 100 && methodMetadataIndex <= 106;
    }

    private static bool IsBalancedFrame(FlowEvent[] frame)
    {
        return frame.Count(flowEvent => flowEvent.Kind == FlowEventKind.Enter) == 1 &&
               frame.Count(flowEvent => flowEvent.Kind == FlowEventKind.Exit) == 1 &&
               frame.First(flowEvent => flowEvent.Kind == FlowEventKind.Enter).Timestamp <=
               frame.First(flowEvent => flowEvent.Kind == FlowEventKind.Exit).Timestamp;
    }

    private static void AssertParentFramesContainChildren(FlowEvent[] events)
    {
        var frameBounds = events.Where(flowEvent => flowEvent.Kind != FlowEventKind.AsyncEdge)
                                .GroupBy(flowEvent => (flowEvent.FlowId, flowEvent.FrameId))
                                .Select(group => new
                                {
                                    group.Key.FlowId,
                                    group.Key.FrameId,
                                    Enter = group.Single(flowEvent => flowEvent.Kind == FlowEventKind.Enter),
                                    Exit = group.Single(flowEvent => flowEvent.Kind == FlowEventKind.Exit)
                                })
                                .ToDictionary(frame => (frame.FlowId, frame.FrameId));

        foreach (var frame in frameBounds.Values)
        {
            if (frame.Enter.ParentFrameId == 0)
            {
                continue;
            }

            var parent = frameBounds[(frame.FlowId, frame.Enter.ParentFrameId)];
            parent.Enter.Timestamp.Should().BeLessThanOrEqualTo(frame.Enter.Timestamp);
            parent.Exit.Timestamp.Should().BeGreaterThanOrEqualTo(frame.Exit.Timestamp);
        }
    }

    private static void AssertAsyncMoveNextStepsShareOperationFlow(FlowEvent[] events, FlowMethodMetadata[] methods)
    {
        var methodsById = methods.ToDictionary(method => method.MethodMetadataIndex, method => method.DisplayName);
        events.Where(flowEvent => flowEvent.Kind == FlowEventKind.Enter)
              .Select(flowEvent => new { Event = flowEvent, LogicalName = TryGetAsyncLogicalName(methodsById, flowEvent.MethodMetadataIndex) })
              .Where(item => item.LogicalName is not null)
              .GroupBy(item => (item.LogicalName, item.Event.FlowId))
              .Should()
              .Contain(group => group.Count() > 1, "an async state-machine invocation should keep the same flow id across MoveNext resumptions");
    }

    private static void AssertAsyncEdgesReferenceRecordedOperations(FlowEvent[] events)
    {
        var operationFlowIds = events.Where(flowEvent => flowEvent.Kind == FlowEventKind.Enter)
                                     .Select(flowEvent => flowEvent.FlowId)
                                     .ToHashSet();
        var edges = events.Where(flowEvent => flowEvent.Kind == FlowEventKind.AsyncEdge).ToArray();
        edges.Should().NotBeEmpty("async kickoff instrumentation should record causality edges");
        edges.Should().OnlyContain(edge => operationFlowIds.Contains(edge.FlowId), "edge parent ids should reference recorded async operations");
        edges.Should().OnlyContain(edge => operationFlowIds.Contains(edge.FrameId), "edge child ids should reference recorded async operations");
    }

    private static string TryGetAsyncLogicalName(IReadOnlyDictionary<int, string> methodNames, int methodMetadataIndex)
    {
        if (!methodNames.TryGetValue(methodMetadataIndex, out var displayName))
        {
            return null;
        }

        const string stateMachinePrefix = "+<";
        const string standaloneStateMachinePrefix = "<";
        const string stateMachineSuffix = ">d__";
        const string moveNextSuffix = ".MoveNext";

        var prefixIndex = displayName.IndexOf(stateMachinePrefix, StringComparison.Ordinal);
        if (prefixIndex < 0 || !displayName.EndsWith(moveNextSuffix, StringComparison.Ordinal))
        {
            if (!displayName.StartsWith(standaloneStateMachinePrefix, StringComparison.Ordinal) ||
                !displayName.EndsWith(moveNextSuffix, StringComparison.Ordinal))
            {
                return null;
            }

            var standaloneSuffixIndex = displayName.IndexOf(stateMachineSuffix, standaloneStateMachinePrefix.Length, StringComparison.Ordinal);
            return standaloneSuffixIndex < 0
                       ? null
                       : displayName.Substring(standaloneStateMachinePrefix.Length, standaloneSuffixIndex - standaloneStateMachinePrefix.Length);
        }

        var methodNameStart = prefixIndex + stateMachinePrefix.Length;
        var suffixIndex = displayName.IndexOf(stateMachineSuffix, methodNameStart, StringComparison.Ordinal);
        if (suffixIndex < 0)
        {
            return null;
        }

        var declaringType = displayName.Substring(0, prefixIndex);
        var methodName = displayName.Substring(methodNameStart, suffixIndex - methodNameStart);
        return declaringType + "." + methodName;
    }

    private static string ReadNativeLogText(string logDirectory)
    {
        Directory.Exists(logDirectory).Should().BeTrue();
        var nativeLogs = Directory.GetFiles(logDirectory, "dotnet-tracer-native-*", SearchOption.AllDirectories);
        nativeLogs.Should().NotBeEmpty();
        return string.Join(Environment.NewLine, nativeLogs.Select(File.ReadAllText));
    }

    private static string RunViewer(string capturePath)
    {
        var solutionDirectory = EnvironmentTools.GetSolutionDirectory();
        var viewerDll = Path.Combine(
            solutionDirectory,
            "artifacts",
            "bin",
            "Samples.LiveDebuggerPoc.Viewer",
            "release_net10.0",
            "Samples.LiveDebuggerPoc.Viewer.dll");

        File.Exists(viewerDll).Should().BeTrue();
        using var process = Process.Start(new ProcessStartInfo("dotnet", "\"" + viewerDll + "\" \"" + capturePath + "\"")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        });

        process.Should().NotBeNull();
        var standardOutput = process!.StandardOutput.ReadToEnd();
        var standardError = process.StandardError.ReadToEnd();
        process.WaitForExit(30_000).Should().BeTrue();
        process.ExitCode.Should().Be(0, standardError);
        return standardOutput;
    }

    private static string GetValueMethodName(FlowEvent[] events, IReadOnlyDictionary<int, string> methodNamesById, FlowCapturedValue value)
    {
        var enter = events.First(flowEvent => flowEvent.FlowId == value.FlowId &&
                                             flowEvent.FrameId == value.FrameId &&
                                             flowEvent.Kind == FlowEventKind.Enter);
        return methodNamesById[enter.MethodMetadataIndex];
    }

    private async Task<(string CapturePath, string LogDirectory)> RunNativeRecorderSample(string scenario = "checkout", string rewriteMode = null)
    {
        if (!EnvironmentTools.IsWindows())
        {
            throw new SkipException("The live debugger POC flow recorder is currently Windows-gated.");
        }

        var capturePath = Path.Combine(
            Path.GetTempPath(),
            "datadog-live-debugger-poc-tests",
            "flow-events-" + Guid.NewGuid().ToString("N") + ".dflp");
        Directory.CreateDirectory(Path.GetDirectoryName(capturePath)!);
        var logDirectory = Path.Combine(
            Path.GetTempPath(),
            "datadog-live-debugger-poc-tests",
            "logs-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(logDirectory);

        SetEnvironmentVariable(ConfigurationKeys.Debugger.DynamicInstrumentationEnabled, "true");
        SetEnvironmentVariable("DD_INTERNAL_DEBUGGER_INSTRUMENT_ALL", "true");
        SetEnvironmentVariable(ConfigurationKeys.InternalDebuggerFlowRecorderEnabled, "true");
        SetEnvironmentVariable(ConfigurationKeys.InternalDebuggerFlowRecorderOutputPath, capturePath);
        SetEnvironmentVariable(ConfigurationKeys.LogDirectory, logDirectory);
        if (rewriteMode is not null)
        {
            SetEnvironmentVariable(ConfigurationKeys.InternalDebuggerFlowRecorderRewriteMode, rewriteMode);
        }

        using var agent = EnvironmentHelper.GetMockAgent();
        using var result = await RunSampleAndWaitForExit(
            agent,
            "--scenario " + scenario + " --recording native --output \"" + capturePath + "\"",
            framework: "net10.0");

        result.StandardOutput.Should().Contain("Dropped events: 0");
        File.Exists(capturePath).Should().BeTrue();
        return (capturePath, logDirectory);
    }

    private readonly record struct FlowEvent(
        FlowEventKind Kind,
        long Timestamp,
        int MethodMetadataIndex,
        ulong FlowId,
        ulong FrameId,
        ulong ParentFrameId,
        int Depth,
        int ThreadId,
        long ExceptionTypeId,
        ulong OperationId);

    private readonly record struct FlowMethodMetadata(int MethodMetadataIndex, string DisplayName);

    private readonly record struct FlowExceptionDetails(ulong FlowId, ulong FrameId, int TypeId, int MessageId, int StackId, int HResult);

    private readonly record struct FlowCapturedValue(
        ulong FlowId,
        ulong FrameId,
        byte Phase,
        byte Kind,
        int NameId,
        int TypeId,
        byte Tag,
        byte NotCapturedReason,
        long NumberValue,
        int StringId,
        int ItemCount,
        int CapturedItemCount);

    private readonly record struct FlowOperationMetadata(
        ulong OperationId,
        long Generation,
        string TriggerReason,
        string Root,
        long StartTimestamp,
        ulong TraceIdUpper,
        ulong TraceIdLower,
        ulong RootSpanId,
        ulong ActiveSpanId);

    private readonly record struct FlowCapture(
        FlowEvent[] Events,
        FlowMethodMetadata[] Methods,
        string[] Strings,
        string[] Types,
        FlowExceptionDetails[] Exceptions,
        FlowCapturedValue[] Values,
        FlowOperationMetadata[] Operations);
}

#endif
