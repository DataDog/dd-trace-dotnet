// <copyright file="EventPlatformPayload.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Ci.Configuration;
using Datadog.Trace.Util;

namespace Datadog.Trace.Ci.Agent.Payloads
{
    /// <summary>
    /// Event-platform payload
    /// </summary>
    internal abstract class EventPlatformPayload
    {
        private readonly CIVisibilitySettings _settings;
        private Uri _url;

        protected EventPlatformPayload(CIVisibilitySettings settings)
        {
            if (settings is null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(settings));
            }

            _settings = settings;
            UseEvpProxy = !settings.Agentless;
        }

        /// <summary>
        /// Gets Event-Platform subdomain/track
        /// </summary>
        public abstract string EventPlatformSubdomain { get; }

        /// <summary>
        /// Gets Event-Platform path
        /// </summary>
        public abstract string EventPlatformPath { get; }

        /// <summary>
        /// Gets or sets the Payload url
        /// </summary>
        public Uri Url
        {
            get
            {
                if (_url is { } url)
                {
                    return url;
                }

                UriBuilder builder;
                if (_settings.Agentless)
                {
                    var agentlessUrl = _settings.AgentlessUrl;
                    if (!string.IsNullOrWhiteSpace(agentlessUrl))
                    {
                        builder = new UriBuilder(agentlessUrl);
                        builder.Path = EventPlatformPath;
                    }
                    else
                    {
                        builder = new UriBuilder(
                            scheme: "https",
                            host: $"{EventPlatformSubdomain}.{_settings.Site}",
                            port: 443,
                            pathValue: EventPlatformPath);
                    }
                }
                else
                {
                    // Use Agent EVP Proxy
                    builder = new UriBuilder(_settings.TracerSettings.ExporterInternal.AgentUri);
                    builder.Path = $"/evp_proxy/v2/{EventPlatformPath}";
                }

                url = builder.Uri;
                _url = url;
                return url;
            }

            set
            {
                _url = value;
            }
        }

        /// <summary>
        /// Gets a value indicating whether the payload is configured to use the agent proxy
        /// </summary>
        public virtual bool UseEvpProxy { get; }

        /// <summary>
        /// Gets a value indicating whether the payload contains events or not
        /// </summary>
        public abstract bool HasEvents { get; }

        /// <summary>
        /// Gets the number of events in the payload
        /// </summary>
        public abstract int Count { get; }

        /// <summary>
        /// Checks if the payload can process a given event
        /// </summary>
        /// <param name="event">Event to be processed</param>
        /// <returns>True if the event can be processed by the payload instance; otherwise false.</returns>
        public abstract bool CanProcessEvent(IEvent @event);

        /// <summary>
        /// Try to process an event
        /// </summary>
        /// <param name="event">Event to be processed</param>
        /// <returns>True if the event has been processed by the payload instance; otherwise false.</returns>
        public abstract bool TryProcessEvent(IEvent @event);

        /// <summary>
        /// Resets payload buffer
        /// </summary>
        public abstract void Reset();
    }
}
