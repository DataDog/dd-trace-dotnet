// <copyright file="MethodMetadataInfo.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Datadog.Trace.Debugger.Instrumentation
{
    /// <summary>
    /// Holds data needed during Debugger instrumentation execution.
    /// </summary>
    internal readonly record struct MethodMetadataInfo
    {
        public MethodMetadataInfo(string[] parameterNames, string[] localVariableNames)
        {
            ParameterNames = parameterNames;
            LocalVariableNames = localVariableNames;
        }

        public string[] ParameterNames { get; init; }

        /// <summary>
        /// Gets the names of the method's local variable, in the same order as they appear in the method's LocalVarSig.
        /// May contain null entries to denote compiler generated locals whose names are meaningless.
        /// </summary>
        public string[] LocalVariableNames { get; init; }
    }
}
