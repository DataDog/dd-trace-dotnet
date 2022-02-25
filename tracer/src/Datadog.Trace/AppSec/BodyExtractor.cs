// <copyright file="BodyExtractor.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections;
using System.Collections.Generic;
using Datadog.Trace.AppSec.Waf;
using Datadog.Trace.Logging;

namespace Datadog.Trace.AppSec
{
    internal static class BodyExtractor
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(BodyExtractor));

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
            Log.Debug("ExtractProperties - body: {Body}", body);

            for (var i = 0; i < properties.Length; i++)
            {
                if (dic.Count >= WafConstants.MaxMapOrArrayLength)
                {
                    return;
                }

                var property = properties[i];

                if (!property.CanRead || property.GetIndexParameters().Length > 0)
                {
                    continue;
                }

                var key = property.Name;
                var value = property.GetValue(body);

                Log.Debug("ExtractProperties - property: {Name} {Value}", property.Name, value);

                if (property.PropertyType.IsArray || (typeof(IEnumerable).IsAssignableFrom(property.PropertyType) && property.PropertyType != typeof(string)))
                {
                    var j = 0;
                    var items = new Dictionary<string, object>();
                    if (value is not null)
                    {
                        foreach (var item in (IEnumerable)value)
                        {
                            var nestedDic = new Dictionary<string, object>();
                            ExtractProperties(nestedDic, item, depth);
                            items.Add(key + j++, nestedDic);
                        }
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
