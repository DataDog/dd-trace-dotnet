// <copyright file="Serverless.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;

namespace Datadog.Trace.ClrProfiler.ServerlessInstrumentation
{
    internal static class Serverless
    {
        private const string HandlerEnvName = "_HANDLER";
        private const string FunctionEnvame = "AWS_LAMBDA_FUNCTION_NAME";
        private const string IntegrationType = "Datadog.Trace.ClrProfiler.ServerlessInstrumentation.AWS.Lambda";

        internal static bool IsRunningInLambda()
        {
            return !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(FunctionEnvame));
        }

        internal static NativeCallTargetDefinition BuildLambdaHandlerDefinition(string assemblyFullName)
        {
            LambdaHandler handler = new LambdaHandler(Environment.GetEnvironmentVariable(HandlerEnvName));
            return new NativeCallTargetDefinition(handler.GetAssembly(), handler.GetFullType(), handler.GetMethodName(), handler.BuidParamTypeArray(), 1, 0, 0, 5, 65535, 65535, assemblyFullName, IntegrationType);
        }

        internal static NativeCallTargetDefinition[] GetDefinitions(NativeCallTargetDefinition[] definitions, string assemblyFullName)
        {
            NativeCallTargetDefinition[] definitionsWithServerless = new NativeCallTargetDefinition[definitions.Length + 1];
            for (var i = 0; i < definitions.Length; i++)
            {
                definitionsWithServerless[i] = definitions[i];
            }

            definitionsWithServerless[definitions.Length] = BuildLambdaHandlerDefinition(assemblyFullName);
            return definitionsWithServerless;
        }
    }
}
