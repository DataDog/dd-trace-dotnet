// <copyright file="ActivityMappingExtensions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;

namespace Datadog.Trace.Activity.Handlers
{
    internal static class ActivityMappingExtensions
    {
        public static void Deconstruct(this KeyValuePair<string, ActivityMapping> item, out string key, out ActivityMapping value)
        {
            key = item.Key;
            value = item.Value;
        }
    }
}
