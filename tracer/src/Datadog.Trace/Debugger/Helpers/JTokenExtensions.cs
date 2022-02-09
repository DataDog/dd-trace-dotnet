// <copyright file="JTokenExtensions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.Debugger.Configurations.Models;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;

namespace Datadog.Trace.Debugger.Helpers
{
    internal static class JTokenExtensions
    {
        public static bool IsNullOrEmpty(this JToken token)
        {
            return token == null || !token.HasValues;
        }

        public static T ParseJsonApiObject<T>(this JToken token)
            where T : IJsonApiObject
        {
            var obj = token["attributes"].ToObject<T>();
            if (obj != null)
            {
                obj.Id = token["id"].Value<string>();
            }

            return obj;
        }

        public static T[] ParseJsonApiObjects<T>(this JToken token, string relationshipType, Dictionary<string, JToken> objectMap)
            where T : IJsonApiObject
        {
            return
                token["relationships"]?[relationshipType]?["data"]?
                   .Select(t => t["id"].Value<string>())
                   .Select(id => ParseJsonApiObject<T>(objectMap[id]))
                   .ToArray();
        }
    }
}
