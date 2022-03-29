// <copyright file="WrongReturnTypeVirtualClass.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.DuckTyping.Tests.Errors.Fields.TypeChaining.ProxiesDefinitions.WrongReturnType
{
    public class WrongReturnTypeVirtualClass
    {
        public class PublicStaticReadonlySelfTypeFieldVirtualClass
        {
            [DuckField(Name = "_publicStaticReadonlySelfTypeField")]
            public virtual int PublicStaticReadonlySelfTypeField { get; }
        }

        public class InternalStaticReadonlySelfTypeFieldVirtualClass
        {
            [DuckField(Name = "_internalStaticReadonlySelfTypeField")]
            public virtual int InternalStaticReadonlySelfTypeField { get; }
        }

        public class ProtectedStaticReadonlySelfTypeFieldVirtualClass
        {
            [DuckField(Name = "_protectedStaticReadonlySelfTypeField")]
            public virtual int ProtectedStaticReadonlySelfTypeField { get; }
        }

        public class PrivateStaticReadonlySelfTypeFieldVirtualClass
        {
            [DuckField(Name = "_privateStaticReadonlySelfTypeField")]
            public virtual int PrivateStaticReadonlySelfTypeField { get; }
        }

        // *

        public class PublicStaticSelfTypeFieldVirtualClass
        {
            [DuckField(Name = "_publicStaticSelfTypeField")]
            public virtual int PublicStaticSelfTypeField { get; set; }
        }

        public class InternalStaticSelfTypeFieldVirtualClass
        {
            [DuckField(Name = "_internalStaticSelfTypeField")]
            public virtual int InternalStaticSelfTypeField { get; set; }
        }

        public class ProtectedStaticSelfTypeFieldVirtualClass
        {
            [DuckField(Name = "_protectedStaticSelfTypeField")]
            public virtual int ProtectedStaticSelfTypeField { get; set; }
        }

        public class PrivateStaticSelfTypeFieldVirtualClass
        {
            [DuckField(Name = "_privateStaticSelfTypeField")]
            public virtual int PrivateStaticSelfTypeField { get; set; }
        }

        // *

        public class PublicReadonlySelfTypeFieldVirtualClass
        {
            [DuckField(Name = "_publicReadonlySelfTypeField")]
            public virtual int PublicReadonlySelfTypeField { get; }
        }

        public class InternalReadonlySelfTypeFieldVirtualClass
        {
            [DuckField(Name = "_internalReadonlySelfTypeField")]
            public virtual int InternalReadonlySelfTypeField { get; }
        }

        public class ProtectedReadonlySelfTypeFieldVirtualClass
        {
            [DuckField(Name = "_protectedReadonlySelfTypeField")]
            public virtual int ProtectedReadonlySelfTypeField { get; }
        }

        public class PrivateReadonlySelfTypeFieldVirtualClass
        {
            [DuckField(Name = "_privateReadonlySelfTypeField")]
            public virtual int PrivateReadonlySelfTypeField { get; }
        }

        // *

        public class PublicSelfTypeFieldVirtualClass
        {
            [DuckField(Name = "_publicSelfTypeField")]
            public virtual int PublicSelfTypeField { get; set; }
        }

        public class InternalSelfTypeFieldVirtualClass
        {
            [DuckField(Name = "_internalSelfTypeField")]
            public virtual int InternalSelfTypeField { get; set; }
        }

        public class ProtectedSelfTypeFieldVirtualClass
        {
            [DuckField(Name = "_protectedSelfTypeField")]
            public virtual int ProtectedSelfTypeField { get; set; }
        }

        public class PrivateSelfTypeFieldVirtualClass
        {
            [DuckField(Name = "_privateSelfTypeField")]
            public virtual int PrivateSelfTypeField { get; set; }
        }
    }
}
