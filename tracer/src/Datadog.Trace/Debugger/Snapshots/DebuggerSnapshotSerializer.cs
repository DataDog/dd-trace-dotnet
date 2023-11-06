// <copyright file="DebuggerSnapshotSerializer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using Datadog.Trace.Logging;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json.Utilities;

namespace Datadog.Trace.Debugger.Snapshots
{
    internal static partial class DebuggerSnapshotSerializer
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(DebuggerSnapshotSerializer));

        private static int _maximumNumberOfItemsInCollectionToCopy = DebuggerSettings.DefaultMaxNumberOfItemsInCollectionToCopy;
        private static int _maximumNumberOfFieldsToCopy = DebuggerSettings.DefaultMaxNumberOfFieldsToCopy;
        private static int _maximumDepthOfMembersToCopy = DebuggerSettings.DefaultMaxDepthToSerialize;
        private static int _maximumSerializationTime = DebuggerSettings.DefaultMaxSerializationTimeInMilliseconds;
        private static int _maximumStringLength = 1000;

        internal static void SetConfig(DebuggerSettings debuggerSettings)
        {
            _maximumDepthOfMembersToCopy = debuggerSettings.MaximumDepthOfMembersToCopy;
            _maximumSerializationTime = debuggerSettings.MaxSerializationTimeInMilliseconds;
        }

        /// <summary>
        /// Note: implemented recursively. We might want to consider an iterative approach for performance gain (Serialize takes part in the MethodDebuggerInvoker process).
        /// </summary>
        internal static void Serialize(
            object source,
            Type type,
            string name,
            JsonWriter jsonWriter)
        {
            using var cts = CreateCancellationTimeout();
            SerializeInternal(source, type, jsonWriter, cts, currentDepth: 0, name, fieldsOnly: false);
        }

        public static void SerializeStaticFields(Type declaringType, JsonTextWriter jsonWriter)
        {
            using var cts = CreateCancellationTimeout();
            WriteFields(null, declaringType, jsonWriter, cts, currentDepth: 0, writeStaticFields: true);
        }

        private static bool SerializeInternal(
            object source,
            Type type,
            JsonWriter jsonWriter,
            CancellationTokenSource cts,
            int currentDepth,
            string variableName,
            bool fieldsOnly)
        {
            try
            {
                if (Redaction.ShouldRedact(variableName, type, out var redactionReason))
                {
                    if (variableName != null)
                    {
                        jsonWriter.WritePropertyName(variableName);
                    }

                    var notCapturedReason = redactionReason == RedactionReason.Identifier ? NotCapturedReason.redactedByIndentifier : NotCapturedReason.redactedByType;

                    jsonWriter.WriteStartObject();
                    jsonWriter.WritePropertyName("type");
                    jsonWriter.WriteValue(type.Name);
                    WriteNotCapturedReason(jsonWriter, notCapturedReason);
                    jsonWriter.WriteEndObject();

                    return true;
                }

                if (source is IEnumerable enumerable && (Redaction.IsSupportedCollection(source) ||
                                                         Redaction.IsSupportedDictionary(source)))
                {
                    if (variableName != null)
                    {
                        jsonWriter.WritePropertyName(variableName);
                    }

                    jsonWriter.WriteStartObject();
                    SerializeEnumerable(source, type, jsonWriter, enumerable, currentDepth, cts);
                    jsonWriter.WriteEndObject();

                    return true;
                }

                return SerializeObject(
                    source,
                    type,
                    jsonWriter,
                    cts,
                    currentDepth,
                    variableName,
                    fieldsOnly);
            }
            catch (Exception e)
            {
                Log.Error(e, "Error serializing object {VariableName} Depth={CurrentDepth} FieldsOnly={FieldsOnly}", variableName, currentDepth, fieldsOnly);
            }

            return false;
        }

        private static bool SerializeObject(
            object source,
            Type type,
            JsonWriter jsonWriter,
            CancellationTokenSource cts,
            int currentDepth,
            string variableName,
            bool fieldsOnly)
        {
            if (!fieldsOnly)
            {
                WriteTypeAndValue(source, type, jsonWriter, variableName);
            }

            try
            {
                SerializeInstanceFieldsInternal(source, type, jsonWriter, cts, currentDepth);
                if (!fieldsOnly)
                {
                    jsonWriter.WriteEndObject();
                }

                return true;
            }
            catch (OperationCanceledException)
            {
                WriteNotCapturedReason(jsonWriter, NotCapturedReason.timeout);
            }
            catch (Exception e)
            {
                Log.Error(e, "Error serializing object {VariableName} Depth={CurrentDepth} FieldsOnly={FieldsOnly}", variableName, currentDepth, fieldsOnly);
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

            if (source == null)
            {
                jsonWriter.WritePropertyName("isNull");
                jsonWriter.WriteValue("true");
            }
            else if (Redaction.IsSafeToCallToString(type))
            {
                jsonWriter.WritePropertyName("value");
                var stringValue = source.ToString();
                var stringValueTruncated = stringValue?.Length < _maximumStringLength ? stringValue : stringValue?.Substring(0, _maximumStringLength);
                jsonWriter.WriteValue(stringValueTruncated);
            }
            else
            {
                jsonWriter.WritePropertyName("value");
                jsonWriter.WriteValue(type.Name);
            }
        }

        private static void SerializeInstanceFieldsInternal(
            object source,
            Type type,
            JsonWriter jsonWriter,
            CancellationTokenSource cts,
            int currentDepth)
        {
            if (Redaction.IsSafeToCallToString(type) || source == null)
            {
                return;
            }

            if (currentDepth >= _maximumDepthOfMembersToCopy)
            {
                WriteNotCapturedReason(jsonWriter, NotCapturedReason.depth);
                return;
            }

            WriteFields(source, type, jsonWriter, cts, currentDepth, writeStaticFields: false);
        }

        private static void WriteFields(object source, Type type, JsonWriter jsonWriter, CancellationTokenSource cts, int currentDepth, bool writeStaticFields)
        {
            var selector = SnapshotSerializerFieldsAndPropsSelector.CreateDeepClonerFieldsAndPropsSelector(type);
            var fields = selector.GetFieldsAndProps(type, source, cts);
            WriteFieldsInternal(source, jsonWriter, cts, currentDepth, fields.Where(f => IsStatic(f) == writeStaticFields), writeStaticFields ? "staticFields" : "fields");
        }

        private static bool IsStatic(MemberInfo arg) =>
            (arg is FieldInfo fieldInfo && fieldInfo.IsStatic) ||
            (arg is PropertyInfo propertyInfo && propertyInfo.GetMethod.IsStatic);

        private static void WriteFieldsInternal(object source, JsonWriter jsonWriter, CancellationTokenSource cts, int currentDepth, IEnumerable<MemberInfo> fields, string fieldsObjectName)
        {
            int index = 0;
            var isFieldCountReached = false;
            foreach (var field in fields)
            {
                var fieldOrPropertyName = GetAutoPropertyOrFieldName(field.Name);

                if (index >= _maximumNumberOfFieldsToCopy)
                {
                    isFieldCountReached = true;
                    break;
                }

                if (!TryGetValue(field, source, out var value, out var type))
                {
                    continue;
                }

                if (index == 0)
                {
                    jsonWriter.WritePropertyName(fieldsObjectName);
                    jsonWriter.WriteStartObject();
                }

                index++;
                var serialized = SerializeInternal(
                    value,
                    type,
                    jsonWriter,
                    cts,
                    currentDepth + 1,
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

            if (isFieldCountReached)
            {
                WriteNotCapturedReason(jsonWriter, NotCapturedReason.fieldCount);
            }
        }

        private static void SerializeEnumerable(
           object source,
           Type type,
           JsonWriter jsonWriter,
           IEnumerable enumerable,
           int currentDepth,
           CancellationTokenSource cts)
        {
            try
            {
                var isDictionary = Redaction.IsSupportedDictionary(source);
                if (source is ICollection collection)
                {
                    jsonWriter.WritePropertyName("type");
                    jsonWriter.WriteValue(type.Name);
                    jsonWriter.WritePropertyName("size");
                    jsonWriter.WriteValue(collection.Count);
                    jsonWriter.WritePropertyName(isDictionary ? "entries" : "elements");
                    jsonWriter.WriteStartArray();

                    var itemIndex = 0;
                    var enumerator = enumerable.GetEnumerator();

                    while (itemIndex < _maximumNumberOfItemsInCollectionToCopy &&
                           enumerator.MoveNext())
                    {
                        cts.Token.ThrowIfCancellationRequested();
                        if (enumerator.Current == null)
                        {
                            break;
                        }

                        bool serialized;
                        if (isDictionary)
                        {
                            serialized = SerializeKeyValuePair(enumerator.Current, jsonWriter, cts, currentDepth);
                        }
                        else
                        {
                            serialized = SerializeInternal(
                                enumerator.Current,
                                enumerator.Current.GetType(),
                                jsonWriter,
                                cts,
                                currentDepth,
                                variableName: null,
                                fieldsOnly: false);
                        }

                        itemIndex++;
                        if (!serialized)
                        {
                            break;
                        }
                    }

                    jsonWriter.WriteEndArray();

                    if (enumerator.MoveNext())
                    {
                        WriteNotCapturedReason(jsonWriter, NotCapturedReason.collectionSize);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                WriteNotCapturedReason(jsonWriter, NotCapturedReason.timeout);
                jsonWriter.WriteEndArray();
            }
            catch (InvalidOperationException e)
            {
                // Collection was modified, enumeration operation may not execute
                Log.Error<int>(e, "Error serializing enumerable (Collection was modified) Depth={CurrentDepth}", currentDepth);
                jsonWriter.WriteEndArray();
            }
            catch (Exception e)
            {
                Log.Error<int>(e, "Error serializing enumerable Depth={CurrentDepth}", currentDepth);
            }
        }

        private static bool SerializeKeyValuePair(
            object current,
            JsonWriter jsonWriter,
            CancellationTokenSource cts,
            int currentDepth)
        {
            var reflectionObject = ReflectionObject.Create(current.GetType(), "Key", "Value");
            jsonWriter.WriteStartArray();

            bool serializedKey = SerializeInternal(reflectionObject.GetValue(current, "Key"), reflectionObject.GetType("Key"), jsonWriter, cts, currentDepth, variableName: null, fieldsOnly: false);
            bool serializedValue = SerializeInternal(reflectionObject.GetValue(current, "Value"), reflectionObject.GetType("Value"), jsonWriter, cts, currentDepth, variableName: null, fieldsOnly: false);

            jsonWriter.WriteEndArray();
            return serializedKey;
        }

        private static void WriteNotCapturedReason(JsonWriter writer, NotCapturedReason notCapturedReason)
        {
            writer.WritePropertyName("notCapturedReason");
            writer.WriteValue(Enum.GetName(typeof(NotCapturedReason), notCapturedReason));
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
                                field.DeclaringType?.ContainsGenericParameters == true ||
                                field.ReflectedType?.ContainsGenericParameters == true)
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
                            Log.Error(nameof(DebuggerSnapshotSerializer) + "." + nameof(TryGetValue) + ": Can't get value of {Name}. Unsupported member info {Type}", fieldOrProp.Name, fieldOrProp.GetType());
                            break;
                        }
                }
            }
            catch (Exception e)
            {
                Log.Error(e, nameof(DebuggerSnapshotSerializer) + "." + nameof(TryGetValue));
            }

            return false;
        }

        private static CancellationTokenSource CreateCancellationTimeout()
        {
            var cts = new CancellationTokenSource();
            if (!System.Diagnostics.Debugger.IsAttached)
            {
                cts.CancelAfter(_maximumSerializationTime);
            }

            return cts;
        }
    }
}
