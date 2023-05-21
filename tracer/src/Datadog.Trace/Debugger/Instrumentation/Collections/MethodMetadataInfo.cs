// <copyright file="MethodMetadataInfo.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Reflection;
using Datadog.Trace.Debugger.Helpers;

namespace Datadog.Trace.Debugger.Instrumentation.Collections
{
    /// <summary>
    /// Holds data needed during Debugger instrumentation execution.
    /// </summary>
    internal readonly record struct MethodMetadataInfo
    {
        public MethodMetadataInfo(string[] parameterNames, string[] localVariableNames, Type type, MethodBase method, string filePath, string methodBeginLineNumber, string methodEndLineNumber, Dictionary<int, int> ilOffsetToLineNumberMapping)
            : this(parameterNames, localVariableNames, null, null, type, method, null, null, filePath, methodBeginLineNumber, methodEndLineNumber, ilOffsetToLineNumberMapping)
        {
        }

        public MethodMetadataInfo(string[] parameterNames, string[] localVariableNames, AsyncHelper.FieldInfoNameSanitized[] asyncMethodHoistedLocals, FieldInfo[] asyncMethodHoistedArguments, Type type, MethodBase method, Type kickoffType, MethodBase kickoffMethod, string filePath, string methodBeginLineNumber, string methodEndLineNumber, Dictionary<int, int> ilOffsetToLineNumberMapping)
        {
            ParameterNames = parameterNames ?? Array.Empty<string>();
            LocalVariableNames = localVariableNames ?? Array.Empty<string>();
            AsyncMethodHoistedLocals = asyncMethodHoistedLocals;
            AsyncMethodHoistedArguments = asyncMethodHoistedArguments ?? Array.Empty<FieldInfo>();
            DeclaringType = type;
            Method = method;
            KickoffInvocationTargetType = kickoffType;
            KickoffMethod = kickoffMethod;
            FilePath = filePath;
            MethodBeginLineNumber = methodBeginLineNumber;
            MethodEndLineNumber = methodEndLineNumber;
            ILOffsetToLineNumberMapping = ilOffsetToLineNumberMapping;
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
        /// Gets the path to the file where the method resides
        /// </summary>
        public string FilePath { get; }

        /// <summary>
        /// Gets the line number of the first line of code inside the method
        /// </summary>
        public string MethodBeginLineNumber { get; }

        /// <summary>
        /// Gets the line number of the last line of code inside the method
        /// </summary>
        public string MethodEndLineNumber { get; }

        /// <summary>
        /// Gets the mapping IL Offset -> IL Number.
        /// </summary>
        public Dictionary<int, int> ILOffsetToLineNumberMapping { get; }
    }
}
