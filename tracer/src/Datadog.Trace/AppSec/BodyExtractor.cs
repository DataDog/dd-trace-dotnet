// <copyright file="BodyExtractor.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections;
using System.Collections.Generic;
using Datadog.Trace.AppSec.Waf;
using Datadog.Trace.Logging;

namespace Datadog.Trace.AppSec
{
    internal static class BodyExtractor
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(BodyExtractor));

        internal static object GetKeysAndValues(object body)
        {
            var item = ExtractType(body.GetType(), body, 0);

            return item;
        }

        private static void ExtractProperties(IDictionary<string, object> dic, object body, int depth)
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

                if (depth >= WafConstants.MaxObjectDepth)
                {
                    continue;
                }

                var item = ExtractType(property.PropertyType, value, depth);
                dic.Add(key, item);
            }
        }

        private static object ExtractType(Type itemType, object value, int depth)
        {
            if (itemType.IsArray || (itemType.IsGenericType && itemType.GetGenericTypeDefinition() == typeof(List<>)))
            {
                var items = ExtractListOrArray(value, depth);
                return items;
            }
            else if (itemType.IsGenericType && itemType.GetGenericTypeDefinition() == typeof(Dictionary<,>))
            {
                var items = ExtractDictionary(value, itemType, depth);
                return items;
            }
            else if (value is null || itemType.IsPrimitive || itemType == typeof(string))
            {
                return value?.ToString();
            }
            else
            {
                var nestedDic = new Dictionary<string, object>();
                ExtractProperties(nestedDic, value, depth);
                return nestedDic;
            }
        }

        private static Dictionary<string, object> ExtractDictionary(object value, Type dictType, int depth)
        {
            var items = new Dictionary<string, object>();
            var gtkvp = typeof(KeyValuePair<,>);
            var tkvp = gtkvp.MakeGenericType(dictType.GetGenericArguments());
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

            return items;
        }

        private static List<object> ExtractListOrArray(object value, int depth)
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

            return items;
        }
    }
}
