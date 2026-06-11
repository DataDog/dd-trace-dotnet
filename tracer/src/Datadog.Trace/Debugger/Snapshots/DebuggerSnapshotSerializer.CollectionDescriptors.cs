// <copyright file="DebuggerSnapshotSerializer.CollectionDescriptors.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections;
using System.Linq.Expressions;
using System.Reflection;
using Datadog.Trace.Vendors.Newtonsoft.Json.Utilities;

#nullable enable

namespace Datadog.Trace.Debugger.Snapshots
{
    internal static partial class DebuggerSnapshotSerializer
    {
        // Cache only supported descriptors. If the cache fills up, additional supported types still
        // serialize correctly but rebuild their descriptor; unsupported IEnumerable types are never cached.
        private const int MaxSupportedEnumerableDescriptorCacheSize = 64;

        // Dictionary entry descriptors are only requested while serializing supported dictionaries.
        // If the cache fills up, entries still serialize correctly but rebuild their descriptor.
        private const int MaxDictionaryEntryDescriptorCacheSize = 64;

        private static readonly object SupportedEnumerableDescriptorCacheLock = new();
        private static readonly object DictionaryEntryDescriptorCacheLock = new();

        private static volatile SupportedEnumerableDescriptor[] _supportedEnumerableDescriptors = [];
        private static volatile DictionaryEntryDescriptor[] _dictionaryEntryDescriptors = [];

        private static DictionaryEntryDescriptor GetDictionaryEntryDescriptor(Type runtimeType)
        {
            var descriptors = _dictionaryEntryDescriptors;
            for (var i = 0; i < descriptors.Length; i++)
            {
                if (descriptors[i].RuntimeType == runtimeType)
                {
                    return descriptors[i];
                }
            }

            lock (DictionaryEntryDescriptorCacheLock)
            {
                descriptors = _dictionaryEntryDescriptors;
                for (var i = 0; i < descriptors.Length; i++)
                {
                    if (descriptors[i].RuntimeType == runtimeType)
                    {
                        return descriptors[i];
                    }
                }

                var descriptor = CreateDictionaryEntryDescriptor(runtimeType);
                if (descriptors.Length < MaxDictionaryEntryDescriptorCacheSize)
                {
                    var newDescriptors = new DictionaryEntryDescriptor[descriptors.Length + 1];
                    Array.Copy(descriptors, newDescriptors, descriptors.Length);
                    newDescriptors[descriptors.Length] = descriptor;
                    _dictionaryEntryDescriptors = newDescriptors;
                }

                return descriptor;
            }
        }

        private static DictionaryEntryDescriptor CreateDictionaryEntryDescriptor(Type runtimeType)
        {
            var reflectionObject = ReflectionObject.Create(runtimeType, "Key", "Value");
            return new DictionaryEntryDescriptor(
                runtimeType,
                reflectionObject,
                reflectionObject.GetType("Key"),
                reflectionObject.GetType("Value"));
        }

        private static bool TryGetSupportedEnumerableInfo(
            object source,
            out SupportedEnumerableInfo enumerableInfo)
        {
            var runtimeType = source.GetType();

            if (source is ICollection collection)
            {
                var isDictionary = Redaction.IsSupportedDictionary(runtimeType);
                if (!isDictionary && !Redaction.IsSupportedCollection(runtimeType))
                {
                    enumerableInfo = default;
                    return false;
                }

                enumerableInfo = new SupportedEnumerableInfo(collection.Count, isDictionary);
                return true;
            }

            if (!TryGetSupportedEnumerableDescriptor(runtimeType, out var descriptor))
            {
                enumerableInfo = default;
                return false;
            }

            enumerableInfo = new SupportedEnumerableInfo(descriptor.GetCount(source), descriptor.IsDictionary);
            return true;
        }

        private static bool TryGetSupportedEnumerableDescriptor(Type runtimeType, out SupportedEnumerableDescriptor descriptor)
        {
            var descriptors = _supportedEnumerableDescriptors;
            for (var i = 0; i < descriptors.Length; i++)
            {
                if (descriptors[i].RuntimeType == runtimeType)
                {
                    descriptor = descriptors[i];
                    return true;
                }
            }

            lock (SupportedEnumerableDescriptorCacheLock)
            {
                descriptors = _supportedEnumerableDescriptors;
                for (var i = 0; i < descriptors.Length; i++)
                {
                    if (descriptors[i].RuntimeType == runtimeType)
                    {
                        descriptor = descriptors[i];
                        return true;
                    }
                }

                descriptor = CreateSupportedEnumerableDescriptor(runtimeType);
                if (!descriptor.IsSupported)
                {
                    return false;
                }

                if (descriptors.Length < MaxSupportedEnumerableDescriptorCacheSize)
                {
                    var newDescriptors = new SupportedEnumerableDescriptor[descriptors.Length + 1];
                    Array.Copy(descriptors, newDescriptors, descriptors.Length);
                    newDescriptors[descriptors.Length] = descriptor;
                    _supportedEnumerableDescriptors = newDescriptors;
                }
            }

            return true;
        }

        private static SupportedEnumerableDescriptor CreateSupportedEnumerableDescriptor(Type runtimeType)
        {
            var isDictionary = Redaction.IsSupportedDictionary(runtimeType);
            if (!isDictionary && !Redaction.IsSupportedCollection(runtimeType))
            {
                return SupportedEnumerableDescriptor.NotSupported;
            }

            var countProperty = runtimeType.GetProperty(nameof(ICollection.Count));
            if (countProperty == null || countProperty.PropertyType != typeof(int))
            {
                return SupportedEnumerableDescriptor.NotSupported;
            }

            return new SupportedEnumerableDescriptor(runtimeType, CreateCountAccessor(runtimeType, countProperty), isDictionary);
        }

        private static Func<object, int> CreateCountAccessor(Type runtimeType, PropertyInfo countProperty)
        {
            var sourceParameter = Expression.Parameter(typeof(object), "source");
            var castSource = Expression.Convert(sourceParameter, runtimeType);
            var count = Expression.Property(castSource, countProperty);
            return Expression.Lambda<Func<object, int>>(count, sourceParameter).Compile();
        }

        private readonly struct SupportedEnumerableInfo
        {
            internal SupportedEnumerableInfo(int count, bool isDictionary)
            {
                Count = count;
                IsDictionary = isDictionary;
            }

            internal int Count { get; }

            internal bool IsDictionary { get; }
        }

        private readonly struct SupportedEnumerableDescriptor
        {
            internal static readonly SupportedEnumerableDescriptor NotSupported = new(null, null, isDictionary: false);

            private readonly Func<object, int>? _getCount;

            internal SupportedEnumerableDescriptor(Type? runtimeType, Func<object, int>? getCount, bool isDictionary)
            {
                RuntimeType = runtimeType;
                _getCount = getCount;
                IsDictionary = isDictionary;
            }

            internal Type? RuntimeType { get; }

            internal bool IsSupported => _getCount != null;

            internal bool IsDictionary { get; }

            internal int GetCount(object source) => _getCount!(source);
        }

        private readonly struct DictionaryEntryDescriptor
        {
            private readonly ReflectionObject _reflectionObject;

            internal DictionaryEntryDescriptor(
                Type runtimeType,
                ReflectionObject reflectionObject,
                Type keyType,
                Type valueType)
            {
                RuntimeType = runtimeType;
                _reflectionObject = reflectionObject;
                KeyType = keyType;
                ValueType = valueType;
            }

            internal Type RuntimeType { get; }

            internal Type KeyType { get; }

            internal Type ValueType { get; }

            internal object? GetKey(object source) => _reflectionObject.GetValue(source, "Key");

            internal object? GetValue(object source) => _reflectionObject.GetValue(source, "Value");
        }
    }
}
