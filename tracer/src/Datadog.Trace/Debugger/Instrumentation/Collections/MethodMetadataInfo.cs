// <copyright file="MethodMetadataInfo.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Reflection;
using Datadog.Trace.Debugger.Helpers;

namespace Datadog.Trace.Debugger.Instrumentation.Collections
{
    /// <summary>
    /// Holds data needed during Debugger instrumentation execution.
    /// </summary>
    internal record struct MethodMetadataInfo
    {
        private readonly object _locker = new();

        public MethodMetadataInfo(string[] parameterNames, string[] localVariableNames, Type type, MethodBase method)
            : this(parameterNames, localVariableNames, null, null, type, method, null, null)
        {
        }

        public MethodMetadataInfo(string[] parameterNames, string[] localVariableNames, AsyncHelper.FieldInfoNameSanitized[] asyncMethodHoistedLocals, FieldInfo[] asyncMethodHoistedArguments, Type type, MethodBase method, Type kickoffType, MethodBase kickoffMethod)
        {
            ParameterNames = parameterNames;
            LocalVariableNames = localVariableNames ?? Array.Empty<string>();
            AsyncMethodHoistedLocals = asyncMethodHoistedLocals ?? Array.Empty<AsyncHelper.FieldInfoNameSanitized>();
            AsyncMethodHoistedArguments = asyncMethodHoistedArguments ?? Array.Empty<FieldInfo>();
            DeclaringType = type;
            Method = method;
            KickoffInvocationTargetType = kickoffType;
            KickoffMethod = kickoffMethod;
        }

        public string[] ParameterNames { get; }

        /// <summary>
        /// Gets the names of the method's local variable, in the same order as they appear in the method's LocalVarSig.
        /// May contain null entries to denote compiler generated locals whose names are meaningless.
        /// </summary>
        public string[] LocalVariableNames { get; }

        /// <summary>
        /// Gets the locals of the async method's from the heap-allocated object that holds them - i.e. the state machine.
        /// May contains fields that are not locals in the async kick-off method, ATM we skip all fields that starts with <see cref="AsyncHelper.StateMachineFieldsNamePrefix"/>>
        /// </summary>
        public AsyncHelper.FieldInfoNameSanitized[] AsyncMethodHoistedLocals { get; }

        /// <summary>
        /// Gets the arguments of the async method's from the heap-allocated object that holds them - i.e. the state machine.
        /// </summary>
        public FieldInfo[] AsyncMethodHoistedArguments { get; }

        /// <summary>
        /// Gets the declaring type of the instrumented method
        /// </summary>
        public Type DeclaringType { get; }

        /// <summary>
        /// Gets the method base of the instrumented method
        /// </summary>
        public MethodBase Method { get; }

        /// <summary>
        /// Gets the type that represents the "this" object of the async kick-off method (i.e. original method)
        /// </summary>
        public Type KickoffInvocationTargetType { get; }

        /// <summary>
        /// Gets the type that represents the kickoff method of the state machine MoveNext (i.e. original method)
        /// </summary>
        public MethodBase KickoffMethod { get; }

        /// <summary>
        /// Gets the probe ids actively instrumented
        /// </summary>
        public string[] ProbeIds { get; private set; }

        /// <summary>
        /// Gets the indices of the probes, used to lookup for <see cref="ProbeData"/> from <see cref="ProbeDataCollection"/>.
        /// </summary>
        public int[] ProbeMetadataIndices { get; private set; }

        /// <summary>
        /// Gets the unique version of the active instrumentation that is being applied. It basically identifies the probe ids combination that is currently applied.
        /// If a probe is getting added and/or removed, then this number will be re-updated using new instrumentation, and have a new version number, "hard-coded" by the instrumentation.
        /// </summary>
        public int InstrumentationVersion { get; private set; } = -1;

        /// <summary>
        /// Updates, atomically using a locking object for syncing, <see cref="ProbeIds"/> and <see cref="ProbeMetadataIndices"/>.
        /// </summary>
        /// <param name="probeIds">Updated probe ids.</param>
        /// <param name="probeMetadataIndices">Updated probe metadata indices.</param>
        /// <param name="instrumentationVersion">The (new) unique identifier of the instrumentation that is currently applied.</param>
        public void Update(string[] probeIds, int[] probeMetadataIndices, int instrumentationVersion)
        {
            lock (_locker)
            {
                ProbeIds = probeIds;
                ProbeMetadataIndices = probeMetadataIndices;
                InstrumentationVersion = instrumentationVersion;
            }
        }
    }
}
