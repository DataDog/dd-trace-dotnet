// <copyright file="FakeTrackedStackFrameNode.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Datadog.Trace.Debugger.Expressions;
using Datadog.Trace.Debugger.Instrumentation.Collections;
using Datadog.Trace.Debugger.Snapshots;
using Fnv1aHash = Datadog.Trace.VendoredMicrosoftCode.System.Reflection.Internal.Hash;

#nullable enable
namespace Datadog.Trace.Debugger.ExceptionAutoInstrumentation
{
    internal sealed class FakeTrackedStackFrameNode(TrackedStackFrameNode? parent, MethodBase method)
        : TrackedStackFrameNode(parent, method, isInvalidPath: false)
    {
        protected override int ComputeEnterSequenceHash()
        {
            return Parent!.EnterSequenceHash;
        }

        protected override int ComputeLeaveSequenceHash()
        {
            lock (this)
            {
                ClearNonRelevantChildNodes();

                if (ActiveChildNodes?.Any() == true)
                {
                    var firstChild = ActiveChildNodes.First();
                    return firstChild.LeaveSequenceHash;
                }

                return Fnv1aHash.Combine(Method.MetadataToken, Fnv1aHash.FnvOffsetBias);
            }
        }

        public override string ToString()
        {
            return $"{nameof(FakeTrackedStackFrameNode)}(Child Count = {ActiveChildNodes?.Count})";
        }
    }
}
