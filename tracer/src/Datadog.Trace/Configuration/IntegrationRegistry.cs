// <copyright file="IntegrationRegistry.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;

namespace Datadog.Trace.Configuration
{
    internal static class IntegrationRegistry
    {
        internal static readonly string[] Names;

        internal static readonly IReadOnlyDictionary<string, int> Ids;

        static IntegrationRegistry()
        {
            var values = Enum.GetValues(typeof(IntegrationId));
            var ids = new Dictionary<string, int>(values.Length, StringComparer.OrdinalIgnoreCase);

            Names = new string[values.Cast<int>().Max() + 1];

            foreach (IntegrationId value in values)
            {
                var name = value.ToString();

                Names[(int)value] = name;
                ids.Add(name, (int)value);
            }

            Ids = ids;
        }

        internal static string GetName(IntegrationId integration)
            => Names[(int)integration];

        internal static bool TryGetIntegrationId(string integrationName, out IntegrationId integration)
        {
            if (Ids.TryGetValue(integrationName, out var id))
            {
                integration = (IntegrationId)id;
                return true;
            }

            integration = default;
            return false;
        }
    }
}
