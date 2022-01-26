// <copyright file="AssemblyProcessor.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.ClrProfiler;
using Datadog.Trace.ClrProfiler.CallTarget;

namespace Datadog.Trace.Tools.Runner.Aot
{
    internal class AssemblyProcessor
    {
        private static readonly Type CallTargetInvokerType = typeof(CallTargetInvoker);
        private static readonly Type CallTargetStateType = typeof(CallTargetState);
        private static readonly Type CallTargetReturnType = typeof(CallTargetReturn);
        private static readonly Type CallTargetReturnGenericType = typeof(CallTargetReturn<>);

        private readonly string _inputPath;
        private readonly string _outputPath;
        private readonly NativeCallTargetDefinition[] _definitions;
        private readonly NativeCallTargetDefinition[] _derivedDefinitions;

        public AssemblyProcessor(string inputPath, string outputPath, NativeCallTargetDefinition[] definitions, NativeCallTargetDefinition[] derivedDefinitions)
        {
            _inputPath = inputPath;
            _outputPath = outputPath;
            _definitions = definitions;
            _derivedDefinitions = derivedDefinitions;
        }
    }
}
