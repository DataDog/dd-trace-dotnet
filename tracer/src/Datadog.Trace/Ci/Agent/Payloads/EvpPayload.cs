// <copyright file="EvpPayload.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;

namespace Datadog.Trace.Ci.Agent.Payloads
{
    /// <summary>
    /// Event-platform payload
    /// </summary>
    internal abstract class EvpPayload
    {
        private Uri _url;

        /// <summary>
        /// Gets Event-Platform subdomain/track
        /// </summary>
        public abstract string EvpSubdomain { get; }

        /// <summary>
        /// Gets Event-Platform path
        /// </summary>
        public abstract string EvpPath { get; }

        /// <summary>
        /// Gets Payload url
        /// </summary>
        public Uri Url
        {
            get
            {
                if (_url is null)
                {
                    UriBuilder builder;
                    if (CIVisibility.Settings.Agentless)
                    {
                        var agentlessUrl = CIVisibility.Settings.AgentlessUrl;
                        if (!string.IsNullOrWhiteSpace(agentlessUrl))
                        {
                            builder = new UriBuilder(agentlessUrl);
                            builder.Path = EvpPath;
                        }
                        else
                        {
                            builder = new UriBuilder("https://datadog.host.com");
                            builder.Host = $"{EvpSubdomain}.{CIVisibility.Settings.Site}";
                            builder.Path = EvpPath;
                        }
                    }
                    else
                    {
                        // Use Agent EVP Proxy
                        builder = new UriBuilder(CIVisibility.Settings.TracerSettings.Exporter.AgentUri);
                        builder.Path = $"/evp_proxy/v1/{EvpPath}";
                    }

                    _url = builder.Uri;
                }

                return _url;
            }
        }
    }
}
