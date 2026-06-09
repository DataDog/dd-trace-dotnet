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
            => Extract(body, TypeToExtractorMap, CreateDefaultExtractors, useSimpleDictionaryFormat: true);

        internal static object? ExtractDataContract(object? body, bool useSimpleDictionaryFormat = false)
            => Extract(body, DataContractTypeToExtractorMap, CreateDataContractAwareExtractors, useSimpleDictionaryFormat);

        private static object? Extract(
            object? body,
            ConcurrentDictionary<Type, MemberExtractor?[]> extractorCache,
            Func<Type, MemberExtractor?[]> createExtractors,
            bool useSimpleDictionaryFormat = false)
        {
            if (body is null)
            {
                return null;
            }

            var visited = new HashSet<object>();
            var item = ExtractType(body.GetType(), body, 0, visited, extractorCache, createExtractors, useSimpleDictionaryFormat);
            return item;
        }

        private static IReadOnlyDictionary<string, object?> ExtractProperties(
            object body,
            int depth,
            HashSet<object> visited,
            ConcurrentDictionary<Type, MemberExtractor?[]> extractorCache,
            Func<Type, MemberExtractor?[]> createExtractors,
            bool useSimpleDictionaryFormat)
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

                    // [DataMember(EmitDefaultValue = false)] means the serializer omits this member
                    // when its value is null/default — skip it to match the actual response body.
                    if (!memberExtractor.EmitDefaultValue && memberExtractor.IsDefault(value))
                    {
                        continue;
                    }

                    if (Log.IsEnabled(LogEventLevel.Debug))
                    {
                        Log.Debug("ExtractProperties - property: {BodyType}.{Name} {Value}", bodyType.FullName, memberExtractor.Name, value);
                    }

                    // Use the runtime type so polymorphic members (declared as object/interface/base)
                    // are extracted correctly, matching what the serializer actually wrote.
                    var item = value is null ? null : ExtractType(value.GetType(), value, depth, visited, extractorCache, createExtractors, useSimpleDictionaryFormat);

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

            // GetProperties/GetFields without DeclaredOnly returns inherited public/protected members
            // but silently drops private members from base types — we handle those below.
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
                extractors.Add(new MemberExtractor(name!, property.PropertyType, CreatePropertyAccessor(bodyType, getter, property.PropertyType), dataMember.EmitDefaultValue));
            }

            foreach (var field in bodyType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (field.GetCustomAttribute<IgnoreDataMemberAttribute>() is not null
                    || field.GetCustomAttribute<DataMemberAttribute>() is not { } dataMember)
                {
                    continue;
                }

                var name = StringUtil.IsNullOrEmpty(dataMember.Name) ? GetPropertyName(field.Name) ?? field.Name : dataMember.Name;
                extractors.Add(new MemberExtractor(name!, field.FieldType, CreateFieldAccessor(bodyType, field), dataMember.EmitDefaultValue));
            }

            // DataContractSerializer includes private [DataMember] members from base [DataContract] types.
            // Those are invisible to the non-DeclaredOnly queries above, so walk the hierarchy explicitly.
            for (var type = bodyType.BaseType; type is not null && type != typeof(object); type = type.BaseType)
            {
                if (type.GetCustomAttribute<DataContractAttribute>() is null)
                {
                    break;
                }

                foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    var getter = property.GetGetMethod(nonPublic: true);
                    if (getter?.IsPrivate is not true
                        || property.GetIndexParameters().Length > 0
                        || property.GetCustomAttribute<IgnoreDataMemberAttribute>() is not null
                        || property.GetCustomAttribute<DataMemberAttribute>() is not { } dataMember)
                    {
                        continue;
                    }

                    var name = StringUtil.IsNullOrEmpty(dataMember.Name) ? property.Name : dataMember.Name;
                    extractors.Add(new MemberExtractor(name!, property.PropertyType, CreatePropertyAccessor(bodyType, getter, property.PropertyType), dataMember.EmitDefaultValue));
                }

                foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (!field.IsPrivate
                        || field.GetCustomAttribute<IgnoreDataMemberAttribute>() is not null
                        || field.GetCustomAttribute<DataMemberAttribute>() is not { } dataMember)
                    {
                        continue;
                    }

                    var name = StringUtil.IsNullOrEmpty(dataMember.Name) ? GetPropertyName(field.Name) ?? field.Name : dataMember.Name;
                    extractors.Add(new MemberExtractor(name!, field.FieldType, CreateFieldAccessor(bodyType, field), dataMember.EmitDefaultValue));
                }
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

            var extractors = new List<MemberExtractor?>(fields.Length);
            for (var i = 0; i < fields.Length; i++)
            {
                var field = fields[i];
                var propertyName = GetPropertyName(field.Name);
                if (StringUtil.IsNullOrEmpty(propertyName))
                {
                    Log.Warning("ExtractProperties - couldn't extract property name from: {FieldName}", field.Name);
                    extractors.Add(null);
                    continue;
                }

                var property = bodyType.GetProperty(propertyName!, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (property?.GetCustomAttribute<IgnoreDataMemberAttribute>() is not null)
                {
                    extractors.Add(null);
                    continue;
                }

                var dataMemberName = property?.GetCustomAttribute<DataMemberAttribute>()?.Name;
                var name = StringUtil.IsNullOrEmpty(dataMemberName) ? propertyName : dataMemberName;
                extractors.Add(new MemberExtractor(name!, field.FieldType, CreateFieldAccessor(bodyType, field)));
            }

            // DataContractJsonSerializer also serializes public fields on non-[DataContract] types.
            foreach (var field in bodyType.GetFields(BindingFlags.Instance | BindingFlags.Public))
            {
                if (field.GetCustomAttribute<IgnoreDataMemberAttribute>() is not null)
                {
                    continue;
                }

                var dataMemberName = field.GetCustomAttribute<DataMemberAttribute>()?.Name;
                var name = StringUtil.IsNullOrEmpty(dataMemberName) ? field.Name : dataMemberName;
                extractors.Add(new MemberExtractor(name!, field.FieldType, CreateFieldAccessor(bodyType, field)));
            }

            return extractors.ToArray();
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
            Func<Type, MemberExtractor?[]> createExtractors,
            bool useSimpleDictionaryFormat)
        {
            if (itemType == typeof(string))
            {
                return value;
            }

            if (itemType.IsArray || (itemType.IsGenericType && itemType.GetGenericTypeDefinition() == typeof(List<>)))
            {
                return ExtractListOrArray(value, depth, visited, extractorCache, createExtractors, useSimpleDictionaryFormat);
            }

            if (itemType.IsGenericType && itemType.GetGenericTypeDefinition() == typeof(Dictionary<,>))
            {
                // DataContractJsonSerializer with UseSimpleDictionaryFormat=false (the default) writes
                // dictionaries as [{Key:k, Value:v}] arrays rather than {k:v} objects.
                return useSimpleDictionaryFormat
                    ? ExtractDictionary(value, itemType, depth, visited, extractorCache, createExtractors, useSimpleDictionaryFormat)
                    : ExtractDictionaryAsKeyValuePairs(value, itemType, depth, visited, extractorCache, createExtractors, useSimpleDictionaryFormat);
            }

            // case of System.Web.Routing.RouteValueDictionary and types inheriting IDictionary
            var iDictionaryType = typeof(IDictionary<string, object>);
            if (iDictionaryType.IsAssignableFrom(itemType))
            {
                return ExtractDictionary(value, iDictionaryType, depth, visited, extractorCache, createExtractors, useSimpleDictionaryFormat);
            }

            // DataContractJsonSerializer and other JSON serializers emit Collection<T>, ObservableCollection<T>,
            // HashSet<T>, ISet<T>, IList<T>, and similar collection types as JSON arrays. Strings are handled
            // above; dictionaries are handled above (and they also implement IEnumerable, so this check MUST
            // follow the dictionary branches to avoid mis-routing them).
            if (value is IEnumerable)
            {
                return ExtractListOrArray(value, depth, visited, extractorCache, createExtractors, useSimpleDictionaryFormat);
            }

            if (WafProcessableTypes.Contains(itemType))
            {
                return value;
            }

            if (itemType.IsEnum)
            {
                // JSON serializers (DataContractJsonSerializer, JSON.NET, System.Text.Json) emit enums as
                // their underlying numeric value, not the name. Widen to long/ulong because the WAF encoder
                // only handles int/uint/long/ulong as numerics — smaller types (byte/short/ushort/sbyte)
                // would be silently dropped (encoded as an empty string) by the modern WAF encoder.
                var underlying = Enum.GetUnderlyingType(itemType);
                return underlying == typeof(byte) || underlying == typeof(ushort) || underlying == typeof(uint) || underlying == typeof(ulong)
                    ? (object)Convert.ToUInt64(value)
                    : (object)Convert.ToInt64(value);
            }

            var unhandledType = itemType == typeof(Guid) || itemType == typeof(DateTime) || itemType == typeof(DateTimeOffset) || itemType == typeof(TimeSpan) || itemType.IsPrimitive;
#if NET6_0_OR_GREATER
            unhandledType = unhandledType || itemType == typeof(DateOnly) || itemType == typeof(TimeOnly);
#endif
            if (unhandledType)
            {
                return value.ToString();
            }

            var nestedDict = ExtractProperties(value, depth, visited, extractorCache, createExtractors, useSimpleDictionaryFormat);
            return nestedDict;
        }

        private static IReadOnlyDictionary<string, object?> ExtractDictionary(
            object value,
            Type dictType,
            int depth,
            HashSet<object> visited,
            ConcurrentDictionary<Type, MemberExtractor?[]> extractorCache,
            Func<Type, MemberExtractor?[]> createExtractors,
            bool useSimpleDictionaryFormat)
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
                            var extractedvalue = ExtractType(dictValue.GetType(), dictValue, depth + 1, visited, extractorCache, createExtractors, useSimpleDictionaryFormat);
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

        // DataContractJsonSerializer with UseSimpleDictionaryFormat=false serializes Dictionary<K,V>
        // as [{Key:k, Value:v}] arrays rather than {k:v} objects. Mirror that structure.
        private static List<object?> ExtractDictionaryAsKeyValuePairs(
            object value,
            Type dictType,
            int depth,
            HashSet<object> visited,
            ConcurrentDictionary<Type, MemberExtractor?[]> extractorCache,
            Func<Type, MemberExtractor?[]> createExtractors,
            bool useSimpleDictionaryFormat)
        {
            if (!visited.Add(value))
            {
                return [];
            }

            var gtkvp = typeof(KeyValuePair<,>);
            var tkvp = gtkvp.MakeGenericType(dictType.GetGenericArguments());
            var keyProp = tkvp.GetProperty("Key");
            var valueProp = tkvp.GetProperty("Value");

            var sourceDict = value as IEnumerable;
            var items = value is ICollection sourceColl
                ? new List<object?>(Math.Min(WafConstants.MaxContainerSize, sourceColl.Count))
                : new List<object?>();

            if (sourceDict is not null && keyProp is not null && valueProp is not null)
            {
                foreach (var entry in sourceDict)
                {
                    var dictKey = keyProp.GetValue(entry);
                    var dictValue = valueProp.GetValue(entry);

                    var pair = new Dictionary<string, object?>(2)
                    {
                        ["Key"] = dictKey is null ? null : ExtractType(dictKey.GetType(), dictKey, depth + 1, visited, extractorCache, createExtractors, useSimpleDictionaryFormat),
                        ["Value"] = dictValue is null ? null : ExtractType(dictValue.GetType(), dictValue, depth + 1, visited, extractorCache, createExtractors, useSimpleDictionaryFormat),
                    };
                    items.Add(pair);

                    if (items.Count >= WafConstants.MaxContainerSize)
                    {
                        break;
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
            Func<Type, MemberExtractor?[]> createExtractors,
            bool useSimpleDictionaryFormat)
        {
            if (visited.Contains(value))
            {
                return [];
            }

            if (value is not IEnumerable source)
            {
                return [];
            }

            // Use ICollection for pre-sizing when available. Some types (e.g. HashSet<T>) implement
            // ICollection<T> but not the non-generic ICollection, so fall back to default capacity.
            var items = value is ICollection sourceColl
                ? new List<object?>(Math.Min(WafConstants.MaxContainerSize, sourceColl.Count))
                : new List<object?>();

            foreach (var item in source)
            {
                if (item is null)
                {
                    items.Add(item);
                }
                else
                {
                    var extractedvalue = ExtractType(item.GetType(), item, depth + 1, visited, extractorCache, createExtractors, useSimpleDictionaryFormat);
                    items.Add(extractedvalue);
                }

                if (items.Count >= WafConstants.MaxContainerSize)
                {
                    break;
                }
            }

            return items;
        }

        private sealed class MemberExtractor
        {
            // Pre-computed default value for value types when EmitDefaultValue=false,
            // used by IsDefault() to avoid allocating a new instance on every member check.
            private readonly object? _valueTypeDefault;

            public MemberExtractor(string name, Type type, Func<object, object?> accessor, bool emitDefaultValue = true)
            {
                Name = name;
                Type = type;
                Accessor = accessor;
                EmitDefaultValue = emitDefaultValue;
                if (!emitDefaultValue && type.IsValueType)
                {
                    _valueTypeDefault = Activator.CreateInstance(type);
                }
            }

            public string Name { get; }

            public Type Type { get; }

            public Func<object, object?> Accessor { get; }

            public bool EmitDefaultValue { get; }

            public bool IsDefault(object? value)
                => value is null || (_valueTypeDefault is not null && value.Equals(_valueTypeDefault));
        }
    }
}
