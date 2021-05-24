// <copyright file="IntegrationInfo.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;

namespace Datadog.Trace.Configuration
{
    internal readonly struct IntegrationInfo
    {
        public readonly string Name;

        public readonly int Id;

        public IntegrationInfo(string integrationName)
        {
            if (integrationName == null)
            {
                throw new ArgumentNullException(nameof(integrationName));
            }

            Name = integrationName;
            Id = 0;
        }

        public IntegrationInfo(int integrationId)
        {
            Name = null;
            Id = integrationId;
        }
    }
}
