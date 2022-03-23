// <copyright file="DebuggerSnapshotSerializer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using Datadog.Trace.Logging;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.Debugger.Snapshots
{
    internal static class DebuggerSnapshotSerializer
    {
        private const string ReachedTimeoutMessage = "Reached timeout";
        private const string ReachedNumberOfObjectsMessage = "Reached the maximum number of objects";
        private const string ReachedNumberOfItemsMessage = "Reached the maximum number of items";
        private const int MaximumNumberOfItemsInCollectionToCopy = 100;
        private const int MaximumNumberOfFieldsToCopy = 1000;
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(DebuggerSnapshotSerializer));
        private static readonly ImmutableDebuggerSettings DebuggerSettings = ImmutableDebuggerSettings.Create(Debugger.DebuggerSettings.FromDefaultSource());
        private static readonly int MaximumDepthOfMembersToCopy = DebuggerSettings.MaximumDepthOfMembersOfMembersToCopy;
        private static readonly int MillisecondsToCancel = DebuggerSettings.MaxSerializationTimeInMilliseconds;

        /// <summary>
        /// Note: implemented recursively. We might want to consider an iterative approach for performance gain (Clone takes part in the processing of sequence points).
        /// </summary>
        internal static void Serialize(
            object source,
            JsonWriter jsonWriter,
            string variableName,
            int? maximumDepthOfMembersToCopy = null)
        {
            var totalObjects = 0;
            using var cts = new CancellationTokenSource();
            cts.CancelAfter(MillisecondsToCancel);
            SerializeInternal(source, jsonWriter, cts, 0, maximumDepthOfMembersToCopy ?? MaximumDepthOfMembersToCopy, ref totalObjects, variableName);
        }

        private static void SerializeInternal(
            object source,
            JsonWriter jsonWriter,
            CancellationTokenSource cts,
            int currentDepth,
            int maximumDepthOfMembersToCopy,
            ref int totalObjects,
            string variableName = null,
            bool isCollectionItem = false)
        {
            try
            {
                if (currentDepth >= maximumDepthOfMembersToCopy)
                {
                    return;
                }

                if (source == null)
                {
                    jsonWriter.WritePropertyName(variableName);
                    jsonWriter.WriteStartObject();
                    jsonWriter.WritePropertyName("type");
                    jsonWriter.WriteValue("NA");
                    jsonWriter.WritePropertyName("value");
                    jsonWriter.WriteNull();
                    jsonWriter.WriteEndObject();
                    return;
                }

                if (source is IEnumerable enumerable && SupportedTypesService.IsSupportedCollection(source))
                {
                    jsonWriter.WritePropertyName(variableName);
                    jsonWriter.WriteStartObject();
                    CloneEnumerable(source, jsonWriter, enumerable, currentDepth, ref totalObjects, maximumDepthOfMembersToCopy, cts);
                    jsonWriter.WriteEndObject();
                }
                else if (SupportedTypesService.IsDenied(source.GetType()))
                {
                    jsonWriter.WritePropertyName(variableName);
                    jsonWriter.WriteStartObject();
                    jsonWriter.WritePropertyName("type");
                    jsonWriter.WriteValue(source.GetType().Name);
                    jsonWriter.WritePropertyName("value");
                    jsonWriter.WriteValue("********");
                    jsonWriter.WriteEndObject();
                }
                else
                {
                    SerializeObject(
                        source,
                        jsonWriter,
                        cts,
                        currentDepth,
                        maximumDepthOfMembersToCopy,
                        ref totalObjects,
                        variableName,
                        isCollectionItem);
                }
            }
            catch (Exception e)
            {
                Log.Error(e.ToString());
            }
        }

        private static void SerializeObject(
            object source,
            JsonWriter jsonWriter,
            CancellationTokenSource cts,
            int currentDepth,
            int maximumDepthOfHierarchyToCopy,
            ref int totalObjects,
            string variableName = null,
            bool isCollectionItem = false)
        {
            if (isCollectionItem == false)
            {
                jsonWriter.WritePropertyName(variableName);
                jsonWriter.WriteStartObject();
            }

            var type = source.GetType();
            jsonWriter.WritePropertyName("type");
            jsonWriter.WriteValue(type.Name);
            jsonWriter.WritePropertyName("value");

            if (SupportedTypesService.IsSafeToCallToString(source))
            {
                totalObjects++;
                jsonWriter.WriteValue(source.ToString());
                jsonWriter.WriteEndObject();
                return;
            }

            jsonWriter.WriteValue(type.Name);
            totalObjects++;
            int index = 0;
            try
            {
                var selector = SnapshotSerializerFieldsAndPropsSelector.CreateDeepClonerFieldsAndPropsSelector(type);
                var fields = selector.GetFieldsAndProps(type, source, maximumDepthOfHierarchyToCopy, MaximumNumberOfFieldsToCopy, cts);
                foreach (var field in fields)
                {
                    cts.Token.ThrowIfCancellationRequested();

                    if (index == 0)
                    {
                        jsonWriter.WritePropertyName("fields");
                        jsonWriter.WriteStartObject();
                    }

                    var fieldOrPropertyName = GetAutoPropertyOrFieldName(field.Name);

                    if (totalObjects >= MaximumNumberOfFieldsToCopy)
                    {
                        WriteLimitReachedNotification(jsonWriter, fieldOrPropertyName ?? source.GetType().Name, ReachedNumberOfObjectsMessage);
                        jsonWriter.WriteEndObject();
                        jsonWriter.WriteEndObject();
                        return;
                    }

                    if (!TryGetValue(field, source, out var value))
                    {
                        continue;
                    }

                    index++;
                    SerializeInternal(
                        value,
                        jsonWriter,
                        cts,
                        currentDepth + 1,
                        maximumDepthOfHierarchyToCopy,
                        ref totalObjects,
                        fieldOrPropertyName);
                }

                if (index > 0)
                {
                    jsonWriter.WriteEndObject();
                }
            }
            catch (OperationCanceledException)
            {
                WriteLimitReachedNotification(jsonWriter, variableName ?? source.GetType().Name, ReachedTimeoutMessage);
            }
            catch (Exception e)
            {
                Log.Error(e.ToString());
                return;
            }

            jsonWriter.WriteEndObject();
        }

        private static void CloneEnumerable(
           object source,
           JsonWriter jsonWriter,
           IEnumerable enumerable,
           int currentDepth,
           ref int totalObjects,
           int maximumDepthOfHierarchyToCopy,
           CancellationTokenSource cts)
        {
            try
            {
                var itemIndex = 0;
                var enumerator = enumerable.GetEnumerator();

                // doing our best efforts to extract the underlying count of the collection
                if (source is ICollection collection)
                {
                    jsonWriter.WritePropertyName("type");
                    jsonWriter.WriteValue(source.GetType().Name);
                    jsonWriter.WritePropertyName("value");
                    jsonWriter.WriteValue($"count: {collection.Count}");
                    jsonWriter.WritePropertyName("Collection");
                    jsonWriter.WriteStartArray();
                }

                while (itemIndex < MaximumNumberOfItemsInCollectionToCopy &&
                       totalObjects < MaximumNumberOfFieldsToCopy &&
                       enumerator.MoveNext())
                {
                    cts.Token.ThrowIfCancellationRequested();

                    totalObjects++;
                    jsonWriter.WriteStartObject();
                    SerializeInternal(
                        enumerator.Current,
                        jsonWriter,
                        cts,
                        currentDepth,
                        maximumDepthOfHierarchyToCopy,
                        ref totalObjects,
                        isCollectionItem: true);
                    itemIndex++;
                }

                if (enumerator.MoveNext())
                {
                    jsonWriter.WritePropertyName("type");
                    jsonWriter.WriteValue("[...]");
                    WriteLimitReachedNotification(jsonWriter, "value", ReachedNumberOfItemsMessage);
                }

                totalObjects++;
                jsonWriter.WriteEndArray();
            }
            catch (OperationCanceledException)
            {
                WriteLimitReachedNotification(jsonWriter, source.GetType().Name, ReachedTimeoutMessage);
                jsonWriter.WriteEndArray();
            }
            catch (Exception e)
            {
                Log.Error(e.ToString());
            }
        }

        private static void WriteLimitReachedNotification(JsonWriter writer, string objectName, string message)
        {
            Log.Debug($"{nameof(DebuggerSnapshotSerializer)}.{nameof(WriteLimitReachedNotification)}: {objectName} {message}");
            writer.WritePropertyName(objectName);
            writer.WriteValue(message);
        }

        private static string GetAutoPropertyOrFieldName(string fieldName)
        {
            const string prefix = "<";
            const string suffix = ">k__BackingField";
            var match = Regex.Match(fieldName, $"{prefix}(.+?){suffix}");
            return match.Success ? match.Groups[1].Value : fieldName;
        }

        internal static bool TryGetValue(MemberInfo fieldOrProp, object source, out object value)
        {
            switch (fieldOrProp)
            {
                case FieldInfo field:
                    {
                        value = field.GetValue(source);
                        return true;
                    }

                case PropertyInfo property:
                    {
                        value = property.GetMethod.Invoke(source, Array.Empty<object>());
                        return true;
                    }

                default:
                    {
                        Log.Error($"{nameof(DebuggerSnapshotSerializer)}.{nameof(TryGetValue)}: Can't get value of {fieldOrProp.Name}. Unsupported member info {fieldOrProp.GetType()}");
                        value = null;
                        return false;
                    }
            }
        }
    }
}
