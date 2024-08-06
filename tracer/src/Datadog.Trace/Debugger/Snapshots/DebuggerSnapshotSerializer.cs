// <copyright file="DebuggerSnapshotSerializer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using Datadog.Trace.Debugger.Expressions;
using Datadog.Trace.Logging;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json.Utilities;

namespace Datadog.Trace.Debugger.Snapshots
{
    internal static partial class DebuggerSnapshotSerializer
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(DebuggerSnapshotSerializer));

        private static int _maximumSerializationTime = DebuggerSettings.DefaultMaxSerializationTimeInMilliseconds;

        internal static void SetConfig(DebuggerSettings debuggerSettings)
        {
            _maximumSerializationTime = debuggerSettings.MaxSerializationTimeInMilliseconds;
        }

        /// <summary>
        /// Note: implemented recursively. We might want to consider an iterative approach for performance gain (Serialize takes part in the MethodDebuggerInvoker process).
        /// </summary>
        internal static void Serialize(
            object source,
            Type type,
            string name,
            JsonWriter jsonWriter,
            CaptureLimitInfo limitInfo)
        {
            using var cts = CreateCancellationTimeout();
            SerializeInternal(source, type, jsonWriter, cts, currentDepth: 0, name, fieldsOnly: false, limitInfo);
        }

        public static void SerializeStaticFields(Type declaringType, JsonTextWriter jsonWriter, CaptureLimitInfo limitInfo)
        {
            using var cts = CreateCancellationTimeout();
            WriteFields(null, declaringType, jsonWriter, cts, currentDepth: 0, writeStaticFields: true, limitInfo);
        }

        private static bool SerializeInternal(
            object source,
            Type type,
            JsonWriter jsonWriter,
            CancellationTokenSource cts,
            int currentDepth,
            string variableName,
            bool fieldsOnly,
            CaptureLimitInfo limitInfo)
        {
            try
            {
                if (Redaction.ShouldRedact(variableName, type, out var redactionReason))
                {
                    if (variableName != null)
                    {
                        jsonWriter.WritePropertyName(variableName);
                    }

                    var notCapturedReason = redactionReason == RedactionReason.Identifier ? NotCapturedReason.redactedIdent : NotCapturedReason.redactedType;

                    jsonWriter.WriteStartObject();
                    jsonWriter.WritePropertyName("type");
                    jsonWriter.WriteValue(type.Name);
                    WriteNotCapturedReason(jsonWriter, notCapturedReason);
                    jsonWriter.WriteEndObject();

                    return true;
                }

                if (source is UnreachableLocal unreachable)
                {
                    jsonWriter.WritePropertyName(variableName);
                    jsonWriter.WriteStartObject();
                    jsonWriter.WritePropertyName("type");
                    jsonWriter.WriteValue(type.Name);
                    WriteNotCapturedReason(jsonWriter, unreachable.Reason);
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
                    SerializeEnumerable(source, type, jsonWriter, enumerable, currentDepth, cts, limitInfo);
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
                    fieldsOnly,
                    limitInfo);
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
            bool fieldsOnly,
            CaptureLimitInfo limitInfo)
        {
            if (!fieldsOnly)
            {
                WriteTypeAndValue(source, type, jsonWriter, variableName, limitInfo);
            }

            try
            {
                SerializeInstanceFieldsInternal(source, type, jsonWriter, cts, currentDepth, limitInfo);
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
            string variableName,
            CaptureLimitInfo limitInfo)
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
                var stringValueTruncated = stringValue?.Length < limitInfo.MaxLength ? stringValue : stringValue?.Substring(0, limitInfo.MaxLength);
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
            int currentDepth,
            CaptureLimitInfo limitInfo)
        {
            if (Redaction.IsSafeToCallToString(type) || source == null)
            {
                return;
            }

            if (currentDepth >= limitInfo.MaxReferenceDepth)
            {
                WriteNotCapturedReason(jsonWriter, NotCapturedReason.depth);
                return;
            }

            WriteFields(source, type, jsonWriter, cts, currentDepth, writeStaticFields: false, limitInfo);
        }

        private static void WriteFields(object source, Type type, JsonWriter jsonWriter, CancellationTokenSource cts, int currentDepth, bool writeStaticFields, CaptureLimitInfo limitInfo)
        {
            var selector = SnapshotSerializerFieldsAndPropsSelector.CreateDeepClonerFieldsAndPropsSelector(type);
            var fields = selector.GetFieldsAndProps(type, source, cts);
            WriteFieldsInternal(source, jsonWriter, cts, currentDepth, fields.Where(f => IsStatic(f) == writeStaticFields), writeStaticFields ? "staticFields" : "fields", limitInfo);
        }

        private static bool IsStatic(MemberInfo arg) =>
            (arg is FieldInfo fieldInfo && fieldInfo.IsStatic) ||
            (arg is PropertyInfo propertyInfo && propertyInfo.GetMethod.IsStatic);

        private static void WriteFieldsInternal(object source, JsonWriter jsonWriter, CancellationTokenSource cts, int currentDepth, IEnumerable<MemberInfo> fields, string fieldsObjectName, CaptureLimitInfo limitInfo)
        {
            int index = 0;
            var isFieldCountReached = false;
            foreach (var field in fields)
            {
                var fieldOrPropertyName = GetAutoPropertyOrFieldName(field.Name);

                if (index >= limitInfo.MaxFieldCount)
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
                    fieldsOnly: false,
                    limitInfo);

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
           CancellationTokenSource cts,
           CaptureLimitInfo limitInfo)
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

                    while (itemIndex < limitInfo.MaxCollectionSize && enumerator.MoveNext())
                    {
                        cts.Token.ThrowIfCancellationRequested();
                        if (enumerator.Current == null)
                        {
                            break;
                        }

                        bool serialized;
                        if (isDictionary)
                        {
                            serialized = SerializeKeyValuePair(enumerator.Current, jsonWriter, cts, currentDepth, limitInfo);
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
                                fieldsOnly: false,
                                limitInfo);
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
            int currentDepth,
            CaptureLimitInfo limitInfo)
        {
            var reflectionObject = ReflectionObject.Create(current.GetType(), "Key", "Value");
            jsonWriter.WriteStartArray();

            bool serializedKey = SerializeInternal(reflectionObject.GetValue(current, "Key"), reflectionObject.GetType("Key"), jsonWriter, cts, currentDepth, variableName: null, fieldsOnly: false, limitInfo);
            bool serializedValue = SerializeInternal(reflectionObject.GetValue(current, "Value"), reflectionObject.GetType("Value"), jsonWriter, cts, currentDepth, variableName: null, fieldsOnly: false, limitInfo);

            jsonWriter.WriteEndArray();
            return serializedKey;
        }

        private static void WriteNotCapturedReason(JsonWriter writer, NotCapturedReason notCapturedReason)
        {
            WriteNotCapturedReason(writer, Enum.GetName(typeof(NotCapturedReason), notCapturedReason));
        }

        private static void WriteNotCapturedReason(JsonWriter writer, string notCapturedReason)
        {
            writer.WritePropertyName("notCapturedReason");
            writer.WriteValue(notCapturedReason);
        }

        private static string GetAutoPropertyOrFieldName(string fieldName)
        {
            const string prefix = "<";
            const string suffix = ">k__BackingField";
            var match = Regex.Match(fieldName, $"{prefix}(.+?){suffix}");
            return match.Success ? match.Groups[1].Value : fieldName;
        }

        internal static bool TryGetValue(MemberInfo fieldOrProp, object source, out object value, [NotNullWhen(true)] out Type type)
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
                                field.ReflectedType?.ContainsGenericParameters == true ||
                                field is System.Reflection.Emit.FieldBuilder)
                            {
                                return false;
                            }

                            if (source != null || field.IsStatic)
                            {
                                type = field.FieldType;
                                value = field.GetValue(source);
                                return true;
                            }

                            break;
                        }

                    case PropertyInfo property:
                        {
                            if (property.PropertyType.ContainsGenericParameters ||
                                property.DeclaringType?.ContainsGenericParameters == true ||
                                property.ReflectedType?.ContainsGenericParameters == true)
                            {
                                return false;
                            }

                            if (source != null || property.GetMethod?.IsStatic == true)
                            {
                                var getMethod = property.GetGetMethod(true);
                                if (getMethod == null || getMethod is System.Reflection.Emit.MethodBuilder)
                                {
                                    return false;
                                }

                                type = property.PropertyType;
                                value = property.GetMethod?.Invoke(source, Array.Empty<object>());
                                return true;
                            }

                            break;
                        }

                    default:
                        {
                            Log.Error(nameof(DebuggerSnapshotSerializer) + "." + nameof(TryGetValue) + ": Can't get value of {Member} from {Source}. Unsupported member info.", GetMemberInfo(fieldOrProp), source?.GetType().FullName);
                            break;
                        }
                }
            }
            catch (Exception e)
            {
                Log.Error(e, nameof(DebuggerSnapshotSerializer) + "." + nameof(TryGetValue) + ": Can't get value of {Member} from {Source}", GetMemberInfo(fieldOrProp), source?.GetType().FullName);
            }

            return false;

            string GetMemberInfo(MemberInfo memberInfo)
            {
                try
                {
                    if (memberInfo is FieldInfo field)
                    {
                        return $"Type: {field.FieldType.FullName}, Name: {field.Name}, Attributes: {field.Attributes.ToString()}";
                    }

                    if (memberInfo is PropertyInfo property)
                    {
                        return $"Type: {property.PropertyType.FullName}, Name: {property.Name}, Attributes: {property.Attributes.ToString()}";
                    }

                    return $"Type: {memberInfo.MemberType}, Name: {memberInfo.Name}";
                }
                catch
                {
                    return "Unknown";
                }
            }
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
