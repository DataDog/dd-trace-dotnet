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
        private const string ExtensionEnvName = "_DD_EXTENSION_PATH";
        private const string ExtensionFullPath = "/opt/extensions/datadog-agent";
        private const string FunctionEnvame = "AWS_LAMBDA_FUNCTION_NAME";
        private const string HandlerEnvName = "_HANDLER";
        private const string LogLevelEnvName = "DD_LOG_LEVEL";

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(Serverless));

        internal static void InitIfNeeded()
        {
            if (IsRunningInLambda(ExtensionFullPath))
            {
                Debug("Sending CallTarget serverless integration definitions to native library.");
                var serverlessDefinitions = GetServerlessDefinitions();
                NativeMethods.InitializeProfiler(DefinitionsId, serverlessDefinitions);
                foreach (var def in serverlessDefinitions)
                {
                    def.Dispose();
                }

                Debug("The profiler has been initialized with serverless definitions, count = " + serverlessDefinitions.Length);
            }
        }

        internal static bool IsRunningInLambda(string extensionPath)
        {
            string path = EnvironmentHelpers.GetEnvironmentVariable(ExtensionEnvName) ?? extensionPath;
            return File.Exists(path) && !string.IsNullOrEmpty(EnvironmentHelpers.GetEnvironmentVariable(FunctionEnvame));
        }

        internal static NativeCallTargetDefinition[] GetServerlessDefinitions()
        {
            try
            {
                LambdaHandler handler = new LambdaHandler(EnvironmentHelpers.GetEnvironmentVariable(HandlerEnvName));
                var assemblyName = typeof(InstrumentationDefinitions).Assembly.FullName;
                var paramCount = handler.ParamTypeArray.Length;
                var integrationType = handler.ParamTypeArray[0].StartsWith(ClrNames.Task) ? GetAsyncIntegrationTypeFromParamCount(paramCount) :
                    GetSyncIntegrationTypeFromParamCount(paramCount);
                return new NativeCallTargetDefinition[]
                {
                    new(handler.GetAssembly(), handler.GetFullType(), handler.GetMethodName(), handler.ParamTypeArray, 0, 0, 0, 65535, 65535, 65535, assemblyName, integrationType)
                };
            }
            catch (Exception ex)
            {
                Error("Impossible to get Serverless Definitions", ex);
                return Array.Empty<NativeCallTargetDefinition>();
            }
        }

        internal static string GetAsyncIntegrationTypeFromParamCount(int paramCount)
        {
            var inputParamCount = paramCount - 1; // since the return type is in the array
            switch (inputParamCount)
            {
                case 0:
                    return typeof(AWS.LambdaNoParamAsync).FullName;
                case 1:
                    return typeof(AWS.LambdaOneParamAsync).FullName;
                case 2:
                    return typeof(AWS.LambdaTwoParamsAsync).FullName;
                default:
                    throw new ArgumentOutOfRangeException("AWS Lambda handler number of params can only be 0, 1 or 2.");
            }
        }

        internal static string GetSyncIntegrationTypeFromParamCount(int paramCount)
        {
            var inputParamCount = paramCount - 1; // since the return type is in the array
            switch (inputParamCount)
            {
                case 0:
                    return typeof(AWS.LambdaNoParamSync).FullName;
                case 1:
                    return typeof(AWS.LambdaOneParamSync).FullName;
                case 2:
                    return typeof(AWS.LambdaTwoParamsSync).FullName;
                default:
                    throw new ArgumentOutOfRangeException("AWS Lambda handler number of params can only be 0, 1 or 2.");
            }
        }

        internal static void Debug(string str)
        {
            if (EnvironmentHelpers.GetEnvironmentVariable(LogLevelEnvName)?.ToLower() == "debug")
            {
                Console.WriteLine("{0} {1}", DateTime.UtcNow.ToString("yyyy-MM-dd MM:mm:ss:fff"), str);
            }
        }

        internal static void Error(string message, Exception ex = null)
        {
            Console.WriteLine($"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss:fff} {message} {ex?.ToString().Replace("\n", "\\n")}");
        }
    }
}
