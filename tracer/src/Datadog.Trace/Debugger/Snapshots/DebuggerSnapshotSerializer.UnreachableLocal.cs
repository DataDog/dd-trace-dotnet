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
        /// the state machine object but kept as regular local. Hence we can not get his value
        /// at method end and the value will be always default.
        /// We want to display in the snapshot that for this value we could not achieve the real value
        /// </summary>
        internal struct UnreachableLocal
        {
            // we are boxing it anyway in the serialization phase
            internal object Value;

            public readonly string Reason;

            public UnreachableLocal(object value, string reason)
            {
                Value = value;
                Reason = reason;
            }
        }

        internal static class UnreachableLocalReason
        {
            internal const string NotHoistedLocalInAsyncMethod = "The value of the variable in an asynchronous method may not be available at this point in time. You can put a line probe after the variable has been assigned to get its value.";
        }
    }
}
