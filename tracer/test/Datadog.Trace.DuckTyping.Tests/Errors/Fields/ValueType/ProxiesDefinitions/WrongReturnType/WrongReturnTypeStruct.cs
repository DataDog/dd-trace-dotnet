// <copyright file="WrongReturnTypeStruct.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.DuckTyping.Tests.Errors.Fields.ValueType.ProxiesDefinitions.WrongReturnType
{
    public struct WrongReturnTypeStruct
    {
        [DuckCopy]
        public struct PublicStaticReadonlyValueTypeFieldStruct
        {
            [DuckField(Name = "_publicStaticReadonlyValueTypeField")]
            public char PublicStaticReadonlyValueTypeField;
        }

        [DuckCopy]
        public struct InternalStaticReadonlyValueTypeFieldStruct
        {
            [DuckField(Name = "_internalStaticReadonlyValueTypeField")]
            public char InternalStaticReadonlyValueTypeField;
        }

        [DuckCopy]
        public struct ProtectedStaticReadonlyValueTypeFieldStruct
        {
            [DuckField(Name = "_protectedStaticReadonlyValueTypeField")]
            public char ProtectedStaticReadonlyValueTypeField;
        }

        [DuckCopy]
        public struct PrivateStaticReadonlyValueTypeFieldStruct
        {
            [DuckField(Name = "_privateStaticReadonlyValueTypeField")]
            public char PrivateStaticReadonlyValueTypeField;
        }

        // *

        [DuckCopy]
        public struct PublicStaticValueTypeFieldStruct
        {
            [DuckField(Name = "_publicStaticValueTypeField")]
            public char PublicStaticValueTypeField;
        }

        [DuckCopy]
        public struct InternalStaticValueTypeFieldStruct
        {
            [DuckField(Name = "_internalStaticValueTypeField")]
            public char InternalStaticValueTypeField;
        }

        [DuckCopy]
        public struct ProtectedStaticValueTypeFieldStruct
        {
            [DuckField(Name = "_protectedStaticValueTypeField")]
            public char ProtectedStaticValueTypeField;
        }

        [DuckCopy]
        public struct PrivateStaticValueTypeFieldStruct
        {
            [DuckField(Name = "_privateStaticValueTypeField")]
            public char PrivateStaticValueTypeField;
        }

        // *

        [DuckCopy]
        public struct PublicReadonlyValueTypeFieldStruct
        {
            [DuckField(Name = "_publicReadonlyValueTypeField")]
            public char PublicReadonlyValueTypeField;
        }

        [DuckCopy]
        public struct InternalReadonlyValueTypeFieldStruct
        {
            [DuckField(Name = "_internalReadonlyValueTypeField")]
            public char InternalReadonlyValueTypeField;
        }

        [DuckCopy]
        public struct ProtectedReadonlyValueTypeFieldStruct
        {
            [DuckField(Name = "_protectedReadonlyValueTypeField")]
            public char ProtectedReadonlyValueTypeField;
        }

        [DuckCopy]
        public struct PrivateReadonlyValueTypeFieldStruct
        {
            [DuckField(Name = "_privateReadonlyValueTypeField")]
            public char PrivateReadonlyValueTypeField;
        }

        // *

        [DuckCopy]
        public struct PublicValueTypeFieldStruct
        {
            [DuckField(Name = "_publicValueTypeField")]
            public char PublicValueTypeField;
        }

        [DuckCopy]
        public struct InternalValueTypeFieldStruct
        {
            [DuckField(Name = "_internalValueTypeField")]
            public char InternalValueTypeField;
        }

        [DuckCopy]
        public struct ProtectedValueTypeFieldStruct
        {
            [DuckField(Name = "_protectedValueTypeField")]
            public char ProtectedValueTypeField;
        }

        [DuckCopy]
        public struct PrivateValueTypeFieldStruct
        {
            [DuckField(Name = "_privateValueTypeField")]
            public char PrivateValueTypeField;
        }

        // *

        [DuckCopy]
        public struct PublicStaticNullableIntFieldStruct
        {
            [DuckField(Name = "_publicStaticNullableIntField")]
            public char? PublicStaticNullableIntField;
        }

        [DuckCopy]
        public struct PrivateStaticNullableIntFieldStruct
        {
            [DuckField(Name = "_privateStaticNullableIntField")]
            public char? PrivateStaticNullableIntField;
        }

        [DuckCopy]
        public struct PublicNullableIntFieldStruct
        {
            [DuckField(Name = "_publicNullableIntField")]
            public char? PublicNullableIntField;
        }

        [DuckCopy]
        public struct PrivateNullableIntFieldStruct
        {
            [DuckField(Name = "_privateNullableIntField")]
            public char? PrivateNullableIntField;
        }
    }
}
