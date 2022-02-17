// <copyright file="BodyExtractor.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections;
using System.Collections.Generic;
using Datadog.Trace.AppSec.Waf;

namespace Datadog.Trace.AppSec
{
    internal class BodyExtractor
    {
        internal static IDictionary<string, object> GetKeysAndValues(object body)
        {
            var dic = new Dictionary<string, object>();
            ExtractProperties(dic, body);
            return dic;
        }

        private static void ExtractProperties(IDictionary<string, object> dic, object body, int depth = 0)
        {
            // todo: unit tests, perfs improvements and maybe skip to go from body > encoded waf arguments
            var properties = body.GetType().GetProperties();
            depth++;
            for (var i = 0; i < properties.Length; i++)
            {
                if (dic.Count >= WafConstants.MaxMapOrArrayLength)
                {
                    return;
                }

                var property = properties[i];
                var key = property.Name;
                var value = property.GetValue(body);
                if (property.PropertyType.IsArray || (typeof(IEnumerable).IsAssignableFrom(property.PropertyType) && property.PropertyType != typeof(string)))
                {
                    var j = 0;
                    var items = new Dictionary<string, object>();
                    foreach (var item in (IEnumerable)value)
                    {
                        var nestedDic = new Dictionary<string, object>();
                        ExtractProperties(nestedDic, item, depth);
                        items.Add(key + j++, nestedDic);
                    }

                    dic.Add(key, items);
                }
                else if (value is null || property.PropertyType.IsValueType || property.PropertyType == typeof(string))
                {
                    dic.Add(key, value?.ToString());
                }
                else
                {
                    if (depth >= WafConstants.MaxObjectDepth)
                    {
                        continue;
                    }

                    var nestedDic = new Dictionary<string, object>();
                    ExtractProperties(nestedDic, value, depth);
                    dic.Add(key, nestedDic);
                }
            }
        }
    }
}
