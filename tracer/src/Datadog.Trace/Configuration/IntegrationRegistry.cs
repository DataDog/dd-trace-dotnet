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
            var values = Enum.GetValues(typeof(IntegrationIds));
            var ids = new Dictionary<string, int>(values.Length);

            Names = new string[values.Cast<int>().Max() + 1];

            foreach (IntegrationIds value in values)
            {
                var name = value.ToString();

                Names[(int)value] = name;
                ids.Add(name, (int)value);
            }

            Ids = ids;
        }

        internal static string GetName(IntegrationInfo integration)
        {
            if (integration.Name == null)
            {
                return Names[integration.Id];
            }

            return integration.Name;
        }

        internal static IntegrationInfo GetIntegrationInfo(string integrationName)
        {
            if (Ids.TryGetValue(integrationName, out var id))
            {
                return new IntegrationInfo(id);
            }

            return new IntegrationInfo(integrationName);
        }
    }
}
