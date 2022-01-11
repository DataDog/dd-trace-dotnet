// <copyright file="Serverless.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;

using Datadog.Trace.ClrProfiler;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;

namespace Datadog.Trace.ClrProfiler.ServerlessInstrumentation
{
    internal static class Serverless
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(Serverless));

        private const string HandlerEnvName = "_HANDLER";
        private const string FunctionEnvame = "AWS_LAMBDA_FUNCTION_NAME";
        private const string ExtensionFullPath = "/opt/datadog-agent";
        private const string IntegrationType = "Datadog.Trace.ClrProfiler.ServerlessInstrumentation.AWS.Lambda";

        internal static void InitIfNeeded()
        {
            if (IsRunningInLambda())
            {
                Log.Debug("Sending CallTarget serverless integration definitions to native library.");
                var serverlessDefinitions = GetServerlessDefinitions();
                NativeMethods.InitializeProfiler(GetServerlessDefinitionsId(), serverlessDefinitions);
                foreach (var def in serverlessDefinitions)
                {
                    def.Dispose();
                }

                Log.Information<int>("The profiler has been initialized with {count} serverless definitions.", serverlessDefinitions.Length);
            }
        }

        internal static bool IsRunningInLambda()
        {
            return !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(FunctionEnvame)) && File.Exists(ExtensionFullPath);
        }

        internal static NativeCallTargetDefinition[] GetServerlessDefinitions()
        {
            LambdaHandler handler = new LambdaHandler(EnvironmentHelpers.GetEnvironmentVariable(HandlerEnvName));
            return new NativeCallTargetDefinition[] {
                new (handler.GetAssembly(), handler.GetFullType(), handler.GetMethodName(), handler.BuidParamTypeArray(), 0, 0, 0, 65535, 65535, 65535, "AWSLambda", IntegrationType)
            };
        }

        internal static string GetServerlessDefinitionsId()
        {
            return "4D16D270B8DC62693B5681458364AF42";
        }
    }
}
