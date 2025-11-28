// <copyright file="ExceptionReplaySnapshotCreator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Debugger.Expressions;
using Datadog.Trace.Debugger.Models;
using Datadog.Trace.Debugger.Snapshots;
using ProbeLocation = Datadog.Trace.Debugger.Expressions.ProbeLocation;

#nullable enable
namespace Datadog.Trace.Debugger.ExceptionAutoInstrumentation
{
    internal sealed class ExceptionReplaySnapshotCreator : DebuggerSnapshotCreator
    {
        public ExceptionReplaySnapshotCreator(bool isFullSnapshot, ProbeLocation location, bool hasCondition, string[] tags, CaptureLimitInfo limitInfo, bool withProcessTags)
            : base(isFullSnapshot, location, hasCondition, tags, limitInfo, withProcessTags)
        {
        }

        public ExceptionReplaySnapshotCreator(bool isFullSnapshot, ProbeLocation location, bool hasCondition, string[] tags, MethodScopeMembers methodScopeMembers, CaptureLimitInfo limitInfo, bool withProcessTags)
            : base(isFullSnapshot, location, hasCondition, tags, methodScopeMembers, limitInfo, withProcessTags)
        {
        }

        internal static string ExceptionHash { get; } = Guid.NewGuid().ToString();

        internal static string ExceptionCaptureId { get; } = Guid.NewGuid().ToString();

        internal static string FrameIndex { get; } = Guid.NewGuid().ToString();

        internal override string DebuggerProduct => DebuggerTags.DebuggerProduct.ER;

        internal override DebuggerSnapshotCreator EndSnapshot()
        {
            JsonWriter.WritePropertyName("exceptionHash");
            JsonWriter.WriteValue(ExceptionHash);

            JsonWriter.WritePropertyName("exceptionId");
            JsonWriter.WriteValue(ExceptionCaptureId);

            JsonWriter.WritePropertyName("frameIndex");
            JsonWriter.WriteValue(FrameIndex);

            return base.EndSnapshot();
        }
    }
}
