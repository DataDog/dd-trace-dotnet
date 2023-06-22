// <copyright file="CachedMessageHeadersHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.SNS
{
    internal static class CachedMessageHeadersHelper<TMarkerType>
    {
        private const string BinaryDataType = "Binary";

        private static readonly Func<string, object> _createMessageAttributeValue;
        private static readonly Func<IDictionary> _createDict;

        static CachedMessageHeadersHelper()
        {
            // Initialize delegate for creating a MessageAttributeValue object
            Console.WriteLine("getTypeAwsSimpleNoti :) ");
            var messageAttributeValueType = typeof(TMarkerType).Assembly.GetType("Amazon.SimpleNotificationService.Model.MessageAttributeValue");
            Console.WriteLine("between");
            var messageAttributeValueCtor = messageAttributeValueType.GetConstructor(System.Type.EmptyTypes);
            Console.WriteLine("after get constructor");
            DynamicMethod createMessageAttributeValueMethod = new DynamicMethod(
                $"SnsCachedMessageHeadersHelpers",
                messageAttributeValueType,
                parameterTypes: new Type[] { typeof(string) },
                typeof(DuckType).Module,
                true);
            Console.WriteLine("b4 ILGen");
            ILGenerator messageAttributeIL = createMessageAttributeValueMethod.GetILGenerator();
            messageAttributeIL.Emit(OpCodes.Newobj, messageAttributeValueCtor);

            messageAttributeIL.Emit(OpCodes.Dup);
            messageAttributeIL.Emit(OpCodes.Ldstr, BinaryDataType);
            messageAttributeIL.Emit(OpCodes.Callvirt, messageAttributeValueType.GetProperty("DataType").GetSetMethod());
            Console.WriteLine("after datatype opcode");
            messageAttributeIL.Emit(OpCodes.Dup);
            messageAttributeIL.Emit(OpCodes.Ldarg_0);
            messageAttributeIL.Emit(OpCodes.Callvirt, messageAttributeValueType.GetProperty("BinaryValue").GetSetMethod());

            messageAttributeIL.Emit(OpCodes.Ret);
            Console.WriteLine("after BinaryValue opcode");
            _createMessageAttributeValue = (Func<string, object>)createMessageAttributeValueMethod.CreateDelegate(typeof(Func<string, object>));

            // Initialize delegate for creating a Dictionary<string, MessageAttributeValue> object
            var genericDictType = typeof(Dictionary<,>);
            var constructedDictType = genericDictType.MakeGenericType(new Type[] { typeof(string), messageAttributeValueType });
            ConstructorInfo dictionaryCtor = constructedDictType.GetConstructor(System.Type.EmptyTypes);

            DynamicMethod createDictMethod = new DynamicMethod(
                $"SnsCachedMessageHeadersHelpers",
                constructedDictType,
                null,
                typeof(DuckType).Module,
                true);

            ILGenerator dictIL = createDictMethod.GetILGenerator();
            dictIL.Emit(OpCodes.Newobj, dictionaryCtor);
            dictIL.Emit(OpCodes.Ret);

            _createDict = (Func<IDictionary>)createDictMethod.CreateDelegate(typeof(Func<IDictionary>));
            Console.WriteLine("all reflect done");
        }

        public static IDictionary CreateMessageAttributes()
        {
            return _createDict();
        }

        public static object CreateMessageAttributeValue(string value)
        {
            return _createMessageAttributeValue(value);
        }
    }
}
