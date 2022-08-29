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
        /// Note: implemented recursively. We might want to consider an iterative approach for performance gain (Serialize takes part in the MethodDebuggerInvoker process).
        /// </summary>
        internal static void Serialize(
            object source,
            Type type,
            string name,
            JsonWriter jsonWriter)
        {
            var totalObjects = 0;
            using var cts = CreateCancellationTimeout();
            SerializeInternal(source, type, jsonWriter, cts, currentDepth: 0, ref totalObjects, name, fieldsOnly: false);
        }

        internal static void SerializeObjectFields(
            object source,
            Type type,
            JsonWriter jsonWriter)
        {
            var totalObjects = 0;
            using var cts = CreateCancellationTimeout();
            SerializeInternal(source, type, jsonWriter, cts, currentDepth: 0, ref totalObjects, variableName: null, fieldsOnly: true);
        }

        private static bool SerializeInternal(
            object source,
            Type type,
            JsonWriter jsonWriter,
            CancellationTokenSource cts,
            int currentDepth,
            ref int totalObjects,
            string variableName,
            bool fieldsOnly)
        {
            try
            {
                if (source is IEnumerable enumerable && SupportedTypesService.IsSupportedCollection(source))
                {
                    if (variableName != null)
                    {
                        jsonWriter.WritePropertyName(variableName);
                    }

                    jsonWriter.WriteStartObject();
                    SerializeEnumerable(source, type, jsonWriter, enumerable, currentDepth, ref totalObjects, cts);
                    jsonWriter.WriteEndObject();

                    return true;
                }

                if (SupportedTypesService.IsDenied(type))
                {
                    jsonWriter.WritePropertyName(variableName);
                    jsonWriter.WriteStartObject();
                    jsonWriter.WritePropertyName("type");
                    jsonWriter.WriteValue(type.Name);
                    jsonWriter.WritePropertyName("value");
                    jsonWriter.WriteValue("********");
                    jsonWriter.WriteEndObject();
                    return true;
                }

                return SerializeObject(
                    source,
                    type,
                    jsonWriter,
                    cts,
                    currentDepth,
                    ref totalObjects,
                    variableName,
                    fieldsOnly);
            }
            catch (Exception e)
            {
                Log.Error(e.ToString());
            }

            return false;
        }

        private static bool SerializeObject(
            object source,
            Type type,
            JsonWriter jsonWriter,
            CancellationTokenSource cts,
            int currentDepth,
            ref int totalObjects,
            string variableName,
            bool fieldsOnly)
        {
            totalObjects++;
            if (!fieldsOnly)
            {
                WriteTypeAndValue(source, type, jsonWriter, variableName);
            }

            try
            {
                SerializeFieldsInternal(source, type, jsonWriter, cts, currentDepth, ref totalObjects);
                if (!fieldsOnly)
                {
                    jsonWriter.WriteEndObject();
                }

                return true;
            }
            catch (OperationCanceledException)
            {
                WriteLimitReachedNotification(jsonWriter, variableName ?? type.Name, ReachedTimeoutMessage, !fieldsOnly);
            }
            catch (Exception e)
            {
                Log.Error(e.ToString());
            }

            return false;
        }

        private static void WriteTypeAndValue(
            object source,
            Type type,
            JsonWriter jsonWriter,
            string variableName)
        {
            if (variableName != null)
            {
                jsonWriter.WritePropertyName(variableName);
            }

            jsonWriter.WriteStartObject();
            jsonWriter.WritePropertyName("type");
            jsonWriter.WriteValue(type.Name);
            jsonWriter.WritePropertyName("value");

            if (source == null)
            {
                jsonWriter.WriteValue("null");
            }
            else if (SupportedTypesService.IsSafeToCallToString(type))
            {
                jsonWriter.WriteValue(source.ToString());
            }
            else
            {
                jsonWriter.WriteValue(type.Name);
            }
        }

        private static void SerializeFieldsInternal(
            object source,
            Type type,
            JsonWriter jsonWriter,
            CancellationTokenSource cts,
            int currentDepth,
            ref int totalObjects)
        {
            if (currentDepth >= MaximumDepthOfMembersToCopy || SupportedTypesService.IsSafeToCallToString(type))
            {
                jsonWriter.WritePropertyName("fields");
                jsonWriter.WriteNull();
                return;
            }

            int index = 0;
            var selector = SnapshotSerializerFieldsAndPropsSelector.CreateDeepClonerFieldsAndPropsSelector(type);
            var fields = selector.GetFieldsAndProps(type, source, MaximumDepthOfMembersToCopy, MaximumNumberOfFieldsToCopy, cts);

            foreach (var field in fields)
            {
                var fieldOrPropertyName = GetAutoPropertyOrFieldName(field.Name);

                if (totalObjects >= MaximumNumberOfFieldsToCopy)
                {
                    WriteLimitReachedNotification(jsonWriter, fieldOrPropertyName ?? type.Name, ReachedNumberOfObjectsMessage, index > 0);
                    return;
                }

                if (!TryGetValue(field, source, out var value, out type))
                {
                    continue;
                }

                if (index == 0)
                {
                    jsonWriter.WritePropertyName("fields");
                    jsonWriter.WriteStartObject();
                }

                index++;
                var serialized = SerializeInternal(
                    value,
                    type,
                    jsonWriter,
                    cts,
                    currentDepth + 1,
                    ref totalObjects,
                    fieldOrPropertyName,
                    fieldsOnly: false);

                if (!serialized)
                {
                    break;
                }
            }

            if (index > 0)
            {
                jsonWriter.WriteEndObject();
            }
            else
            {
                jsonWriter.WritePropertyName("fields");
                jsonWriter.WriteNull();
            }
        }

        private static void SerializeEnumerable(
           object source,
           Type type,
           JsonWriter jsonWriter,
           IEnumerable enumerable,
           int currentDepth,
           ref int totalObjects,
           CancellationTokenSource cts)
        {
            try
            {
                if (source is ICollection collection)
                {
                    jsonWriter.WritePropertyName("type");
                    jsonWriter.WriteValue(type.Name);
                    jsonWriter.WritePropertyName("value");
                    jsonWriter.WriteValue($"count: {collection.Count}");
                    jsonWriter.WritePropertyName("Collection");
                    jsonWriter.WriteStartArray();

                    var itemIndex = 0;
                    var enumerator = enumerable.GetEnumerator();

                    while (itemIndex < MaximumNumberOfItemsInCollectionToCopy &&
                           totalObjects < MaximumNumberOfFieldsToCopy &&
                           enumerator.MoveNext())
                    {
                        cts.Token.ThrowIfCancellationRequested();
                        if (enumerator.Current == null)
                        {
                            break;
                        }

                        totalObjects++;
                        var serialized = SerializeInternal(
                            enumerator.Current,
                            enumerator.Current.GetType(),
                            jsonWriter,
                            cts,
                            currentDepth,
                            ref totalObjects,
                            variableName: null,
                            fieldsOnly: false);

                        itemIndex++;
                        if (!serialized)
                        {
                            break;
                        }
                    }

                    if (enumerator.MoveNext())
                    {
                        jsonWriter.WriteStartObject();
                        jsonWriter.WritePropertyName("type");
                        jsonWriter.WriteValue("[...]");
                        WriteLimitReachedNotification(jsonWriter, "value", ReachedNumberOfItemsMessage, true);
                    }

                    totalObjects++;
                    jsonWriter.WriteEndArray();
                }
            }
            catch (OperationCanceledException)
            {
                WriteLimitReachedNotification(jsonWriter, type.Name, ReachedTimeoutMessage, false);
                jsonWriter.WriteEndArray();
            }
            catch (Exception e)
            {
                Log.Error(e.ToString());
            }
        }

        private static void WriteLimitReachedNotification(JsonWriter writer, string objectName, string message, bool shouldCloseObject)
        {
            Log.Debug($"{nameof(DebuggerSnapshotSerializer)}.{nameof(WriteLimitReachedNotification)}: {objectName} {message}");
            writer.WritePropertyName(objectName);
            writer.WriteValue(message);
            if (shouldCloseObject)
            {
                writer.WriteEndObject();
            }
        }

        private static string GetAutoPropertyOrFieldName(string fieldName)
        {
            const string prefix = "<";
            const string suffix = ">k__BackingField";
            var match = Regex.Match(fieldName, $"{prefix}(.+?){suffix}");
            return match.Success ? match.Groups[1].Value : fieldName;
        }

        internal static bool TryGetValue(MemberInfo fieldOrProp, object source, out object value, out Type type)
        {
            value = null;
            type = null;
            try
            {
                switch (fieldOrProp)
                {
                    case FieldInfo field:
                        {
                            if (field.FieldType.ContainsGenericParameters ||
                                field.DeclaringType.ContainsGenericParameters ||
                                field.ReflectedType.ContainsGenericParameters)
                            {
                                return false;
                            }

                            type = field.FieldType;
                            if (source != null || field.IsStatic)
                            {
                                value = field.GetValue(source);
                                return true;
                            }

                            break;
                        }

                    case PropertyInfo property:
                        {
                            type = property.PropertyType;
                            if (source != null || property.GetMethod?.IsStatic == true)
                            {
                                value = property.GetMethod.Invoke(source, Array.Empty<object>());
                                return true;
                            }

                            break;
                        }

                    default:
                        {
                            Log.Error($"{nameof(DebuggerSnapshotSerializer)}.{nameof(TryGetValue)}: Can't get value of {fieldOrProp.Name}. Unsupported member info {fieldOrProp.GetType()}");
                            break;
                        }
                }
            }
            catch (Exception e)
            {
                Log.Error($"{nameof(DebuggerSnapshotSerializer)}.{nameof(TryGetValue)}: {e}");
            }

            return false;
        }

        private static CancellationTokenSource CreateCancellationTimeout()
        {
            var cts = new CancellationTokenSource();
            cts.CancelAfter(MillisecondsToCancel);
            return cts;
        }
    }
}
