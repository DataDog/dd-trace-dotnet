// <copyright file="SnapshotHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using Datadog.Trace.Debugger;
using Datadog.Trace.Debugger.Expressions;
using Datadog.Trace.Debugger.Snapshots;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.Tests.Debugger;

internal static class SnapshotHelper
{
    internal static string GenerateSnapshot(object instance, bool prettify = true)
    {
        return GenerateSnapshot(null, new object[] { }, new object[] { instance }, prettify);
    }

    /// <summary>
    /// Generate a debugger snapshot by simulating the same flow of method calls as our instrumentation produces for a method probe.
    /// </summary>
    private static string GenerateSnapshot(object instance, object[] args, object[] locals, bool prettify)
    {
        var maxInfo = new CaptureLimitInfo(
            MaxReferenceDepth: DebuggerSettings.DefaultMaxDepthToSerialize,
            MaxCollectionSize: DebuggerSettings.DefaultMaxNumberOfItemsInCollectionToCopy,
            MaxFieldCount: DebuggerSettings.DefaultMaxNumberOfFieldsToCopy,
            MaxLength: DebuggerSettings.DefaultMaxStringLength);

        var snapshotCreator = new DebuggerSnapshotCreator(isFullSnapshot: true, ProbeLocation.Method, hasCondition: false, new[] { "Tag1", "Tag2" }, maxInfo);
        {
            // method entry
            snapshotCreator.StartEntry();
            if (instance != null)
            {
                snapshotCreator.CaptureInstance(instance, instance.GetType());
            }

            for (var i = 0; i < args.Length; i++)
            {
                snapshotCreator.CaptureArgument(args[i], "arg" + i, args[i].GetType());
            }

            snapshotCreator.EndEntry(hasArgumentsOrLocals: args.Length > 0);
        }

        {
            // method exit
            snapshotCreator.StartReturn();
            for (var i = 0; i < locals.Length; i++)
            {
                snapshotCreator.CaptureLocal(locals[i], "local" + i, locals[i]?.GetType() ?? typeof(object));
            }

            for (var i = 0; i < args.Length; i++)
            {
                snapshotCreator.CaptureArgument(args[i], "arg" + i, args[i].GetType());
            }

            if (instance != null)
            {
                snapshotCreator.CaptureInstance(instance, instance.GetType());
            }

            snapshotCreator.EndReturn(hasArgumentsOrLocals: args.Length + locals.Length > 0);
        }

        snapshotCreator.FinalizeSnapshot("Foo", "Bar", "foo");

        var snapshot = snapshotCreator.GetSnapshotJson();
        return prettify ? JsonPrettify(snapshot) : snapshot;
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
}
