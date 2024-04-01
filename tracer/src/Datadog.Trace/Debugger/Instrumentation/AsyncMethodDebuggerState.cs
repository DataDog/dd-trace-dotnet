// <copyright file="AsyncMethodDebuggerState.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Datadog.Trace.Debugger.Instrumentation.Collections;
using Datadog.Trace.Debugger.Snapshots;

namespace Datadog.Trace.Debugger.Instrumentation
{
    /// <summary>
    /// Live debugger async execution state
    /// It must be a class type because we have a cycle reference to parent state
    /// </summary>
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class AsyncMethodDebuggerState
    {
        /// <summary>
        /// Gets a disabled state
        /// </summary>
        internal static readonly AsyncMethodDebuggerState[] DisabledStates = { new() { IsActive = false } };

        /// <summary>
        /// Initializes a new instance of the <see cref="AsyncMethodDebuggerState"/> class.
        /// </summary>
        internal AsyncMethodDebuggerState(string probeId, ref ProbeData probeData)
        {
            ProbeId = probeId;
            HasLocalsOrReturnValue = false;
            HasArguments = false;
            var processor = probeData.Processor;
            SnapshotCreator = processor.CreateSnapshotCreator();
            ProbeData = probeData;
        }

        private AsyncMethodDebuggerState()
        {
        }

        /// <summary>
        /// Gets or sets a value indicating whether we should still be capturing values,
        /// or halt for any reason (e.g an exception was caused by our instrumentation, rate limiter threshold reached).
        /// </summary>
        internal bool IsActive { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether the current state of the async method captured locals or return value
        /// </summary>
        internal bool HasLocalsOrReturnValue { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the current state of the async method captured arguments
        /// </summary>
        internal bool HasArguments { get; set; }

        /// <summary>
        /// Gets the value of the MethodMetadataInfo that related to the current async method
        /// </summary>
        internal ref MethodMetadataInfo MethodMetadataInfo => ref MethodMetadataCollection.Instance.Get(MethodMetadataIndex);

        /// <summary>
        /// Gets the value of ProbeData associated with the state
        /// </summary>
        internal ProbeData ProbeData { get; }

        /// <summary>
        /// Gets the LiveDebugger SnapshotCreator
        /// </summary>
        internal IDebuggerSnapshotCreator SnapshotCreator { get; }

        /// <summary>
        /// Gets or sets the LiveDebugger BeginMethod scope
        /// </summary>
        internal Scope Scope { get; set; }

        /// <summary>
        /// Gets or sets the LiveDebugger state StartTime
        /// </summary>
        internal DateTimeOffset? StartTime { get; set; }

        /// <summary>
        /// Gets or sets the method probe ID
        /// </summary>
        internal string ProbeId { get; set; }

        /// <summary>
        /// Gets or sets the object that represents the "this" object of the async kick-off method (i.e. original method)
        /// We can save it in some cases as TTarget but because we use it in serialization as boxed object anyway, we save it as object here
        /// </summary>
        public object KickoffInvocationTarget { get; set; }

        /// <summary>
        /// Gets or sets the object that represents the "this" object of the async MoveNext method
        /// We can save it in some cases as TTarget but because we use it in serialization as boxed object anyway, we save it as object here
        /// </summary>
        public object MoveNextInvocationTarget { get; set; }

        /// <summary>
        /// Gets or sets the MethodMetadataIndex
        /// Used to perform a fast lookup to grab the proper <see cref="MethodMetadataInfo"/>.
        /// This unique index is hard-coded into the method's instrumented byte-code.
        /// </summary>
        public int MethodMetadataIndex { get; set; }

        /// <summary>
        /// Gets invalid live debugger state
        /// </summary>
        /// <returns>Invalid live debugger state</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static AsyncMethodDebuggerState[] CreateInvalidatedDebuggerStates()
        {
            return DisabledStates;
        }

        /// <summary>
        /// Gets invalid live debugger state
        /// </summary>
        /// <returns>Invalid live debugger state</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static AsyncMethodDebuggerState CreateInvalidatedDebuggerState()
        {
            return DisabledStates[0];
        }

        /// <summary>
        /// ToString override
        /// </summary>
        /// <returns>String value</returns>
        public override string ToString()
        {
            return $"{typeof(AsyncMethodDebuggerState).FullName}({Scope})";
        }
    }
}
