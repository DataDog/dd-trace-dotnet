// <copyright file="ObjectExtractor.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using Datadog.Trace.AppSec.Waf;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.Serilog.Events;

#nullable enable

namespace Datadog.Trace.AppSec
{
    internal static class ObjectExtractor
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(ObjectExtractor));
        private static readonly IReadOnlyDictionary<string, object?> EmptyDictionary = new ReadOnlyDictionary<string, object?>(new Dictionary<string, object?>(0));

        private static readonly ConcurrentDictionary<Type, FieldExtractor[]> TypeToExtractorMap = new();

        private static readonly HashSet<Type> WafProcessableTypes = new()
        {
            typeof(float),
            typeof(double),
            typeof(decimal),
            typeof(short),
            typeof(ushort),
            typeof(int),
            typeof(uint),
            typeof(long),
            typeof(bool),
            typeof(byte),
            typeof(ulong)
        };

        internal static object? Extract(object? body)
        {
            if (body == null)
            {
                return null;
            }

            var visited = new HashSet<object>();
            var item = ExtractType(body.GetType(), body, 0, visited);
            return item;
        }

        private static IReadOnlyDictionary<string, object?> ExtractProperties(object body, int depth, HashSet<object> visited)
        {
            try
            {
                if (visited.Contains(body))
                {
                    return EmptyDictionary;
                }

                visited.Add(body);
            }
            catch
            {
                // Contains and Add call GetHashCode which can throw an exception if has a custom implementation
                // If visited is empty, we could potentially get the exception only when calling Add
                return EmptyDictionary;
            }

            if (Log.IsEnabled(LogEventLevel.Debug))
            {
                Log.Debug("ExtractProperties - body: {Body}", body);
            }

            var bodyType = body.GetType();

            depth++;

            if (!TypeToExtractorMap.TryGetValue(bodyType, out var fieldExtractors))
            {
                var fields = bodyType.GetFields(BindingFlags.Instance | BindingFlags.NonPublic);
                if (!bodyType.GetTypeInfo().IsAnonymous())
                {
                    fields = fields.Where(x => x.IsPrivate && x.Name.EndsWith("__BackingField")).ToArray();
                }

                fieldExtractors = new FieldExtractor[fields.Length];
                for (var i = 0; i < fields.Length; i++)
                {
                    var field = fields[i];

                    var propertyName = GetPropertyName(field.Name);
                    if (string.IsNullOrEmpty(propertyName))
                    {
                        Log.Warning("ExtractProperties - couldn't extract property name from: {FieldName}", field.Name);
                        continue;
                    }

                    var dynMethod = new DynamicMethod(
                        bodyType + "_get_" + propertyName,
                        typeof(object),
                        new[] { typeof(object) },
                        typeof(ObjectExtractor).Module,
                        true);
                    var ilGen = dynMethod.GetILGenerator();
                    ilGen.Emit(OpCodes.Ldarg_0);
                    if (bodyType.IsValueType)
                    {
                        ilGen.Emit(OpCodes.Unbox_Any, bodyType);
                    }

                    ilGen.Emit(OpCodes.Ldfld, field);
                    if (field.FieldType.IsValueType)
                    {
                        ilGen.Emit(OpCodes.Box, field.FieldType);
                    }

                    ilGen.Emit(OpCodes.Ret);
                    var func = (Func<object, object>)dynMethod.CreateDelegate(typeof(Func<object, object>));

                    fieldExtractors[i] = new FieldExtractor(propertyName!, field.FieldType, func);
                }

                // this would be expect to fail sometimes, when several threads attempt process the same body
                TypeToExtractorMap.TryAdd(bodyType, fieldExtractors);
            }

            var dictSize = Math.Min(WafConstants.MaxContainerSize, fieldExtractors.Length);
            var dict = new Dictionary<string, object?>(dictSize);

            for (var i = 0; i < fieldExtractors.Length; i++)
            {
                if (dict.Count >= WafConstants.MaxContainerSize || depth >= WafConstants.MaxContainerDepth)
                {
                    return dict;
                }

                var fieldExtractor = fieldExtractors[i];

                if (fieldExtractor != null)
                {
                    var value = fieldExtractor.Accessor.Invoke(body);
                    if (Log.IsEnabled(LogEventLevel.Debug))
                    {
                        Log.Debug("ExtractProperties - property: {BodyType}.{Name} {Value}", bodyType.FullName, fieldExtractor.Name, value);
                    }

                    var item =
                        value == null ? null : ExtractType(fieldExtractor.Type, value, depth, visited);

                    dict.Add(fieldExtractor.Name, item);
                }
            }

            return dict;
        }

        private static string? GetPropertyName(string fieldName)
        {
            if (fieldName[0] == '<')
            {
                var end = fieldName.IndexOf('>');

                return fieldName.Substring(1, end - 1);
            }

            return null;
        }

        private static object? ExtractType(Type itemType, object value, int depth, HashSet<object> visited)
        {
            if (itemType == typeof(string))
            {
                return value;
            }

            if (itemType.IsArray || (itemType.IsGenericType && itemType.GetGenericTypeDefinition() == typeof(List<>)))
            {
                return ExtractListOrArray(value, depth, visited);
            }

            if (itemType.IsGenericType && itemType.GetGenericTypeDefinition() == typeof(Dictionary<,>))
            {
                return ExtractDictionary(value, itemType, depth, visited);
            }

            // case of System.Web.Routing.RouteValueDictionary and types inheriting IDictionary
            var iDictionaryType = typeof(IDictionary<string, object>);
            if (iDictionaryType.IsAssignableFrom(itemType))
            {
                return ExtractDictionary(value, iDictionaryType, depth, visited);
            }

            if (WafProcessableTypes.Contains(itemType))
            {
                return value;
            }

            var unhandledType = itemType.IsEnum || itemType == typeof(Guid) || itemType == typeof(DateTime) || itemType == typeof(DateTimeOffset) || itemType == typeof(TimeSpan) || itemType.IsPrimitive;
#if NET6_0_OR_GREATER
            unhandledType = unhandledType || itemType == typeof(DateOnly) || itemType == typeof(TimeOnly);
#endif
            if (unhandledType)
            {
                return value?.ToString();
            }

            var nestedDict = ExtractProperties(value, depth, visited);
            return nestedDict;
        }

        private static Dictionary<string, object?> ExtractDictionary(object value, Type dictType, int depth, HashSet<object> visited)
        {
            var gtkvp = typeof(KeyValuePair<,>);
            var tkvp = gtkvp.MakeGenericType(dictType.GetGenericArguments());
            var keyProp = tkvp.GetProperty("Key");
            var valueProp = tkvp.GetProperty("Value");

            var sourceDict = value as IEnumerable;

            Dictionary<string, object?> items;
            // some types, like System.Web.Routing.RouteValueDictionary don't inherit ICollection, but ICollection<,> which never inherits ICollection but IEnumerable instead.
            if (value is ICollection sourceColl)
            {
                var dictSize = Math.Min(WafConstants.MaxContainerSize, sourceColl.Count);
                items = new Dictionary<string, object?>(dictSize);
            }
            else
            {
                items = new Dictionary<string, object?>();
            }

            if (sourceDict is not null && keyProp is not null && valueProp is not null)
            {
                foreach (var item in sourceDict)
                {
                    var dictKey = keyProp.GetValue(item)?.ToString();
                    var dictValue = valueProp.GetValue(item);

                    if (dictKey is not null)
                    {
                        if (dictValue is null)
                        {
                            items.Add(dictKey, null);
                        }
                        else
                        {
                            var extractedvalue = ExtractType(dictValue.GetType(), dictValue, depth + 1, visited);
                            items.Add(dictKey, extractedvalue);
                        }

                        if (items.Count >= WafConstants.MaxContainerSize)
                        {
                            break;
                        }
                    }
                }
            }

            return items;
        }

        private static List<object?> ExtractListOrArray(object value, int depth, HashSet<object> visited)
        {
            var sourceList = (ICollection)value;
            var listSize = Math.Min(WafConstants.MaxContainerSize, sourceList.Count);
            var items = new List<object?>(listSize);

            foreach (var item in sourceList)
            {
                if (item is null)
                {
                    items.Add(item);
                }
                else
                {
                    var extractedvalue = ExtractType(item.GetType(), item, depth + 1, visited);
                    items.Add(extractedvalue);
                }

                if (items.Count >= WafConstants.MaxContainerSize)
                {
                    break;
                }
            }

            return items;
        }

        private class FieldExtractor
        {
            public FieldExtractor(string name, Type type, Func<object, object> accessor)
            {
                Name = name;
                Type = type;
                Accessor = accessor;
            }

            public string Name { get; set; }

            public Type Type { get; set; }

            public Func<object, object> Accessor { get; set; }
        }
    }
}
