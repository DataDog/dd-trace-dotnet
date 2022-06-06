// <copyright file="MethodMetadataInfo.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Reflection;
using Datadog.Trace.Debugger.Helpers;

namespace Datadog.Trace.Debugger.Instrumentation
{
    /// <summary>
    /// Holds data needed during Debugger instrumentation execution.
    /// </summary>
    internal readonly record struct MethodMetadataInfo
    {
        public MethodMetadataInfo(string[] parameterNames, string[] localVariableNames, Type type, MethodBase method)
            : this(parameterNames, localVariableNames, null, type, method)
        {
        }

        public MethodMetadataInfo(string[] parameterNames, string[] localVariableNames, List<AsyncHelper.FieldNameValue> asyncMethodHoistedLocals, Type type, MethodBase method)
        {
            ParameterNames = parameterNames;
            LocalVariableNames = localVariableNames;
            AsyncMethodHoistedLocals = asyncMethodHoistedLocals;
            DeclaringType = type;
            Method = method;
        }

        public string[] ParameterNames { get; }

        /// <summary>
        /// Gets the names of the method's local variable, in the same order as they appear in the method's LocalVarSig.
        /// May contain null entries to denote compiler generated locals whose names are meaningless.
        /// </summary>
        public string[] LocalVariableNames { get; }

        /// <summary>
        /// Gets the names and the values of the async method's local variable from the hoist object - i.e. the state machine.
        /// May contains fields that they not locals in the kick off method, ATM we skip only the `builder` and `this` fields
        /// </summary>
        public List<AsyncHelper.FieldNameValue> AsyncMethodHoistedLocals { get; }

        /// <summary>
        /// Gets the declaring type of the instrumented method
        /// </summary>
        public Type DeclaringType { get; }

        /// <summary>
        /// Gets the method base of the instrumented method
        /// </summary>
        public MethodBase Method { get;  }
    }
}
