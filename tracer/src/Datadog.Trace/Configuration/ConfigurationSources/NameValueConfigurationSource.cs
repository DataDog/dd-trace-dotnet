// <copyright file="NameValueConfigurationSource.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Specialized;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.SourceGenerators;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Metrics;

namespace Datadog.Trace.Configuration
{
    /// <summary>
    /// Represents a configuration source that retrieves
    /// values from the provided <see cref="NameValueCollection"/>.
    /// </summary>
    public class NameValueConfigurationSource : StringConfigurationSource
    {
        private readonly NameValueCollection _nameValueCollection;

        /// <summary>
        /// Initializes a new instance of the <see cref="NameValueConfigurationSource"/> class
        /// that wraps the specified <see cref="NameValueCollection"/>.
        /// </summary>
        /// <param name="nameValueCollection">The collection that will be wrapped by this configuration source.</param>
        [PublicApi]
        public NameValueConfigurationSource(NameValueCollection nameValueCollection)
            : this(nameValueCollection, ConfigurationOrigins.Code)
        {
            TelemetryFactory.Metrics.Record(PublicApiUsage.NameValueConfigurationSource_Ctor);
        }

        internal NameValueConfigurationSource(NameValueCollection nameValueCollection, ConfigurationOrigins origin)
        {
            _nameValueCollection = nameValueCollection;
            Origin = origin;
        }

        internal override ConfigurationOrigins Origin { get; }

        /// <inheritdoc />
        [PublicApi]
        public override string? GetString(string key)
        {
            return _nameValueCollection[key];
        }
    }
}
