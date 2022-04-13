// <copyright file="BodyExtractor.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using Datadog.Trace.AppSec.Waf;
using Datadog.Trace.Logging;

namespace Datadog.Trace.AppSec
{
    internal static class BodyExtractor
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(BodyExtractor));
        private static readonly IReadOnlyDictionary<string, object> EmptyDictionary = new ReadOnlyDictionary<string, object>(new Dictionary<string, object>(0));

        private static readonly HashSet<Type> AdditionalPrimitives = new()
        {
            typeof(string),
            typeof(decimal),
            typeof(Guid),
            typeof(DateTime),
            typeof(DateTimeOffset),
            typeof(TimeSpan)
        };

        internal static object Extract(object body)
        {
            var visted = new HashSet<object>();
            var item = ExtractType(body.GetType(), body, 0, visted);

            return item;
        }

        private static bool IsOurKindOfPrimitive(Type t)
        {
            return t.IsPrimitive || AdditionalPrimitives.Contains(t) || t.IsEnum;
        }

        private static IReadOnlyDictionary<string, object> ExtractProperties(object body, int depth, HashSet<object> visited)
        {
            if (visited.Contains(body))
            {
                return EmptyDictionary;
            }

            visited.Add(body);

            Log.Debug("ExtractProperties - body: {Body}", body);

            var fields = body.GetType()
                        .GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
                        .Where(x => x.IsPrivate && x.Name.EndsWith("__BackingField"))
                        .ToArray();

            depth++;

            var dictSize = Math.Min(WafConstants.MaxContainerSize, fields.Length);
            var dict = new Dictionary<string, object>(dictSize);

            for (var i = 0; i < fields.Length; i++)
            {
                if (dict.Count >= WafConstants.MaxContainerSize || depth >= WafConstants.MaxContainerDepth)
                {
                    return dict;
                }

                var field = fields[i];

                var propertyName = GetPropertyName(field.Name);
                if (string.IsNullOrEmpty(propertyName))
                {
                    Log.Warning("ExtractProperties - couldn't extract property name from: {FieldName}", field.Name);
                    continue;
                }

                var value = field.GetValue(body);

                Log.Debug("ExtractProperties - property: {Name} {Value}", propertyName, value);

                var item =
                    value == null ?
                        null :
                        ExtractType(field.FieldType, value, depth, visited);

                dict.Add(propertyName, item);
            }

            return dict;
        }

        private static string GetPropertyName(string fieldName)
        {
            if (fieldName[0] == '<')
            {
                var end = fieldName.IndexOf('>');

                return fieldName.Substring(1, end - 1);
            }

            return null;
        }

        private static object ExtractType(Type itemType, object value, int depth, HashSet<object> visited)
        {
            if (itemType.IsArray || (itemType.IsGenericType && itemType.GetGenericTypeDefinition() == typeof(List<>)))
            {
                return ExtractListOrArray(value, depth, visited);
            }
            else if (itemType.IsGenericType && itemType.GetGenericTypeDefinition() == typeof(Dictionary<,>))
            {
                return ExtractDictionary(value, itemType, depth, visited);
            }
            else if (IsOurKindOfPrimitive(itemType))
            {
                return value?.ToString();
            }
            else
            {
                var nestedDict = ExtractProperties(value, depth, visited);
                return nestedDict;
            }
        }

        private static Dictionary<string, object> ExtractDictionary(object value, Type dictType, int depth, HashSet<object> visited)
        {
            var gtkvp = typeof(KeyValuePair<,>);
            var tkvp = gtkvp.MakeGenericType(dictType.GetGenericArguments());
            var keyProp = tkvp.GetProperty("Key");
            var valueProp = tkvp.GetProperty("Value");

            var sourceDict = (ICollection)value;
            var dictSize = Math.Min(WafConstants.MaxContainerSize, sourceDict.Count);
            var items = new Dictionary<string, object>(dictSize);

            foreach (var item in sourceDict)
            {
                var dictKey = keyProp.GetValue(item)?.ToString();
                var dictValue = valueProp.GetValue(item);
                if (dictValue is null || IsOurKindOfPrimitive(dictValue.GetType()))
                {
                    items.Add(dictKey, dictValue?.ToString());
                }
                else
                {
                    var nestedDict = ExtractProperties(dictValue, depth, visited);
                    items.Add(dictKey, nestedDict);
                }

                if (items.Count >= WafConstants.MaxContainerSize)
                {
                    break;
                }
            }

            return items;
        }

        private static List<object> ExtractListOrArray(object value, int depth, HashSet<object> visited)
        {
            var sourceList = (ICollection)value;
            var listSize = Math.Min(WafConstants.MaxContainerSize, sourceList.Count);
            var items = new List<object>(listSize);

            foreach (var item in sourceList)
            {
                if (IsOurKindOfPrimitive(item.GetType()))
                {
                    items.Add(item);
                }
                else
                {
                    var nestedDict = ExtractProperties(item, depth, visited);
                    items.Add(nestedDict);
                }

                if (items.Count >= WafConstants.MaxContainerSize)
                {
                    break;
                }
            }

            return items;
        }
    }
}
