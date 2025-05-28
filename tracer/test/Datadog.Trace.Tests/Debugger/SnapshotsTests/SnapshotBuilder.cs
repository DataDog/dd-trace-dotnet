// <copyright file="FlexibleSnapshotBuilder.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using Datadog.Trace.Debugger;
using Datadog.Trace.Debugger.Expressions;
using Datadog.Trace.Debugger.Snapshots;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.Tests.Debugger.SnapshotsTests;

/// <summary>
/// Flexible snapshot builder that allows testing different snapshot construction patterns
/// and configurable capture limits, unlike the rigid SnapshotHelper.
/// </summary>
internal class SnapshotBuilder
{
    private readonly DebuggerSnapshotCreator _snapshotCreator;
    private readonly List<CaptureAction> _entryActions = new();
    private readonly List<CaptureAction> _returnActions = new();
    private bool _entryStarted = false;
    private bool _returnStarted = false;
    private bool _finalized = false;

    public SnapshotBuilder(CaptureLimitInfo? limitInfo = null, bool isFullSnapshot = true, ProbeLocation location = ProbeLocation.Method)
    {
        var actualLimitInfo = limitInfo ?? new CaptureLimitInfo(
            MaxReferenceDepth: DebuggerSettings.DefaultMaxDepthToSerialize,
            MaxCollectionSize: DebuggerSettings.DefaultMaxNumberOfItemsInCollectionToCopy,
            MaxFieldCount: DebuggerSettings.DefaultMaxNumberOfFieldsToCopy,
            MaxLength: DebuggerSettings.DefaultMaxStringLength);

        _snapshotCreator = new DebuggerSnapshotCreator(
            isFullSnapshot: isFullSnapshot,
            location: location,
            hasCondition: false,
            tags: new[] { "Tag1", "Tag2" },
            limitInfo: actualLimitInfo);
    }

    /// <summary>
    /// Configure custom capture limits for testing specific scenarios
    /// </summary>
    public static CaptureLimitInfo CreateLimitInfo(
        int? maxDepth = null,
        int? maxCollectionSize = null,
        int? maxFieldCount = null,
        int? maxStringLength = null)
    {
        return new CaptureLimitInfo(
            MaxReferenceDepth: maxDepth ?? DebuggerSettings.DefaultMaxDepthToSerialize,
            MaxCollectionSize: maxCollectionSize ?? DebuggerSettings.DefaultMaxNumberOfItemsInCollectionToCopy,
            MaxFieldCount: maxFieldCount ?? DebuggerSettings.DefaultMaxNumberOfFieldsToCopy,
            MaxLength: maxStringLength ?? DebuggerSettings.DefaultMaxStringLength);
    }

    /// <summary>
    /// Configure limits that will trigger timeout (very low serialization time)
    /// </summary>
    public static CaptureLimitInfo CreateTimeoutLimits()
    {
        // Note: Timeout is controlled by DebuggerSnapshotSerializer._maximumSerializationTime
        // We can't directly control it here, but we can create conditions that are likely to timeout
        return CreateLimitInfo(maxDepth: 1000, maxCollectionSize: 10000, maxFieldCount: 1000);
    }

    /// <summary>
    /// Add an instance capture to entry section
    /// </summary>
    public SnapshotBuilder AddEntryInstance(object instance)
    {
        if (instance != null)
        {
            _entryActions.Add(new CaptureAction(CaptureType.Instance, instance, instance.GetType(), "this"));
        }
        return this;
    }

    /// <summary>
    /// Add an argument capture to entry section
    /// </summary>
    public SnapshotBuilder AddEntryArgument(object value, string name, Type type = null)
    {
        _entryActions.Add(new CaptureAction(CaptureType.Argument, value, type ?? value?.GetType() ?? typeof(object), name));
        return this;
    }

    /// <summary>
    /// Add a local capture to entry section (this should normally fail validation)
    /// </summary>
    public SnapshotBuilder AddEntryLocal(object value, string name, Type type = null)
    {
        _entryActions.Add(new CaptureAction(CaptureType.Local, value, type ?? value?.GetType() ?? typeof(object), name));
        return this;
    }

    /// <summary>
    /// Add an instance capture to return section
    /// </summary>
    public SnapshotBuilder AddReturnInstance(object instance)
    {
        if (instance != null)
        {
            _returnActions.Add(new CaptureAction(CaptureType.Instance, instance, instance.GetType(), "this"));
        }
        return this;
    }

    /// <summary>
    /// Add an argument capture to return section
    /// </summary>
    public SnapshotBuilder AddReturnArgument(object value, string name, Type type = null)
    {
        _returnActions.Add(new CaptureAction(CaptureType.Argument, value, type ?? value?.GetType() ?? typeof(object), name));
        return this;
    }

    /// <summary>
    /// Add a local capture to return section
    /// </summary>
    public SnapshotBuilder AddReturnLocal(object value, string name, Type type = null)
    {
        _returnActions.Add(new CaptureAction(CaptureType.Local, value, type ?? value?.GetType() ?? typeof(object), name));
        return this;
    }

    /// <summary>
    /// Build the snapshot with the configured actions in the specified order
    /// </summary>
    public string Build(bool prettify = true)
    {
        if (_finalized)
        {
            throw new InvalidOperationException("Snapshot has already been built");
        }

        try
        {
            // Execute entry actions
            if (_entryActions.Count > 0)
            {
                _snapshotCreator.StartEntry();
                _entryStarted = true;

                foreach (var action in _entryActions)
                {
                    ExecuteAction(action);
                }

                _snapshotCreator.EndEntry(hasArgumentsOrLocals: _entryActions.Count > 0);
            }

            // Execute return actions
            if (_returnActions.Count > 0)
            {
                _snapshotCreator.StartReturn();
                _returnStarted = true;

                foreach (var action in _returnActions)
                {
                    ExecuteAction(action);
                }

                _snapshotCreator.EndReturn(hasArgumentsOrLocals: _returnActions.Count > 0);
            }

            // Finalize
            _snapshotCreator.FinalizeSnapshot("TestMethod", "TestClass", "test");
            _finalized = true;

            var snapshot = _snapshotCreator.GetSnapshotJsonAndDispose();
            return prettify ? JsonPrettify(snapshot) : snapshot;
        }
        catch (Exception)
        {
            // Ensure cleanup even if building fails
            if (!_finalized)
            {
                try
                {
                    _snapshotCreator.GetSnapshotJsonAndDispose();
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
            throw;
        }
    }

    /// <summary>
    /// Build a snapshot that deliberately violates normal patterns for testing error handling
    /// </summary>
    public string BuildWithViolations(SnapshotViolation violation, bool prettify = true)
    {
        if (_finalized)
        {
            throw new InvalidOperationException("Snapshot has already been built");
        }

        try
        {
            switch (violation)
            {
                case SnapshotViolation.LocalsInEntry:
                    // Add locals to entry (should be arguments only)
                    _snapshotCreator.StartEntry();
                    _snapshotCreator.CaptureLocal("invalid", "local0", typeof(string));
                    _snapshotCreator.EndEntry(hasArgumentsOrLocals: true);
                    break;

                case SnapshotViolation.ArgumentsBeforeLocalsInReturn:
                    // Add arguments before locals in return (wrong order)
                    _snapshotCreator.StartReturn();
                    _snapshotCreator.CaptureArgument("arg", "arg0", typeof(string));
                    _snapshotCreator.CaptureLocal("local", "local0", typeof(string));
                    _snapshotCreator.EndReturn(hasArgumentsOrLocals: true);
                    break;

                case SnapshotViolation.MissingInstanceForInstanceMethod:
                    // Simulate instance method without capturing instance
                    _snapshotCreator.StartEntry();
                    _snapshotCreator.CaptureArgument("arg", "arg0", typeof(string));
                    _snapshotCreator.EndEntry(hasArgumentsOrLocals: true);
                    break;

                case SnapshotViolation.DoubleEntry:
                    // Try to start entry twice
                    _snapshotCreator.StartEntry();
                    _snapshotCreator.StartEntry(); // This should cause issues
                    break;

                case SnapshotViolation.ReturnWithoutEntry:
                    // Start return without entry
                    _snapshotCreator.StartReturn();
                    _snapshotCreator.EndReturn(hasArgumentsOrLocals: false);
                    break;

                case SnapshotViolation.IncompleteJson:
                    // Don't finalize properly to create incomplete JSON
                    _snapshotCreator.StartEntry();
                    _snapshotCreator.EndEntry(hasArgumentsOrLocals: false);
                    // Don't call FinalizeSnapshot
                    return _snapshotCreator.GetSnapshotJsonAndDispose();

                default:
                    throw new ArgumentException($"Unknown violation type: {violation}");
            }

            _snapshotCreator.FinalizeSnapshot("TestMethod", "TestClass", "test");
            _finalized = true;

            var snapshot = _snapshotCreator.GetSnapshotJsonAndDispose();
            return prettify ? JsonPrettify(snapshot) : snapshot;
        }
        catch (Exception)
        {
            // Ensure cleanup even if building fails
            if (!_finalized)
            {
                try
                {
                    _snapshotCreator.GetSnapshotJsonAndDispose();
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
            throw;
        }
    }

    private void ExecuteAction(CaptureAction action)
    {
        switch (action.Type)
        {
            case CaptureType.Instance:
                _snapshotCreator.CaptureInstance(action.Value, action.ValueType);
                break;
            case CaptureType.Argument:
                _snapshotCreator.CaptureArgument(action.Value, action.Name, action.ValueType);
                break;
            case CaptureType.Local:
                _snapshotCreator.CaptureLocal(action.Value, action.Name, action.ValueType);
                break;
            default:
                throw new ArgumentException($"Unknown capture type: {action.Type}");
        }
    }

    /// <summary>
    /// Convenience method that mimics the old SnapshotHelper.GenerateSnapshot behavior
    /// for easy migration of existing tests.
    /// </summary>
    public static string GenerateSnapshot(object instance, bool prettify = true)
    {
        return new SnapshotBuilder()
            .AddReturnLocal(instance, "local0")
            .Build(prettify);
    }

    /// <summary>
    /// Create a deeply nested object for testing depth limits
    /// </summary>
    public static NestedObject CreateDeeplyNestedObject(int depth)
    {
        if (depth <= 0)
        {
            return new NestedObject { Value = "leaf" };
        }

        return new NestedObject
        {
            Value = $"depth-{depth}",
            Child = CreateDeeplyNestedObject(depth - 1)
        };
    }

    private static string JsonPrettify(string json)
    {
        using var stringReader = new StringReader(json);
        using var stringWriter = new StringWriter();
        var jsonReader = new JsonTextReader(stringReader);
        var jsonWriter = new JsonTextWriter(stringWriter) { Formatting = Formatting.Indented };
        jsonWriter.WriteToken(jsonReader);
        return stringWriter.ToString();
    }

    private record CaptureAction(CaptureType Type, object Value, Type ValueType, string Name);

    private enum CaptureType
    {
        Instance,
        Argument,
        Local
    }
}

/// <summary>
/// Types of snapshot construction violations for testing error handling
/// </summary>
public enum SnapshotViolation
{
    /// <summary>
    /// Adding locals to entry section (should only have arguments)
    /// </summary>
    LocalsInEntry,

    /// <summary>
    /// Adding arguments before locals in return section (wrong order)
    /// </summary>
    ArgumentsBeforeLocalsInReturn,

    /// <summary>
    /// Missing instance capture for instance method
    /// </summary>
    MissingInstanceForInstanceMethod,

    /// <summary>
    /// Starting entry section twice
    /// </summary>
    DoubleEntry,

    /// <summary>
    /// Starting return without entry
    /// </summary>
    ReturnWithoutEntry,

    /// <summary>
    /// Creating incomplete JSON by not finalizing properly
    /// </summary>
    IncompleteJson
} 
