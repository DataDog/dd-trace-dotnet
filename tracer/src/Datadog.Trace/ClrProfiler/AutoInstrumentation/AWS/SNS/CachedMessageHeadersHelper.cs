// <copyright file="CachedMessageHeadersHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.Shared;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Util;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.SNS
{
    /// <summary>
    /// Allows the creation of MessageAttributes and MessageAttributeValue with a type corresponding to the one given as template parameter
    /// </summary>
    /// <typeparam name="TMarkerType">can be any type in the same assembly as the Attributes we want to create.</typeparam>
    internal sealed class CachedMessageHeadersHelper<TMarkerType> : IMessageHeadersHelper
    {
        // We have to use Binary for SNS because passing JSON payloads with String type makes subscription filter policies fail silently.
        // see https://github.com/DataDog/datadog-lambda-js/pull/269 for more details.
        private const string StringDataType = "Binary";

        // ReSharper disable StaticMemberInGenericType
        // there will be one instance of those fields per template type
        private static readonly Func<MemoryStream, object> MessageAttributeValueCreator;
        private static readonly ActivatorHelper DictionaryActivator;
        // ReSharper restore StaticMemberInGenericType

        public static readonly CachedMessageHeadersHelper<TMarkerType> Instance;

        private CachedMessageHeadersHelper()
        {
        }

        static CachedMessageHeadersHelper()
        {
            // Initialize delegate for creating a MessageAttributeValue object
            var messageAttributeValueType = typeof(TMarkerType).Assembly.GetType("Amazon.SimpleNotificationService.Model.MessageAttributeValue");
            var messageAttributeValueCtor = messageAttributeValueType.GetConstructor(System.Type.EmptyTypes);

            DynamicMethod createMessageAttributeValueMethod = new DynamicMethod(
                $"SnsCachedMessageHeadersHelpers",
                messageAttributeValueType,
                parameterTypes: new Type[] { typeof(MemoryStream) },
                typeof(DuckType).Module,
                true);

            ILGenerator messageAttributeIL = createMessageAttributeValueMethod.GetILGenerator();
            messageAttributeIL.Emit(OpCodes.Newobj, messageAttributeValueCtor);

            messageAttributeIL.Emit(OpCodes.Dup);
            messageAttributeIL.Emit(OpCodes.Ldstr, StringDataType);
            messageAttributeIL.Emit(OpCodes.Callvirt, messageAttributeValueType.GetProperty("DataType").GetSetMethod());

            messageAttributeIL.Emit(OpCodes.Dup);
            messageAttributeIL.Emit(OpCodes.Ldarg_0);
            messageAttributeIL.Emit(OpCodes.Callvirt, messageAttributeValueType.GetProperty("BinaryValue").GetSetMethod());

            messageAttributeIL.Emit(OpCodes.Ret);

            MessageAttributeValueCreator = (Func<MemoryStream, object>)createMessageAttributeValueMethod.CreateDelegate(typeof(Func<MemoryStream, object>));

            // Initialize delegate for creating a Dictionary<string, MessageAttributeValue> object
            DictionaryActivator = new ActivatorHelper(typeof(Dictionary<,>).MakeGenericType(typeof(string), messageAttributeValueType));

            Instance = new CachedMessageHeadersHelper<TMarkerType>();
        }

        public IDictionary CreateMessageAttributes()
        {
            return (IDictionary)DictionaryActivator.CreateInstance();
        }

        public object CreateMessageAttributeValue(string value)
        {
            var bytes = Encoding.UTF8.GetBytes(value);
            var stream = new MemoryStream(bytes);
            return MessageAttributeValueCreator(stream);
        }
    }
}
