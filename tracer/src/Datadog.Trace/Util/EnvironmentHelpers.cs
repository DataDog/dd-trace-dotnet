// <copyright file="EnvironmentHelpers.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using Datadog.Trace.ClrProfiler.ServerlessInstrumentation;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Util
{
    /// <summary>
    /// Helpers to access environment variables
    /// </summary>
    internal static class EnvironmentHelpers
    {
        // EnvironmentHelpers is called when initialising DataDogLogging.SharedLogger
        // Using Lazy<> here avoids setting the Logger field to the "null" logger, before initialization is complete
        private static readonly Lazy<IDatadogLogger> Logger = new Lazy<IDatadogLogger>(() => DatadogLogging.GetLoggerFor(typeof(EnvironmentHelpers)));

        /// <summary>
        /// Safe wrapper around Environment.SetEnvironmentVariable
        /// </summary>
        /// <param name="key">Name of the environment variable to set</param>
        /// <param name="value">Value to set</param>
        public static void SetEnvironmentVariable(string key, string? value)
        {
            try
            {
                Environment.SetEnvironmentVariable(key, value);
            }
            catch (Exception ex)
            {
                Logger.Value.Error(ex, "Error setting environment variable {EnvironmentVariable}={Value}", key, value);
            }
        }

        /// <summary>
        /// Safe wrapper around Environment.MachineName
        /// </summary>
        /// <returns>The value of <see cref="Environment.MachineName"/>, or null if an error occured</returns>
        public static string? GetMachineName()
        {
            try
            {
                return Environment.MachineName;
            }
            catch (Exception ex)
            {
                Logger.Value.Warning(ex, "Error while reading machine name");
            }

            return null;
        }

        /// <summary>
        /// Safe wrapper around Environment.GetEnvironmentVariable
        /// </summary>
        /// <param name="key">Name of the environment variable to fetch</param>
        /// <param name="defaultValue">Value to return in case of error</param>
        /// <returns>The value of the environment variable, or the default value if an error occured</returns>
        public static string? GetEnvironmentVariable(string key, string? defaultValue = null)
        {
            try
            {
                return Environment.GetEnvironmentVariable(key);
            }
            catch (Exception ex)
            {
                Logger.Value.Warning(ex, "Error while reading environment variable {EnvironmentVariable}", key);
            }

            return defaultValue;
        }

        /// <summary>
        /// Safe wrapper around Environment.GetEnvironmentVariables
        /// </summary>
        /// <returns>A dictionary that contains all environment variables, or en empty dictionary if an error occured</returns>
        public static IDictionary GetEnvironmentVariables()
        {
            try
            {
                return Environment.GetEnvironmentVariables();
            }
            catch (Exception ex)
            {
                Logger.Value.Warning(ex, "Error while reading environment variables");
            }

            return new Dictionary<object, object>();
        }

        /// <summary>
        /// Check if the current environment is Azure App Services
        /// by checking for the presence of "WEBSITE_SITE_NAME".
        /// Note that this is a superset of IsAzureFunctions().
        /// This method reads environment variables directly and bypasses the configuration system.
        /// </summary>
        public static bool IsAzureAppServices()
        {
            return EnvironmentVariableExists(PlatformKeys.AzureAppService.SiteNameKey);
        }

        /// <summary>
        /// Check if the current environment is Azure Functions
        /// by checking for the presence of "WEBSITE_SITE_NAME", "FUNCTIONS_WORKER_RUNTIME", and "FUNCTIONS_EXTENSION_VERSION".
        /// Note that his is a subset of IsAzureAppServices().
        /// This method reads environment variables directly and bypasses the configuration system.
        /// </summary>
        public static bool IsAzureFunctions()
        {
            return IsAzureAppServices() &&
                   EnvironmentVariableExists(PlatformKeys.AzureFunctions.FunctionsWorkerRuntime) &&
                   EnvironmentVariableExists(PlatformKeys.AzureFunctions.FunctionsExtensionVersion);
        }

        /// <summary>
        /// Check if the current environment is using Azure App Services Site Extension
        /// by checking for the presence of "DD_AZURE_APP_SERVICES=1".
        /// This method reads environment variables directly and bypasses the configuration system.
        /// </summary>
        public static bool IsUsingAzureAppServicesSiteExtension()
        {
            return GetEnvironmentVariable(ConfigurationKeys.AzureAppService.AzureAppServicesContextKey) == "1";
        }

        /// <summary>
        /// Check if the current environment is the Azure Functions host process
        /// by checking that:
        ///
        /// - <see cref="IsAzureFunctions"/> is <c>true</c>
        /// - "FUNCTIONS_WORKER_RUNTIME" is set to "dotnet-isolated"
        /// - we DO NOT see EITHER "--functions-worker-id" or "--workerId" on the command line as flags.
        /// The host and worker process will share the top two bullet points; however, only the worker process will have the flags
        /// Note that his is a subset of IsAzureFunctions().
        /// This method reads environment variables directly and bypasses the configuration system.
        /// </summary>
        public static bool IsRunningInAzureFunctionsHost()
        {
            var cmd = Environment.CommandLine ?? string.Empty;
            // heuristic to detect the worker process
            // the worker process would be the one to have these flags
            // example in log output
            // "CommandLine": "Samples.AzureFunctions.V4Isolated.AspNetCore.dll --workerId <GUID> --functions-worker-id <GUID>"
            var hasWorkerId = cmd.IndexOf("--functions-worker-id", StringComparison.OrdinalIgnoreCase) >= 0 ||
                              cmd.IndexOf("--workerId", StringComparison.OrdinalIgnoreCase) >= 0;

            return IsAzureFunctions()
                   && string.Equals(GetEnvironmentVariable(PlatformKeys.AzureFunctions.FunctionsWorkerRuntime, defaultValue: string.Empty), "dotnet-isolated", StringComparison.Ordinal)
                   && !hasWorkerId;
        }

        /// <summary>
        /// Check if the current environment is AWS Lambda
        /// by checking for the presence of "AWS_LAMBDA_FUNCTION_NAME".
        /// This method reads environment variables directly and bypasses the configuration system.
        /// </summary>
        public static bool IsAwsLambda()
        {
            return EnvironmentVariableExists(LambdaMetadata.FunctionNameEnvVar);
        }

        /// <summary>
        /// Check if the current environment is Google Cloud Functions
        /// by checking for the presence of either "K_SERVICE" and "FUNCTION_TARGET",
        /// or "FUNCTION_NAME" and "GCP_PROJECT".
        /// This method reads environment variables directly and bypasses the configuration system.
        /// </summary>
        public static bool IsGoogleCloudFunctions()
        {
            return (EnvironmentVariableExists(PlatformKeys.GcpFunction.FunctionNameKey) &&
                    EnvironmentVariableExists(PlatformKeys.GcpFunction.FunctionTargetKey)) ||
                   (EnvironmentVariableExists(PlatformKeys.GcpFunction.DeprecatedFunctionNameKey) &&
                    EnvironmentVariableExists(PlatformKeys.GcpFunction.DeprecatedProjectKey));
        }

        /// <summary>
        /// Checks if the specified environment variable exists in the current environment.
        /// </summary>
        private static bool EnvironmentVariableExists(string key) => !string.IsNullOrEmpty(GetEnvironmentVariable(key));
    }
}
