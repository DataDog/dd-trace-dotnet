// <copyright file="ConfigurationKeys.GCPFunction.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.Configuration
{
    internal partial class ConfigurationKeys
    {
        // GCP Function auto-set env var reference: https://cloud.google.com/functions/docs/configuring/env-var#runtime_environment_variables_set_automatically
        internal class GCPFunction
        {
            /// <summary>
            /// The name of functions running deprecated runtimes.
            /// </summary>
            internal const string DeprecatedFunctionNameKey = "FUNCTION_NAME";

            /// <summary>
            /// The name of the gcp project a deprecated function belongs to. Only set in functions running deprecated runtimes.
            /// Used to tell whether or not we are in a deprecated function environment.
            /// </summary>
            internal const string DeprecatedProjectKey = "GCP_PROJECT";

            /// <summary>
            /// The name of functions running non-deprecated runtimes.
            /// </summary>
            internal const string FunctionNameKey = "K_SERVICE";

            /// <summary>
            /// The name of the function handler to be executed when the function is invoked. Only set in functions running non-deprecated runtimes.
            /// Used to tell whether or not we are in a non-deprecated function environment.
            /// </summary>
            internal const string FunctionTargetKey = "FUNCTION_TARGET";
        }
    }
}
