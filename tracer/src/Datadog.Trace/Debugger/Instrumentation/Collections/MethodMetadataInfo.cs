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
    internal readonly record struct MethodMetadataInfo
    {
        public MethodMetadataInfo(string[] parameterNames, string[] localVariableNames, Type type, MethodBase method)
            : this(parameterNames, localVariableNames, null, null, type, method, null, null)
        {
        }

        public MethodMetadataInfo(string[] parameterNames, string[] localVariableNames, AsyncHelper.FieldInfoNameSanitized[] asyncMethodHoistedLocals, FieldInfo[] asyncMethodHoistedArguments, Type type, MethodBase method, Type kickoffType, MethodBase kickoffMethod)
        {
            ParameterNames = parameterNames;
            LocalVariableNames = localVariableNames;
            AsyncMethodHoistedLocals = asyncMethodHoistedLocals;
            AsyncMethodHoistedArguments = asyncMethodHoistedArguments;
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
    }
}
