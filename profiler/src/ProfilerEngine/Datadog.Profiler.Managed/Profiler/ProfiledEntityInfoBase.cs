// <copyright file="ProfiledEntityInfoBase.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;

namespace Datadog.Profiler
{
    public class ProfiledEntityInfoBase
    {
        public ProfiledEntityInfoBase(int providerSessionId)
        {
            ProviderSessionId = providerSessionId;
            IsEntityActive = true;
            IsEntityStale = false;
        }

        /// <summary>
        /// Gets or sets the session ID the this entity was updated last
        /// </summary>
        public int ProviderSessionId { get; protected set; }

        /// <summary>
        /// Gets or sets a value indicating whether or not the underlying profiled entity represented by this instance is still active in the runtime.
        /// An entity that is inactive during a compaction phase becomes stale.
        /// </summary>
        public bool IsEntityActive { get; protected set; }

        /// <summary>
        /// Gets or sets a value indicating whether or not the underlying profiled entity represented by this intanance is eligible for being dropped
        /// from cache during the next compaction phase.
        /// An entity becomes stale it is inactive during a cache compaction phase and will thus be dropped during the
        /// subsequent compaction phase.
        /// </summary>
        public bool IsEntityStale { get; protected set; }

        internal void SetEntityInactive(int providerSessionId)
        {
            ProviderSessionId = providerSessionId;
            IsEntityActive = false;
        }

        internal void SetEntityStale(int providerSessionId)
        {
            ProviderSessionId = providerSessionId;
            IsEntityActive = false;
            IsEntityStale = true;
        }
    }
}