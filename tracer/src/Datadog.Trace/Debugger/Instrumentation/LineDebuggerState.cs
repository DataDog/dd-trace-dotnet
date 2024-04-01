// <copyright file="LineDebuggerState.cs" company="Datadog">
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
    /// Live debugger execution state
    /// </summary>
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public ref struct LineDebuggerState
    {
        private readonly string _probeId;
        private readonly Scope _scope;
        private readonly string _probeFilePath;
        private readonly int _lineNumber;

        /// <summary>
        /// Backing field of <see cref="MethodMetadataIndex"/>.
        /// Used to perform a fast lookup to grab the proper <see cref="Collections.MethodMetadataInfo"/>.
        /// This index is hard-coded into the method's instrumented bytecode.
        /// </summary>
        private readonly int _methodMetadataIndex;

        // Determines whether we should still be capturing values, or halt for any reason (e.g an exception was caused by our instrumentation, rate limiter threshold reached).
        internal bool IsActive = true;
        internal bool HasLocalsOrReturnValue;

        /// <summary>
        /// Initializes a new instance of the <see cref="LineDebuggerState"/> struct.
        /// </summary>
        /// <param name="probeId">The id of the probe</param>
        /// <param name="scope">Scope instance</param>
        /// <param name="methodMetadataIndex">The unique index of the method's <see cref="Collections.MethodMetadataInfo"/></param>
        /// <param name="probeData">The <see cref="ProbeData"/> associated with the executing instrumentation</param>
        /// <param name="lineNumber">The line number where the probe is located on</param>
        /// <param name="probeFilePath">The path to the file of the probe</param>
        /// <param name="invocationTarget">The instance object (or null for static methods)</param>
        internal LineDebuggerState(string probeId, Scope scope, int methodMetadataIndex, ref ProbeData probeData, int lineNumber, string probeFilePath, object invocationTarget)
        {
            _probeId = probeId;
            _scope = scope;
            _methodMetadataIndex = methodMetadataIndex;
            _lineNumber = lineNumber;
            _probeFilePath = probeFilePath;
            HasLocalsOrReturnValue = false;
            var processor = probeData.Processor;
            SnapshotCreator = processor.CreateSnapshotCreator();
            ProbeData = probeData;
            InvocationTarget = invocationTarget;
        }

        /// <summary>
        /// Gets an index that is used as a fast lookup to grab the proper <see cref="Collections.MethodMetadataInfo"/>.
        /// This index is hard-coded into the method's instrumented bytecode.
        /// </summary>
        internal int MethodMetadataIndex
        {
            get
            {
                return _methodMetadataIndex;
            }
        }

        internal ref MethodMetadataInfo MethodMetadataInfo => ref MethodMetadataCollection.Instance.Get(_methodMetadataIndex);

        internal ProbeData ProbeData { get; }

        /// <summary>
        /// Gets the LiveDebugger SnapshotCreator
        /// </summary>
        internal IDebuggerSnapshotCreator SnapshotCreator { get; }

        /// <summary>
        /// Gets the LiveDebugger BeginMethod scope
        /// </summary>
        internal Scope Scope => _scope;

        /// <summary>
        /// Gets the Id of the probe
        /// </summary>
        internal string ProbeId => _probeId;

        /// <summary>
        /// Gets the location of the probe
        /// </summary>
        internal string ProbeFilePath => _probeFilePath;

        /// <summary>
        /// Gets the Line Number
        /// </summary>
        internal int LineNumber => _lineNumber;

        internal object InvocationTarget { get; }

        /// <summary>
        /// Gets the default live debugger state (used by the native side to initialize the locals)
        /// </summary>
        /// <returns>Default live debugger state</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static LineDebuggerState GetDefault()
        {
            return new LineDebuggerState();
        }

        /// <summary>
        /// ToString override
        /// </summary>
        /// <returns>String value</returns>
        public override string ToString()
        {
            return $"{typeof(LineDebuggerState).FullName}({_scope})";
        }
    }
}
