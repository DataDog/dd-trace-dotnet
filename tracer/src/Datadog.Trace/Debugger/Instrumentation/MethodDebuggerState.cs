// <copyright file="MethodDebuggerState.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Datadog.Trace.Debugger.Snapshots;

namespace Datadog.Trace.Debugger.Instrumentation
{
    /// <summary>
    /// Live debugger execution state
    /// </summary>
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public ref struct MethodDebuggerState
    {
        private readonly string _probeId;
        private readonly Scope _scope;
        private readonly DateTimeOffset? _startTime;

        /// <summary>
        /// Used to perform a fast lookup to grab the proper <see cref="Instrumentation.MethodMetadataInfo"/>.
        /// This index is hard-coded into the method's instrumented bytecode.
        /// </summary>
        private readonly int _methodMetadataIndex;

        // Determines whether we should still be capturing values, or halt for any reason (e.g an exception was caused by our instrumentation, rate limiter threshold reached).
        internal bool IsActive = true;

        internal bool HasLocalsOrReturnValue;

        /// <summary>
        /// Initializes a new instance of the <see cref="MethodDebuggerState"/> struct.
        /// </summary>
        /// <param name="probeId">The id of the probe</param>
        /// <param name="scope">Scope instance</param>
        /// <param name="startTime">The intended start time of the scope, intended for scopes created in the OnMethodEnd handler</param>
        /// <param name="methodMetadataIndex">The unique index of the method's <see cref="Instrumentation.MethodMetadataInfo"/></param>
        internal MethodDebuggerState(string probeId, Scope scope, DateTimeOffset? startTime, int methodMetadataIndex)
        {
            _probeId = probeId;
            _scope = scope;
            _startTime = startTime;
            _methodMetadataIndex = methodMetadataIndex;
            HasLocalsOrReturnValue = false;
            SnapshotCreator = new DebuggerSnapshotCreator();
        }

        internal ref MethodMetadataInfo MethodMetadataInfo => ref MethodMetadataProvider.Get(_methodMetadataIndex);

        /// <summary>
        /// Gets the LiveDebugger SnapshotCreator
        /// </summary>
        internal DebuggerSnapshotCreator SnapshotCreator { get; }

        /// <summary>
        /// Gets the LiveDebugger BeginMethod scope
        /// </summary>
        internal Scope Scope => _scope;

        /// <summary>
        /// Gets the LiveDebugger state StartTime
        /// </summary>
        internal DateTimeOffset? StartTime => _startTime;

        /// <summary>
        /// Gets the Id of the probe
        /// </summary>
        internal string ProbeId => _probeId;

        /// <summary>
        /// Gets the default live debugger state (used by the native side to initialize the locals)
        /// </summary>
        /// <returns>Default live debugger state</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static MethodDebuggerState GetDefault()
        {
            return new MethodDebuggerState();
        }

        /// <summary>
        /// ToString override
        /// </summary>
        /// <returns>String value</returns>
        public override string ToString()
        {
            return $"{typeof(MethodDebuggerState).FullName}({_scope})";
        }
    }
}
