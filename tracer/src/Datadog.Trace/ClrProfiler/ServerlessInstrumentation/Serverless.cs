// <copyright file="Serverless.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;

using Datadog.Trace.Logging;
using Datadog.Trace.Util;

namespace Datadog.Trace.ClrProfiler.ServerlessInstrumentation
{
    internal static class Serverless
    {
        private const string ExtensionFullPath = "/opt/extensions/datadog-agent";
        private const string FunctionEnvame = "AWS_LAMBDA_FUNCTION_NAME";
        private const string HandlerEnvName = "_HANDLER";

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(Serverless));

        internal static void InitIfNeeded()
        {
            if (IsRunningInLambda(ExtensionFullPath))
            {
                Console.WriteLine("Sending CallTarget serverless integration definitions to native library.");
                var serverlessDefinitions = GetServerlessDefinitions();
                NativeMethods.InitializeProfiler(GetServerlessDefinitionsId(), serverlessDefinitions);
                foreach (var def in serverlessDefinitions)
                {
                    def.Dispose();
                }

                Console.WriteLine("The profiler has been initialized with serverless definitions count = " + serverlessDefinitions.Length);
            }
        }

        internal static bool IsRunningInLambda(string extensionPath)
        {
            return !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(FunctionEnvame)) && File.Exists(extensionPath);
        }

        internal static NativeCallTargetDefinition[] GetServerlessDefinitions()
        {
            var serverlessDefinitions = Array.Empty<NativeCallTargetDefinition>();
            try
            {
                LambdaHandler handler = new LambdaHandler(EnvironmentHelpers.GetEnvironmentVariable(HandlerEnvName));
                string assymblyName = typeof(InstrumentationDefinitions).Assembly.FullName;
                string integrationType = GetIntegrationTypeFromParamCount(handler.GetParamTypeArray().Length);
                serverlessDefinitions = new NativeCallTargetDefinition[]
                {
                    new(handler.GetAssembly(), handler.GetFullType(), handler.GetMethodName(), handler.GetParamTypeArray(), 0, 0, 0, 65535, 65535, 65535, assymblyName, integrationType)
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine("oppsy" + ex);
                Log.Error(ex, ex.Message);
            }

            return serverlessDefinitions;
        }

        internal static string GetServerlessDefinitionsId()
        {
            return "4D16D270B8DC62693B5681458364AF42";
        }

        internal static string GetIntegrationTypeFromParamCount(int paramCount)
        {
            int inputParamCount = paramCount - 1; // since the return type is in the array
            if (inputParamCount == 0)
            {
                return typeof(AWS.LambdaNoParam).FullName;
            }

            if (inputParamCount == 1)
            {
                return typeof(AWS.LambdaOneParam).FullName;
            }

            if (inputParamCount == 2)
            {
                return typeof(AWS.LambdaTwoParams).FullName;
            }

            throw new ArgumentOutOfRangeException("AWS Lambda handler number of params can only be 0, 1 or 2.");
        }
    }
}
