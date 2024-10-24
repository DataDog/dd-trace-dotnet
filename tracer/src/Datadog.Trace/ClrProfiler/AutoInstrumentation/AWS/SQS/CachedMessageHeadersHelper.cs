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
    internal class CachedMessageHeadersHelper<TMarkerType> : IMessageHeadersHelper
    {
        private const string StringDataType = "String";

        private static readonly Func<string, object> _createMessageAttributeValue;
        private static readonly ActivatorHelper DictionaryActivator;

        public static readonly CachedMessageHeadersHelper<TMarkerType> Instance = new();

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

            _createMessageAttributeValue = (Func<string, object>)createMessageAttributeValueMethod.CreateDelegate(typeof(Func<string, object>));

            // Initialize delegate for creating a Dictionary<string, MessageAttributeValue> object
            DictionaryActivator = new ActivatorHelper(typeof(Dictionary<,>).MakeGenericType(typeof(string), messageAttributeValueType));
        }

        public IDictionary CreateMessageAttributes()
        {
            return (IDictionary)DictionaryActivator.CreateInstance();
        }

        public object CreateMessageAttributeValue(string value)
        {
            return _createMessageAttributeValue(value);
        }
    }
}
