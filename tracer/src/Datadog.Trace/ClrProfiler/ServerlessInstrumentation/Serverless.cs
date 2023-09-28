// <copyright file="Serverless.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Util;

namespace Datadog.Trace.ClrProfiler.ServerlessInstrumentation
{
    internal static class Serverless
    {
        private const string DefinitionsId = "68224F20D001430F9400668DD25245BA";
        private const string LogLevelEnvName = "DD_LOG_LEVEL";

        private static NativeCallTargetDefinition[] callTargetDefinitions = null;

        internal static void InitIfNeeded(LambdaMetadata metadata)
        {
            if (metadata is { IsRunningInLambda: true, HandlerName: var handlerName })
            {
                if (string.IsNullOrEmpty(handlerName))
                {
                    Error("Error initializing serverless integration: handler name not found");
                    return;
                }

                Debug("Sending CallTarget serverless integration definitions to native library.");
                var serverlessDefinitions = GetServerlessDefinitions(handlerName);
                NativeMethods.InitializeProfiler(DefinitionsId, serverlessDefinitions);

                Debug("The profiler has been initialized with serverless definitions, count = " + serverlessDefinitions.Length);
            }
        }

        internal static string GetIntegrationType(string returnType, int paramCount)
        {
            var inputParamCount = paramCount - 1; // since the return type is in the array
            if (returnType.Equals(ClrNames.Void))
            {
                return GetVoidIntegrationTypeFromParamCount(inputParamCount);
            }

            if (returnType.StartsWith(ClrNames.Task))
            {
                return GetAsyncIntegrationTypeFromParamCount(inputParamCount);
            }

            return GetSyncIntegrationTypeFromParamCount(inputParamCount);
        }

        internal static string GetAsyncIntegrationTypeFromParamCount(int paramCount)
        {
            switch (paramCount)
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
            switch (paramCount)
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

        internal static string GetVoidIntegrationTypeFromParamCount(int paramCount)
        {
            switch (paramCount)
            {
                case 0:
                    return typeof(AWS.LambdaNoParamVoid).FullName;
                case 1:
                    return typeof(AWS.LambdaOneParamVoid).FullName;
                case 2:
                    return typeof(AWS.LambdaTwoParamsVoid).FullName;
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

        private static void RunShutdown()
        {
            callTargetDefinitions = null;
        }

        private static NativeCallTargetDefinition[] GetServerlessDefinitions(string handlerName)
        {
            try
            {
                if (callTargetDefinitions == null)
                {
                    LambdaHandler handler = new LambdaHandler(handlerName);
                    var assemblyName = typeof(InstrumentationDefinitions).Assembly.FullName;
                    var paramCount = handler.ParamTypeArray.Length;

                    var integrationType = GetIntegrationType(handler.ParamTypeArray[0], paramCount);
                    callTargetDefinitions = new NativeCallTargetDefinition[]
                    {
                        new(handler.Assembly, handler.FullType, handler.MethodName, handler.ParamTypeArray, 0, 0, 0, 65535, 65535, 65535, assemblyName, integrationType)
                    };

                    LifetimeManager.Instance.AddShutdownTask(RunShutdown);
                }

                return callTargetDefinitions;
            }
            catch (Exception ex)
            {
                Error("Impossible to get Serverless Definitions", ex);
                return Array.Empty<NativeCallTargetDefinition>();
            }
        }
    }
}
