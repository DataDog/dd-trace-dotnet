// <copyright file="BodyExtractor.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections;
using System.Collections.Generic;
using System.Reflection;
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

                if (property.PropertyType.IsArray || (property.PropertyType.IsGenericType && property.PropertyType.GetGenericTypeDefinition() == typeof(List<>)))
                {
                    ExtractListOrArray(dic, depth, key, value);
                }
                else if (property.PropertyType.IsGenericType && property.PropertyType.GetGenericTypeDefinition() == typeof(Dictionary<,>))
                {
                    ExtractDictionary(dic, depth, property, key, value);
                }
                else if (value is null || property.PropertyType.IsPrimitive || property.PropertyType == typeof(string))
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

        private static void ExtractDictionary(IDictionary<string, object> dic, int depth, PropertyInfo property, string key, object value)
        {
            var items = new Dictionary<string, object>();
            var gtkvp = typeof(KeyValuePair<,>);
            var tkvp = gtkvp.MakeGenericType(property.PropertyType.GetGenericArguments());
            var keyProp = tkvp.GetProperty("Key");
            var valueProp = tkvp.GetProperty("Value");

            var i = 0;
            foreach (var item in (IEnumerable)value)
            {
                var dictKey = keyProp.GetValue(item)?.ToString();
                var dictValue = valueProp.GetValue(item);
                if (dictValue.GetType().IsPrimitive || dictValue is string)
                {
                    items.Add(dictKey, dictValue);
                }
                else
                {
                    var nestedDic = new Dictionary<string, object>();
                    ExtractProperties(nestedDic, dictValue, depth);
                    items.Add(dictKey, nestedDic);
                }

                i++;
                if (i >= WafConstants.MaxMapOrArrayLength)
                {
                    break;
                }
            }

            dic.Add(key, items);
        }

        private static void ExtractListOrArray(IDictionary<string, object> dic, int depth, string key, object value)
        {
            var items = new List<object>();

            var i = 0;
            foreach (var item in (IEnumerable)value)
            {
                if (item.GetType().IsPrimitive || item is string)
                {
                    items.Add(item);
                }
                else
                {
                    var nestedDic = new Dictionary<string, object>();
                    ExtractProperties(nestedDic, item, depth);
                    items.Add(nestedDic);
                }

                i++;
                if (i >= WafConstants.MaxMapOrArrayLength)
                {
                    break;
                }
            }

            dic.Add(key, items);
        }
    }
}
