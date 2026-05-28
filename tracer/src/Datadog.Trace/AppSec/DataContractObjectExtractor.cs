// <copyright file="DataContractObjectExtractor.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Serialization;
using Datadog.Trace.AppSec.Waf;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.Serilog.Events;

namespace Datadog.Trace.AppSec
{
    internal static class DataContractObjectExtractor
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(DataContractObjectExtractor));
        private static readonly IReadOnlyDictionary<string, object?> EmptyDictionary = new ReadOnlyDictionary<string, object?>(new Dictionary<string, object?>(0));

        private static readonly ConcurrentDictionary<Type, MemberExtractor?[]> TypeToExtractorMap = new();

        private static readonly HashSet<Type> WafProcessableTypes =
        [
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
        ];

        internal static object? Extract(object? body)
        {
            if (body is null)
            {
                return null;
            }

            var visited = new HashSet<object>();
            return ExtractType(body.GetType(), body, 0, visited);
        }

        private static IReadOnlyDictionary<string, object?> ExtractProperties(object body, int depth, HashSet<object> visited)
        {
            try
            {
                if (!visited.Add(body))
                {
                    return EmptyDictionary;
                }
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

            if (!TypeToExtractorMap.TryGetValue(bodyType, out var memberExtractors))
            {
                memberExtractors = bodyType.GetCustomAttribute<DataContractAttribute>() is not null
                    ? CreateDataContractExtractors(bodyType)
                    : CreateDefaultExtractors(bodyType);

                TypeToExtractorMap.TryAdd(bodyType, memberExtractors);
            }

            var dictSize = Math.Min(WafConstants.MaxContainerSize, memberExtractors.Length);
            var dict = new Dictionary<string, object?>(dictSize);

            for (var i = 0; i < memberExtractors.Length; i++)
            {
                if (dict.Count >= WafConstants.MaxContainerSize || depth >= WafConstants.MaxContainerDepth)
                {
                    return dict;
                }

                var memberExtractor = memberExtractors[i];

                if (memberExtractor is not null)
                {
                    var value = memberExtractor.Accessor.Invoke(body);
                    if (Log.IsEnabled(LogEventLevel.Debug))
                    {
                        Log.Debug("ExtractProperties - property: {BodyType}.{Name} {Value}", bodyType.FullName, memberExtractor.Name, value);
                    }

                    var item = value is null ? null : ExtractType(memberExtractor.Type, value, depth, visited);
                    dict.Add(memberExtractor.Name, item);
                }
            }

            return dict;
        }

        private static MemberExtractor?[] CreateDataContractExtractors(Type bodyType)
        {
            var extractors = new List<MemberExtractor>();
            foreach (var property in bodyType.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (property.GetIndexParameters().Length > 0
                    || property.GetCustomAttribute<IgnoreDataMemberAttribute>() is not null
                    || property.GetCustomAttribute<DataMemberAttribute>() is not { } dataMember
                    || property.GetGetMethod(nonPublic: true) is not { } getter)
                {
                    continue;
                }

                var name = StringUtil.IsNullOrEmpty(dataMember.Name) ? property.Name : dataMember.Name;
                extractors.Add(new MemberExtractor(name!, property.PropertyType, CreatePropertyAccessor(bodyType, getter, property.PropertyType)));
            }

            foreach (var field in bodyType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (field.GetCustomAttribute<IgnoreDataMemberAttribute>() is not null
                    || field.GetCustomAttribute<DataMemberAttribute>() is not { } dataMember)
                {
                    continue;
                }

                var name = StringUtil.IsNullOrEmpty(dataMember.Name) ? GetPropertyName(field.Name) ?? field.Name : dataMember.Name;
                extractors.Add(new MemberExtractor(name!, field.FieldType, CreateFieldAccessor(bodyType, field)));
            }

            return extractors.ToArray();
        }

        private static MemberExtractor?[] CreateDefaultExtractors(Type bodyType)
        {
            var fields = bodyType.GetFields(BindingFlags.Instance | BindingFlags.NonPublic);
            if (!bodyType.GetTypeInfo().IsAnonymous())
            {
                fields = fields.Where(x => x.IsPrivate && x.Name.EndsWith("__BackingField")).ToArray();
            }

            var fieldExtractors = new MemberExtractor[fields.Length];
            for (var i = 0; i < fields.Length; i++)
            {
                var field = fields[i];
                var propertyName = GetPropertyName(field.Name);
                if (StringUtil.IsNullOrEmpty(propertyName))
                {
                    Log.Warning("ExtractProperties - couldn't extract property name from: {FieldName}", field.Name);
                    continue;
                }

                var property = bodyType.GetProperty(propertyName!, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (property?.GetCustomAttribute<IgnoreDataMemberAttribute>() is not null)
                {
                    continue;
                }

                var dataMemberName = property?.GetCustomAttribute<DataMemberAttribute>()?.Name;
                var name = StringUtil.IsNullOrEmpty(dataMemberName) ? propertyName : dataMemberName;
                fieldExtractors[i] = new MemberExtractor(name!, field.FieldType, CreateFieldAccessor(bodyType, field));
            }

            return fieldExtractors;
        }

        private static Func<object, object?> CreateFieldAccessor(Type bodyType, FieldInfo field)
        {
            var dynMethod = new DynamicMethod(
                bodyType + "_get_" + field.Name,
                typeof(object),
                [typeof(object)],
                typeof(DataContractObjectExtractor).Module,
                true);
            var ilGen = dynMethod.GetILGenerator();
            ilGen.Emit(OpCodes.Ldarg_0);
            if (bodyType.IsValueType)
            {
                ilGen.Emit(OpCodes.Unbox_Any, bodyType);
            }
            else
            {
                ilGen.Emit(OpCodes.Castclass, bodyType);
            }

            ilGen.Emit(OpCodes.Ldfld, field);
            if (field.FieldType.IsValueType)
            {
                ilGen.Emit(OpCodes.Box, field.FieldType);
            }

            ilGen.Emit(OpCodes.Ret);
            return (Func<object, object?>)dynMethod.CreateDelegate(typeof(Func<object, object?>));
        }

        private static Func<object, object?> CreatePropertyAccessor(Type bodyType, MethodInfo getter, Type propertyType)
        {
            var dynMethod = new DynamicMethod(
                bodyType + "_get_" + getter.Name,
                typeof(object),
                [typeof(object)],
                typeof(DataContractObjectExtractor).Module,
                true);
            var ilGen = dynMethod.GetILGenerator();
            ilGen.Emit(OpCodes.Ldarg_0);
            if (bodyType.IsValueType)
            {
                ilGen.Emit(OpCodes.Unbox, bodyType);
                ilGen.Emit(OpCodes.Call, getter);
            }
            else
            {
                ilGen.Emit(OpCodes.Castclass, bodyType);
                ilGen.Emit(getter.IsVirtual ? OpCodes.Callvirt : OpCodes.Call, getter);
            }

            if (propertyType.IsValueType)
            {
                ilGen.Emit(OpCodes.Box, propertyType);
            }

            ilGen.Emit(OpCodes.Ret);
            return (Func<object, object?>)dynMethod.CreateDelegate(typeof(Func<object, object?>));
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
                return value.ToString();
            }

            return ExtractProperties(value, depth, visited);
        }

        private static IReadOnlyDictionary<string, object?> ExtractDictionary(object value, Type dictType, int depth, HashSet<object> visited)
        {
            if (!visited.Add(value))
            {
                return EmptyDictionary;
            }

            var gtkvp = typeof(KeyValuePair<,>);
            var tkvp = gtkvp.MakeGenericType(dictType.GetGenericArguments());
            var keyProp = tkvp.GetProperty("Key");
            var valueProp = tkvp.GetProperty("Value");
            var sourceDict = value as IEnumerable;
            var items = value is ICollection sourceColl
                ? new Dictionary<string, object?>(Math.Min(WafConstants.MaxContainerSize, sourceColl.Count))
                : new Dictionary<string, object?>();

            if (sourceDict is not null && keyProp is not null && valueProp is not null)
            {
                foreach (var item in sourceDict)
                {
                    var dictKey = keyProp.GetValue(item)?.ToString();
                    var dictValue = valueProp.GetValue(item);
                    if (dictKey is not null)
                    {
                        items.Add(dictKey, dictValue is null ? null : ExtractType(dictValue.GetType(), dictValue, depth + 1, visited));
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
            if (visited.Contains(value))
            {
                return [];
            }

            var sourceList = (ICollection)value;
            var listSize = Math.Min(WafConstants.MaxContainerSize, sourceList.Count);
            var items = new List<object?>(listSize);

            foreach (var item in sourceList)
            {
                items.Add(item is null ? null : ExtractType(item.GetType(), item, depth + 1, visited));
                if (items.Count >= WafConstants.MaxContainerSize)
                {
                    break;
                }
            }

            return items;
        }

        private sealed class MemberExtractor(string name, Type type, Func<object, object?> accessor)
        {
            public string Name { get; } = name;

            public Type Type { get; } = type;

            public Func<object, object?> Accessor { get; } = accessor;
        }
    }
}
