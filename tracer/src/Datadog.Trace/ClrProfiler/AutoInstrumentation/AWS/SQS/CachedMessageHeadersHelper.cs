// <copyright file="CachedMessageHeadersHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection.Emit;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.Shared;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Util;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.SQS
{
    /// <summary>
    /// Allows the creation of MessageAttributes and MessageAttributeValue with a type corresponding to the one given as template parameter
    /// </summary>
    /// <typeparam name="TMarkerType">can be any type in the same assembly as the Attributes we want to create.</typeparam>
    internal sealed class CachedMessageHeadersHelper<TMarkerType> : IMessageHeadersHelper
    {
        private const string StringDataType = "String";

        // ReSharper disable StaticMemberInGenericType
        // there will be one instance of those fields per template type
        private static readonly Func<string, object> MessageAttributeValueCreator;
        private static readonly ActivatorHelper DictionaryActivator;
        // ReSharper restore StaticMemberInGenericType

        public static readonly CachedMessageHeadersHelper<TMarkerType> Instance;

        private CachedMessageHeadersHelper()
        {
        }

        static CachedMessageHeadersHelper()
        {
            // Initialize delegate for creating a MessageAttributeValue object
            var messageAttributeValueType = typeof(TMarkerType).Assembly.GetType("Amazon.SQS.Model.MessageAttributeValue");
            var messageAttributeValueCtor = messageAttributeValueType.GetConstructor(System.Type.EmptyTypes);

            DynamicMethod createMessageAttributeValueMethod = new DynamicMethod(
                $"KafkaCachedMessageHeadersHelpers",
                messageAttributeValueType,
                parameterTypes: new Type[] { typeof(string) },
                typeof(DuckType).Module,
                true);

            ILGenerator messageAttributeIL = createMessageAttributeValueMethod.GetILGenerator();
            messageAttributeIL.Emit(OpCodes.Newobj, messageAttributeValueCtor);

            messageAttributeIL.Emit(OpCodes.Dup);
            messageAttributeIL.Emit(OpCodes.Ldstr, StringDataType);
            messageAttributeIL.Emit(OpCodes.Callvirt, messageAttributeValueType.GetProperty("DataType").GetSetMethod());

            messageAttributeIL.Emit(OpCodes.Dup);
            messageAttributeIL.Emit(OpCodes.Ldarg_0);
            messageAttributeIL.Emit(OpCodes.Callvirt, messageAttributeValueType.GetProperty("StringValue").GetSetMethod());

            messageAttributeIL.Emit(OpCodes.Ret);

            MessageAttributeValueCreator = (Func<string, object>)createMessageAttributeValueMethod.CreateDelegate(typeof(Func<string, object>));

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
            return MessageAttributeValueCreator(value);
        }
    }
}
