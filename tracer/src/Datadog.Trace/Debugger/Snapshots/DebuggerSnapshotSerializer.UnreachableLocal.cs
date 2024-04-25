// <copyright file="DebuggerSnapshotSerializer.UnreachableLocal.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;

namespace Datadog.Trace.Debugger.Snapshots
{
    internal static partial class DebuggerSnapshotSerializer
    {
        /// <summary>
        /// This class hold a local variable inside an async method that was not hoisted by
        /// the state machine object but kept as regular local. Hence we can not obtain its value
        /// at method end and the value will be always the default value.
        /// We want to display in the snapshot that for this value we could not obtain the real value
        /// </summary>
        internal struct UnreachableLocal
        {
            public readonly string Reason;

            public UnreachableLocal(string reason)
            {
                Reason = reason;
            }
        }

        internal static class UnreachableLocalReason
        {
            internal const string NotHoistedLocalInAsyncMethod = "The value of this variable in not available in the specified probe location. Place a line probe closer to where the variable was assigned or used to obtain its value.";
        }
    }
}
