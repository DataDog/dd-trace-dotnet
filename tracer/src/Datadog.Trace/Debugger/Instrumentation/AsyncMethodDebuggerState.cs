// <copyright file="AsyncMethodDebuggerState.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using Datadog.Trace.Debugger.Snapshots;

namespace Datadog.Trace.Debugger.Instrumentation
{
    /// <summary>
    /// Live debugger async execution state
    /// It must be a class type because we have a cycle reference to parent state
    /// </summary>
    public class AsyncMethodDebuggerState
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AsyncMethodDebuggerState"/> class.
        /// </summary>
        internal AsyncMethodDebuggerState()
        {
            HasLocalsOrReturnValue = false;
            HasArguments = false;
            SnapshotCreator = new DebuggerSnapshotCreator();
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
        /// Gets or sets a value of the parent (caller) state
        /// </summary>
        internal AsyncMethodDebuggerState Parent { get; set; }

        /// <summary>
        /// Gets the value of the MethodMetadataInfo that related to the current async method
        /// </summary>
        internal ref MethodMetadataInfo MethodMetadataInfo => ref MethodMetadataProvider.Get(MethodMetadataIndex);

        /// <summary>
        /// Gets the LiveDebugger SnapshotCreator
        /// </summary>
        internal DebuggerSnapshotCreator SnapshotCreator { get; }

        /// <summary>
        /// Gets or sets the LiveDebugger BeginMethod scope
        /// </summary>
        internal Scope Scope { get; set; }

        /// <summary>
        /// Gets or sets the LiveDebugger state StartTime
        /// </summary>
        internal DateTimeOffset? StartTime { get; set; }

        /// <summary>
        /// Gets or sets the Id of the probe
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
        /// Gets or sets the type that represents the "this" object of the async kick-off method (i.e. original method)
        /// </summary>
        public Type KickoffInvocationTargetType { get; set; }

        /// <summary>
        /// Gets or sets the type that represents the kickoff method of the state machine MoveNext (i.e. original method)
        /// </summary>
        public MethodBase KickoffMethod { get; set; }

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
        public static AsyncMethodDebuggerState CreateInvalidatedDebuggerState()
        {
            var state = new AsyncMethodDebuggerState();
            state.IsActive = false;
            return state;
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
