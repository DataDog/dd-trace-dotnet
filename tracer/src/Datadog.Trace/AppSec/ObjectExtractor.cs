// <copyright file="ObjectExtractor.cs" company="Datadog">
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
    internal static class ObjectExtractor
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(ObjectExtractor));
        private static readonly IReadOnlyDictionary<string, object?> EmptyDictionary = new ReadOnlyDictionary<string, object?>(new Dictionary<string, object?>(0));

        private static readonly ConcurrentDictionary<Type, MemberExtractor?[]> TypeToExtractorMap = new();
        private static readonly ConcurrentDictionary<Type, MemberExtractor?[]> DataContractTypeToExtractorMap = new();

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
            => Extract(body, TypeToExtractorMap, CreateDefaultExtractors);

        internal static object? ExtractDataContract(object? body)
            => Extract(body, DataContractTypeToExtractorMap, CreateDataContractAwareExtractors);

        private static object? Extract(
            object? body,
            ConcurrentDictionary<Type, MemberExtractor?[]> extractorCache,
            Func<Type, MemberExtractor?[]> createExtractors)
        {
            if (body is null)
            {
                return null;
            }

            var visited = new HashSet<object>();
            var item = ExtractType(body.GetType(), body, 0, visited, extractorCache, createExtractors);
            return item;
        }

        private static IReadOnlyDictionary<string, object?> ExtractProperties(
            object body,
            int depth,
            HashSet<object> visited,
            ConcurrentDictionary<Type, MemberExtractor?[]> extractorCache,
            Func<Type, MemberExtractor?[]> createExtractors)
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

            if (!extractorCache.TryGetValue(bodyType, out var memberExtractors))
            {
                memberExtractors = createExtractors(bodyType);

                // this would be expected to fail sometimes, when several threads attempt process the same body
                extractorCache.TryAdd(bodyType, memberExtractors);
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

                    var item = value is null ? null : ExtractType(memberExtractor.Type, value, depth, visited, extractorCache, createExtractors);

                    // Use the indexer rather than Add: DataContract types can resolve two members
                    // to the same name (explicit [DataMember(Name = ...)], shadowed/inherited members),
                    // and Add would throw and abort the whole extraction. Last one wins, matching the serializer.
                    dict[memberExtractor.Name] = item;
                }
            }

            return dict;
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

                fieldExtractors[i] = new MemberExtractor(propertyName!, field.FieldType, CreateFieldAccessor(bodyType, field));
            }

            return fieldExtractors;
        }

        private static MemberExtractor?[] CreateDataContractAwareExtractors(Type bodyType)
        {
            return bodyType.GetCustomAttribute<DataContractAttribute>() is not null
                ? CreateDataContractExtractors(bodyType)
                : CreateDataContractDefaultExtractors(bodyType);
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

        private static MemberExtractor?[] CreateDataContractDefaultExtractors(Type bodyType)
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
                typeof(ObjectExtractor).Module,
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
                typeof(ObjectExtractor).Module,
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

        private static object? ExtractType(
            Type itemType,
            object value,
            int depth,
            HashSet<object> visited,
            ConcurrentDictionary<Type, MemberExtractor?[]> extractorCache,
            Func<Type, MemberExtractor?[]> createExtractors)
        {
            if (itemType == typeof(string))
            {
                return value;
            }

            if (itemType.IsArray || (itemType.IsGenericType && itemType.GetGenericTypeDefinition() == typeof(List<>)))
            {
                return ExtractListOrArray(value, depth, visited, extractorCache, createExtractors);
            }

            if (itemType.IsGenericType && itemType.GetGenericTypeDefinition() == typeof(Dictionary<,>))
            {
                return ExtractDictionary(value, itemType, depth, visited, extractorCache, createExtractors);
            }

            // case of System.Web.Routing.RouteValueDictionary and types inheriting IDictionary
            var iDictionaryType = typeof(IDictionary<string, object>);
            if (iDictionaryType.IsAssignableFrom(itemType))
            {
                return ExtractDictionary(value, iDictionaryType, depth, visited, extractorCache, createExtractors);
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

            var nestedDict = ExtractProperties(value, depth, visited, extractorCache, createExtractors);
            return nestedDict;
        }

        private static IReadOnlyDictionary<string, object?> ExtractDictionary(
            object value,
            Type dictType,
            int depth,
            HashSet<object> visited,
            ConcurrentDictionary<Type, MemberExtractor?[]> extractorCache,
            Func<Type, MemberExtractor?[]> createExtractors)
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
                            var extractedvalue = ExtractType(dictValue.GetType(), dictValue, depth + 1, visited, extractorCache, createExtractors);
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

        private static List<object?> ExtractListOrArray(
            object value,
            int depth,
            HashSet<object> visited,
            ConcurrentDictionary<Type, MemberExtractor?[]> extractorCache,
            Func<Type, MemberExtractor?[]> createExtractors)
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
                if (item is null)
                {
                    items.Add(item);
                }
                else
                {
                    var extractedvalue = ExtractType(item.GetType(), item, depth + 1, visited, extractorCache, createExtractors);
                    items.Add(extractedvalue);
                }

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
