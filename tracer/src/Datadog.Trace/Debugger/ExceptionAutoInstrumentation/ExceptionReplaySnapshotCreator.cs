// <copyright file="ExceptionReplaySnapshotCreator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.Debugger.Expressions;
using Datadog.Trace.Debugger.Snapshots;
using ProbeLocation = Datadog.Trace.Debugger.Expressions.ProbeLocation;

#nullable enable
namespace Datadog.Trace.Debugger.ExceptionAutoInstrumentation
{
    internal class ExceptionReplaySnapshotCreator : DebuggerSnapshotCreator
    {
        private readonly string _exceptionHash;
        private readonly string _exceptionCaptureId;
        private readonly int _frameIndex;

        public ExceptionReplaySnapshotCreator(bool isFullSnapshot, ProbeLocation location, bool hasCondition, string[] tags, CaptureLimitInfo limitInfo, string exceptionHash, string exceptionCaptureId, int frameIndex)
            : base(isFullSnapshot, location, hasCondition, tags, limitInfo)
        {
            _exceptionHash = exceptionHash;
            _exceptionCaptureId = exceptionCaptureId;
            _frameIndex = frameIndex;
        }

        public ExceptionReplaySnapshotCreator(bool isFullSnapshot, ProbeLocation location, bool hasCondition, string[] tags, MethodScopeMembers methodScopeMembers, CaptureLimitInfo limitInfo, string exceptionHash, string exceptionCaptureId, int frameIndex)
            : base(isFullSnapshot, location, hasCondition, tags, methodScopeMembers, limitInfo)
        {
            _exceptionHash = exceptionHash;
            _exceptionCaptureId = exceptionCaptureId;
            _frameIndex = frameIndex;
        }

        internal override DebuggerSnapshotCreator EndSnapshot()
        {
            JsonWriter.WritePropertyName("exception_hash");
            JsonWriter.WriteValue(_exceptionHash);

            JsonWriter.WritePropertyName("exception_capture_id");
            JsonWriter.WriteValue(_exceptionCaptureId);

            JsonWriter.WritePropertyName("frame_index");
            JsonWriter.WriteValue(_frameIndex);

            return base.EndSnapshot();
        }
    }
}
