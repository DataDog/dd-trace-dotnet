// <copyright file="WrongFieldNameStruct.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.DuckTyping.Tests.Errors.Fields.ReferenceType.ProxiesDefinitions.WrongFieldName
{
    public struct WrongFieldNameStruct
    {
        [DuckCopy]
        public struct PublicStaticReadonlyReferenceTypeFieldStruct
        {
            [DuckField(Name = "publicStaticReadonlyReferenceTypeField")]
            public string PublicStaticReadonlyReferenceTypeField;
        }

        [DuckCopy]
        public struct InternalStaticReadonlyReferenceTypeFieldStruct
        {
            [DuckField(Name = "internalStaticReadonlyReferenceTypeField")]
            public string InternalStaticReadonlyReferenceTypeField;
        }

        [DuckCopy]
        public struct ProtectedStaticReadonlyReferenceTypeFieldStruct
        {
            [DuckField(Name = "protectedStaticReadonlyReferenceTypeField")]
            public string ProtectedStaticReadonlyReferenceTypeField;
        }

        [DuckCopy]
        public struct PrivateStaticReadonlyReferenceTypeFieldStruct
        {
            [DuckField(Name = "privateStaticReadonlyReferenceTypeField")]
            public string PrivateStaticReadonlyReferenceTypeField;
        }

        // *

        [DuckCopy]
        public struct PublicStaticReferenceTypeFieldStruct
        {
            [DuckField(Name = "publicStaticReferenceTypeField")]
            public string PublicStaticReferenceTypeField;
        }

        [DuckCopy]
        public struct InternalStaticReferenceTypeFieldStruct
        {
            [DuckField(Name = "internalStaticReferenceTypeField")]
            public string InternalStaticReferenceTypeField;
        }

        [DuckCopy]
        public struct ProtectedStaticReferenceTypeFieldStruct
        {
            [DuckField(Name = "protectedStaticReferenceTypeField")]
            public string ProtectedStaticReferenceTypeField;
        }

        [DuckCopy]
        public struct PrivateStaticReferenceTypeFieldStruct
        {
            [DuckField(Name = "privateStaticReferenceTypeField")]
            public string PrivateStaticReferenceTypeField;
        }

        // *

        [DuckCopy]
        public struct PublicReadonlyReferenceTypeFieldStruct
        {
            [DuckField(Name = "publicReadonlyReferenceTypeField")]
            public string PublicReadonlyReferenceTypeField;
        }

        [DuckCopy]
        public struct InternalReadonlyReferenceTypeFieldStruct
        {
            [DuckField(Name = "internalReadonlyReferenceTypeField")]
            public string InternalReadonlyReferenceTypeField;
        }

        [DuckCopy]
        public struct ProtectedReadonlyReferenceTypeFieldStruct
        {
            [DuckField(Name = "protectedReadonlyReferenceTypeField")]
            public string ProtectedReadonlyReferenceTypeField;
        }

        [DuckCopy]
        public struct PrivateReadonlyReferenceTypeFieldStruct
        {
            [DuckField(Name = "privateReadonlyReferenceTypeField")]
            public string PrivateReadonlyReferenceTypeField;
        }

        // *

        [DuckCopy]
        public struct PublicReferenceTypeFieldStruct
        {
            [DuckField(Name = "publicReferenceTypeField")]
            public string PublicReferenceTypeField;
        }

        [DuckCopy]
        public struct InternalReferenceTypeFieldStruct
        {
            [DuckField(Name = "internalReferenceTypeField")]
            public string InternalReferenceTypeField;
        }

        [DuckCopy]
        public struct ProtectedReferenceTypeFieldStruct
        {
            [DuckField(Name = "protectedReferenceTypeField")]
            public string ProtectedReferenceTypeField;
        }

        [DuckCopy]
        public struct PrivateReferenceTypeFieldStruct
        {
            [DuckField(Name = "privateReferenceTypeField")]
            public string PrivateReferenceTypeField;
        }
    }
}
