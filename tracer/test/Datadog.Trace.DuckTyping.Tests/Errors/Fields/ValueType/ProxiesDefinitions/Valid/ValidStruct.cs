// <copyright file="ValidStruct.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.DuckTyping.Tests.Errors.Fields.ValueType.ProxiesDefinitions.Valid
{
    public struct ValidStruct
    {
        [DuckCopy]
        public struct PublicStaticReadonlyValueTypeFieldStruct
        {
            [DuckField(Name = "_publicStaticReadonlyValueTypeField")]
            public int PublicStaticReadonlyValueTypeField;
        }

        [DuckCopy]
        public struct InternalStaticReadonlyValueTypeFieldStruct
        {
            [DuckField(Name = "_internalStaticReadonlyValueTypeField")]
            public int InternalStaticReadonlyValueTypeField;
        }

        [DuckCopy]
        public struct ProtectedStaticReadonlyValueTypeFieldStruct
        {
            [DuckField(Name = "_protectedStaticReadonlyValueTypeField")]
            public int ProtectedStaticReadonlyValueTypeField;
        }

        [DuckCopy]
        public struct PrivateStaticReadonlyValueTypeFieldStruct
        {
            [DuckField(Name = "_privateStaticReadonlyValueTypeField")]
            public int PrivateStaticReadonlyValueTypeField;
        }

        // *

        [DuckCopy]
        public struct PublicStaticValueTypeFieldStruct
        {
            [DuckField(Name = "_publicStaticValueTypeField")]
            public int PublicStaticValueTypeField;
        }

        [DuckCopy]
        public struct InternalStaticValueTypeFieldStruct
        {
            [DuckField(Name = "_internalStaticValueTypeField")]
            public int InternalStaticValueTypeField;
        }

        [DuckCopy]
        public struct ProtectedStaticValueTypeFieldStruct
        {
            [DuckField(Name = "_protectedStaticValueTypeField")]
            public int ProtectedStaticValueTypeField;
        }

        [DuckCopy]
        public struct PrivateStaticValueTypeFieldStruct
        {
            [DuckField(Name = "_privateStaticValueTypeField")]
            public int PrivateStaticValueTypeField;
        }

        // *

        [DuckCopy]
        public struct PublicReadonlyValueTypeFieldStruct
        {
            [DuckField(Name = "_publicReadonlyValueTypeField")]
            public int PublicReadonlyValueTypeField;
        }

        [DuckCopy]
        public struct InternalReadonlyValueTypeFieldStruct
        {
            [DuckField(Name = "_internalReadonlyValueTypeField")]
            public int InternalReadonlyValueTypeField;
        }

        [DuckCopy]
        public struct ProtectedReadonlyValueTypeFieldStruct
        {
            [DuckField(Name = "_protectedReadonlyValueTypeField")]
            public int ProtectedReadonlyValueTypeField;
        }

        [DuckCopy]
        public struct PrivateReadonlyValueTypeFieldStruct
        {
            [DuckField(Name = "_privateReadonlyValueTypeField")]
            public int PrivateReadonlyValueTypeField;
        }

        // *

        [DuckCopy]
        public struct PublicValueTypeFieldStruct
        {
            [DuckField(Name = "_publicValueTypeField")]
            public int PublicValueTypeField;
        }

        [DuckCopy]
        public struct InternalValueTypeFieldStruct
        {
            [DuckField(Name = "_internalValueTypeField")]
            public int InternalValueTypeField;
        }

        [DuckCopy]
        public struct ProtectedValueTypeFieldStruct
        {
            [DuckField(Name = "_protectedValueTypeField")]
            public int ProtectedValueTypeField;
        }

        [DuckCopy]
        public struct PrivateValueTypeFieldStruct
        {
            [DuckField(Name = "_privateValueTypeField")]
            public int PrivateValueTypeField;
        }

        // *

        [DuckCopy]
        public struct PublicStaticNullableIntFieldStruct
        {
            [DuckField(Name = "_publicStaticNullableIntField")]
            public int? PublicStaticNullableIntField;
        }

        [DuckCopy]
        public struct PrivateStaticNullableIntFieldStruct
        {
            [DuckField(Name = "_privateStaticNullableIntField")]
            public int? PrivateStaticNullableIntField;
        }

        [DuckCopy]
        public struct PublicNullableIntFieldStruct
        {
            [DuckField(Name = "_publicNullableIntField")]
            public int? PublicNullableIntField;
        }

        [DuckCopy]
        public struct PrivateNullableIntFieldStruct
        {
            [DuckField(Name = "_privateNullableIntField")]
            public int? PrivateNullableIntField;
        }
    }
}
