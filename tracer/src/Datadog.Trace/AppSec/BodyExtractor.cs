// <copyright file="BodyExtractor.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
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

        private static readonly HashSet<Type> AdditionalPrimatives = new()
        {
            typeof(string),
            typeof(decimal),
            typeof(Guid),
            typeof(DateTime),
            typeof(DateTimeOffset),
            typeof(TimeSpan)
        };

        internal static object GetKeysAndValues(object body)
        {
            var visted = new HashSet<object>();
            var item = ExtractType(body.GetType(), body, 0, visted);

            return item;
        }

        private static bool IsOurKindOfPrimitive(Type t)
        {
            return t.IsPrimitive || AdditionalPrimatives.Contains(t);
        }

        private static void ExtractProperties(Dictionary<string, object> dic, object body, int depth, HashSet<object> visited)
        {
            if (visited.Contains(body))
            {
                return;
            }

            visited.Add(body);

            var properties = body.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public);
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

                var item = ExtractType(property.PropertyType, value, depth, visited);
                dic.Add(key, item);
            }
        }

        private static object ExtractType(Type itemType, object value, int depth, HashSet<object> visited)
        {
            if (itemType.IsArray || (itemType.IsGenericType && itemType.GetGenericTypeDefinition() == typeof(List<>)))
            {
                var items = ExtractListOrArray(value, depth, visited);
                return items;
            }
            else if (itemType.IsGenericType && itemType.GetGenericTypeDefinition() == typeof(Dictionary<,>))
            {
                var items = ExtractDictionary(value, itemType, depth, visited);
                return items;
            }
            else if (value is null || IsOurKindOfPrimitive(itemType))
            {
                return value?.ToString();
            }
            else
            {
                var nestedDic = new Dictionary<string, object>();
                ExtractProperties(nestedDic, value, depth, visited);
                return nestedDic;
            }
        }

        private static Dictionary<string, object> ExtractDictionary(object value, Type dictType, int depth, HashSet<object> visited)
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
                if (dictValue is null || IsOurKindOfPrimitive(dictValue.GetType()))
                {
                    items.Add(dictKey, dictValue?.ToString());
                }
                else
                {
                    var nestedDic = new Dictionary<string, object>();
                    ExtractProperties(nestedDic, dictValue, depth, visited);
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

        private static List<object> ExtractListOrArray(object value, int depth, HashSet<object> visited)
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
                    ExtractProperties(nestedDic, item, depth, visited);
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
