// <copyright file="MethodDebuggerState.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Datadog.Trace.Debugger.Configurations.Models;
using Datadog.Trace.Debugger.Instrumentation.Collections;
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

        /// <summary>
        /// Used to perform a fast lookup to grab the proper <see cref="Collections.MethodMetadataInfo"/>.
        /// This index is hard-coded into the method's instrumented bytecode.
        /// </summary>
        private readonly int _methodMetadataIndex;
        private readonly ProbeData _probeData;
        private readonly DebuggerSnapshotCreator _snapshotCreator;

        // Determines whether we should still be capturing values, or halt for any reason (e.g an exception was caused by our instrumentation, rate limiter threshold reached).

        internal bool HasLocalsOrReturnValue;
        internal object InvocationTarget;
        private EvaluateAt _methodPhase;
        private bool _isActive = true;
        private Exception _exceptionThrown = null;

        /// <summary>
        /// Initializes a new instance of the <see cref="MethodDebuggerState"/> struct.
        /// </summary>
        /// <param name="probeId">The id of the probe</param>
        /// <param name="scope">Scope instance</param>
        /// <param name="methodMetadataIndex">The unique index of the method's <see cref="Collections.MethodMetadataInfo"/></param>
        /// <param name="probeData">The <see cref="ProbeData"/> associated with the executing instrumentation</param>
        /// <param name="invocationTarget">The current invocation target ('this' object)</param>
        internal MethodDebuggerState(string probeId, Scope scope, int methodMetadataIndex, ref ProbeData probeData, object invocationTarget)
        {
            _probeId = probeId;
            _scope = scope;
            _methodMetadataIndex = methodMetadataIndex;
            HasLocalsOrReturnValue = false;
            InvocationTarget = invocationTarget;
            _snapshotCreator = DebuggerSnapshotCreator.BuildSnapshotCreator(probeData.Processor);
            _probeData = probeData;
            MethodPhase = EvaluateAt.Entry;
        }

        internal EvaluateAt MethodPhase
        {
            readonly get => _methodPhase;
            set => _methodPhase = value;
        }

        internal ref MethodMetadataInfo MethodMetadataInfo => ref MethodMetadataCollection.Instance.Get(_methodMetadataIndex);

        internal readonly ProbeData ProbeData => _probeData;

        internal bool IsActive
        {
            get => _isActive;
            // ReSharper disable once RedundantCheckBeforeAssignment
            set
            {
                if (_isActive != value)
                {
                    _isActive = value;
                }
            }
        }

        /// <summary>
        /// Gets the LiveDebugger SnapshotCreator
        /// </summary>
        internal readonly DebuggerSnapshotCreator SnapshotCreator => _snapshotCreator;

        /// <summary>
        /// Gets the LiveDebugger BeginMethod scope
        /// </summary>
        internal Scope Scope => _scope;

        /// <summary>
        /// Gets the Id of the probe
        /// </summary>
        internal string ProbeId => _probeId;

        /// <summary>
        /// Gets or sets an Exception
        /// </summary>
        internal Exception ExceptionThrown
        {
            readonly get => _exceptionThrown;
            set => _exceptionThrown = value;
        }

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
