// <copyright file="TaskSnapshotSerializerFieldsAndPropsSelector.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Datadog.Trace.Debugger.Snapshots
{
    internal class TaskSnapshotSerializerFieldsAndPropsSelector : SnapshotSerializerFieldsAndPropsSelector
    {
        internal override bool IsApplicable(Type type)
        {
            return type.Name == "Task`1" && type.Namespace == "System.Threading.Tasks";
        }

        internal override IEnumerable<MemberInfo> GetFieldsAndProps(
            Type type,
            object source,
            int maximumDepthOfHierarchyToCopy,
            int maximumNumberOfFieldsToCopy,
            CancellationTokenSource cts)
        {
            yield return type.GetProperty("Id");
            var statusProp = type.GetProperty("Status");

            if (DebuggerSnapshotSerializer.TryGetValue(statusProp, source, out var status, out var _))
            {
                yield return statusProp;
                if (TaskStatus.RanToCompletion.Equals(status))
                {
                    yield return type.GetProperty("Result");
                }
                else if (TaskStatus.Faulted.Equals(status))
                {
                    yield return type.GetProperty("Exception");
                }
            }
        }
    }
}
