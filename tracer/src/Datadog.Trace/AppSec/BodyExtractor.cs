// <copyright file="BodyExtractor.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Datadog.Trace.AppSec.Waf;
using Datadog.Trace.Logging;

namespace Datadog.Trace.AppSec
{
    internal static class BodyExtractor
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(BodyExtractor));

        private static readonly Regex NameExtractor = new Regex(@"\<(?'PropertyName'[^\>]+)\>", RegexOptions.Compiled);

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

            Log.Debug("ExtractProperties - body: {Body}", body);

            var fields =
                    body.GetType()
                        .GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
                        .Where(x => x.IsPrivate && x.Name.EndsWith("__BackingField"))
                        .ToArray();

            depth++;

            for (var i = 0; i < fields.Length; i++)
            {
                if (dic.Count >= WafConstants.MaxMapOrArrayLength || depth >= WafConstants.MaxObjectDepth)
                {
                    return;
                }

                var field = fields[i];

                var nameMatch = NameExtractor.Match(field.Name);
                if (!nameMatch.Success || nameMatch.Groups.Count == 0)
                {
                    throw new Exception("Can't extract property name from " + field.Name);
                }

                var key = nameMatch.Groups["PropertyName"].Value;
                var value = field.GetValue(body);

                Log.Debug("ExtractProperties - property: {Name} {Value}", key, value);

                var item = ExtractType(field.FieldType, value, depth, visited);
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
