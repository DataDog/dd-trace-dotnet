// <copyright file="WrongReturnTypeStruct.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.DuckTyping.Tests.Errors.Fields.ReferenceType.ProxiesDefinitions.WrongReturnType
{
    public struct WrongReturnTypeStruct
    {
        [DuckCopy]
        public struct PublicStaticReadonlyReferenceTypeFieldStruct
        {
            [DuckField(Name = "_publicStaticReadonlyReferenceTypeField")]
            public int PublicStaticReadonlyReferenceTypeField;
        }

        [DuckCopy]
        public struct InternalStaticReadonlyReferenceTypeFieldStruct
        {
            [DuckField(Name = "_internalStaticReadonlyReferenceTypeField")]
            public int InternalStaticReadonlyReferenceTypeField;
        }

        [DuckCopy]
        public struct ProtectedStaticReadonlyReferenceTypeFieldStruct
        {
            [DuckField(Name = "_protectedStaticReadonlyReferenceTypeField")]
            public int ProtectedStaticReadonlyReferenceTypeField;
        }

        [DuckCopy]
        public struct PrivateStaticReadonlyReferenceTypeFieldStruct
        {
            [DuckField(Name = "_privateStaticReadonlyReferenceTypeField")]
            public int PrivateStaticReadonlyReferenceTypeField;
        }

        // *

        [DuckCopy]
        public struct PublicStaticReferenceTypeFieldStruct
        {
            [DuckField(Name = "_publicStaticReferenceTypeField")]
            public int PublicStaticReferenceTypeField;
        }

        [DuckCopy]
        public struct InternalStaticReferenceTypeFieldStruct
        {
            [DuckField(Name = "_internalStaticReferenceTypeField")]
            public int InternalStaticReferenceTypeField;
        }

        [DuckCopy]
        public struct ProtectedStaticReferenceTypeFieldStruct
        {
            [DuckField(Name = "_protectedStaticReferenceTypeField")]
            public int ProtectedStaticReferenceTypeField;
        }

        [DuckCopy]
        public struct PrivateStaticReferenceTypeFieldStruct
        {
            [DuckField(Name = "_privateStaticReferenceTypeField")]
            public int PrivateStaticReferenceTypeField;
        }

        // *

        [DuckCopy]
        public struct PublicReadonlyReferenceTypeFieldStruct
        {
            [DuckField(Name = "_publicReadonlyReferenceTypeField")]
            public int PublicReadonlyReferenceTypeField;
        }

        [DuckCopy]
        public struct InternalReadonlyReferenceTypeFieldStruct
        {
            [DuckField(Name = "_internalReadonlyReferenceTypeField")]
            public int InternalReadonlyReferenceTypeField;
        }

        [DuckCopy]
        public struct ProtectedReadonlyReferenceTypeFieldStruct
        {
            [DuckField(Name = "_protectedReadonlyReferenceTypeField")]
            public int ProtectedReadonlyReferenceTypeField;
        }

        [DuckCopy]
        public struct PrivateReadonlyReferenceTypeFieldStruct
        {
            [DuckField(Name = "_privateReadonlyReferenceTypeField")]
            public int PrivateReadonlyReferenceTypeField;
        }

        // *

        [DuckCopy]
        public struct PublicReferenceTypeFieldStruct
        {
            [DuckField(Name = "_publicReferenceTypeField")]
            public int PublicReferenceTypeField;
        }

        [DuckCopy]
        public struct InternalReferenceTypeFieldStruct
        {
            [DuckField(Name = "_internalReferenceTypeField")]
            public int InternalReferenceTypeField;
        }

        [DuckCopy]
        public struct ProtectedReferenceTypeFieldStruct
        {
            [DuckField(Name = "_protectedReferenceTypeField")]
            public int ProtectedReferenceTypeField;
        }

        [DuckCopy]
        public struct PrivateReferenceTypeFieldStruct
        {
            [DuckField(Name = "_privateReferenceTypeField")]
            public int PrivateReferenceTypeField;
        }
    }
}
