// <copyright file="IntegrationSettingsCollection.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.SourceGenerators;

namespace Datadog.Trace.Configuration
{
    /// <summary>
    /// A collection of <see cref="IntegrationSettings"/> instances, referenced by name.
    /// </summary>
    public sealed class IntegrationSettingsCollection
    {
        /// <summary>
        /// Gets the <see cref="IntegrationSettings"/> for the specified integration.
        /// </summary>
        /// <param name="integrationName">The name of the integration.</param>
        /// <returns>The integration-specific settings for the specified integration.</returns>
        [Instrumented]
        public IntegrationSettings this[string integrationName]
        {
            get
            {
                // Return a "null" version. The auto-instrumentation will overwrite with the "correct" values
                return new IntegrationSettings(integrationName, enabled: null, analyticsEnabled: false, analyticsSampleRate: 1.0);
            }
        }

        // The auto-instrumentation can call this method to create IntegrationSettings without needing
        // to use reverse duck typing
        [DuckTypeTarget]
        private IntegrationSettings CreateIntegrationSetting(string integrationName, bool? enabled, bool? analyticsEnabled, double analyticsSampleRate)
            => new(integrationName, enabled, analyticsEnabled, analyticsSampleRate);
    }
}
