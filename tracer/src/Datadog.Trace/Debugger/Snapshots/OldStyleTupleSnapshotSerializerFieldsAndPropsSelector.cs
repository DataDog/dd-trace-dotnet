// <copyright file="OldStyleTupleSnapshotSerializerFieldsAndPropsSelector.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;

namespace Datadog.Trace.Debugger.Snapshots;

internal class OldStyleTupleSnapshotSerializerFieldsAndPropsSelector : SnapshotSerializerFieldsAndPropsSelector
{
    internal override bool IsApplicable(Type type)
    {
        return type.Name.StartsWith("Tuple`") && type.Namespace == "System";
    }

    internal override IEnumerable<MemberInfo> GetFieldsAndProps(
        Type type,
        object source,
        int maximumDepthOfHierarchyToCopy,
        int maximumNumberOfFieldsToCopy,
        CancellationTokenSource cts)
    {
        return type.GetProperties();
    }
}
