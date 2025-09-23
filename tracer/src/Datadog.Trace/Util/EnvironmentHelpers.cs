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
using Datadog.Trace.Configuration.Telemetry;
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
        private static readonly Lazy<IDatadogLogger> Logger = new(() => DatadogLogging.GetLoggerFor(typeof(EnvironmentHelpers)));

        private static readonly ConfigurationBuilder EnvironmentBuilder = ConfigurationBuilder.FromEnvironmentSourceOnly();

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
// authorize for now, but needs to be removed
#pragma warning disable RS0030
                return Environment.GetEnvironmentVariable(key);
#pragma warning restore RS0030
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

        public static string? GetLogLevelName() => EnvironmentBuilder.WithKeys(ConfigurationKeys.LogLevel).AsString();

        /// <summary>
        /// Check if the current environment is Azure App Services
        /// by checking for the presence of "WEBSITE_SITE_NAME".
        /// Note that this is a superset of IsAzureFunctions().
        /// This method reads environment variables directly and bypasses the configuration system.
        /// </summary>
        public static bool IsAzureAppServices() => EnvironmentBuilder.WithKeys(PlatformKeys.AzureAppService.SiteNameKey).AsStringResult().ConfigurationResult.IsPresent;

        /// <summary>
        /// Check if the current environment is Azure Functions
        /// by checking for the presence of "WEBSITE_SITE_NAME", "FUNCTIONS_WORKER_RUNTIME", and "FUNCTIONS_EXTENSION_VERSION".
        /// Note that his is a subset of IsAzureAppServices().
        /// This method reads environment variables directly and bypasses the configuration system.
        /// </summary>
        public static bool IsAzureFunctions()
        {
            return IsAzureAppServices() &&
                   EnvironmentBuilder.WithKeys(PlatformKeys.AzureFunctions.FunctionsWorkerRuntime).AsStringResult().ConfigurationResult.IsPresent &&
                   EnvironmentBuilder.WithKeys(PlatformKeys.AzureFunctions.FunctionsExtensionVersion).AsStringResult().ConfigurationResult.IsPresent;
        }

        /// <summary>
        /// Check if the current environment is using Azure App Services Site Extension
        /// by checking for the presence of "DD_AZURE_APP_SERVICES=1".
        /// This method reads environment variables directly and bypasses the configuration system.
        /// </summary>
        public static bool IsUsingAzureAppServicesSiteExtension() => EnvironmentBuilder.WithKeys(ConfigurationKeys.AzureAppService.AzureAppServicesContextKey).AsString() == "1";

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
            return (EnvironmentBuilder.WithKeys(PlatformKeys.GCPFunction.FunctionNameKey).IsPresent &&
                    EnvironmentBuilder.WithKeys(PlatformKeys.GCPFunction.FunctionTargetKey).IsPresent) ||
                   (EnvironmentBuilder.WithKeys(PlatformKeys.GCPFunction.DeprecatedFunctionNameKey).IsPresent &&
                    EnvironmentBuilder.WithKeys(PlatformKeys.GCPFunction.DeprecatedProjectKey).IsPresent);
        }

        /// <summary>
        /// Checks if the specified environment variable exists in the current environment.
        /// </summary>
        private static bool EnvironmentVariableExists(string key) => !string.IsNullOrEmpty(GetEnvironmentVariable(key));
    }
}
