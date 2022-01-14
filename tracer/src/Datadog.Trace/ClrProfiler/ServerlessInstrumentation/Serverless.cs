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
        private const string DefinitionsId = "68224F20D001430F9400668DD25245BA";
        private const string ExtensionFullPath = "/opt/extensions/datadog-agent";
        private const string FunctionEnvame = "AWS_LAMBDA_FUNCTION_NAME";
        private const string HandlerEnvName = "_HANDLER";

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(Serverless));

        internal static void InitIfNeeded()
        {
            if (IsRunningInLambda(ExtensionFullPath))
            {
                Log.Debug("Sending CallTarget serverless integration definitions to native library.");
                var serverlessDefinitions = GetServerlessDefinitions();
                NativeMethods.InitializeProfiler(DefinitionsId, serverlessDefinitions);
                foreach (var def in serverlessDefinitions)
                {
                    def.Dispose();
                }

                Log.Information<int>("The profiler has been initialized with {count} serverless definitions.", serverlessDefinitions.Length);
            }
        }

        internal static bool IsRunningInLambda(string extensionPath)
        {
            return File.Exists(extensionPath) && !string.IsNullOrEmpty(EnvironmentHelpers.GetEnvironmentVariable(FunctionEnvame));
        }

        internal static NativeCallTargetDefinition[] GetServerlessDefinitions()
        {
            try
            {
                LambdaHandler handler = new LambdaHandler(EnvironmentHelpers.GetEnvironmentVariable(HandlerEnvName));
                string assymblyName = typeof(InstrumentationDefinitions).Assembly.FullName;
                string integrationType = GetIntegrationTypeFromParamCount(handler.ParamTypeArray.Length);
                return new NativeCallTargetDefinition[]
                {
                    new(handler.GetAssembly(), handler.GetFullType(), handler.GetMethodName(), handler.ParamTypeArray, 0, 0, 0, 65535, 65535, 65535, assymblyName, integrationType)
                };
            }
            catch (Exception ex)
            {
                Log.Error(ex, ex.Message);
                return Array.Empty<NativeCallTargetDefinition>();
            }
        }

        internal static string GetIntegrationTypeFromParamCount(int paramCount)
        {
            int inputParamCount = paramCount - 1; // since the return type is in the array
            switch (inputParamCount)
            {
                case 0:
                    return typeof(AWS.LambdaNoParam).FullName;
                case 1:
                    return typeof(AWS.LambdaOneParam).FullName;
                case 2:
                    return typeof(AWS.LambdaTwoParams).FullName;
                default:
                    throw new ArgumentOutOfRangeException("AWS Lambda handler number of params can only be 0, 1 or 2.");
            }
        }
    }
}
