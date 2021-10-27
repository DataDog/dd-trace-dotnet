// <copyright file="IValid.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.DuckTyping.Tests.Errors.Fields.TypeChaining.ProxiesDefinitions.Valid
{
    public interface IValid
    {
        public interface IPublicStaticReadonlySelfTypeField
        {
            [DuckField(Name = "_publicStaticReadonlySelfTypeField")]
            IDummyFieldObject PublicStaticReadonlySelfTypeField { get; }
        }

        public interface IInternalStaticReadonlySelfTypeField
        {
            [DuckField(Name = "_internalStaticReadonlySelfTypeField")]
            IDummyFieldObject InternalStaticReadonlySelfTypeField { get; }
        }

        public interface IProtectedStaticReadonlySelfTypeField
        {
            [DuckField(Name = "_protectedStaticReadonlySelfTypeField")]
            IDummyFieldObject ProtectedStaticReadonlySelfTypeField { get; }
        }

        public interface IPrivateStaticReadonlySelfTypeField
        {
            [DuckField(Name = "_privateStaticReadonlySelfTypeField")]
            IDummyFieldObject PrivateStaticReadonlySelfTypeField { get; }
        }

        // *

        public interface IPublicStaticSelfTypeField
        {
            [DuckField(Name = "_publicStaticSelfTypeField")]
            IDummyFieldObject PublicStaticSelfTypeField { get; set; }
        }

        public interface IInternalStaticSelfTypeField
        {
            [DuckField(Name = "_internalStaticSelfTypeField")]
            IDummyFieldObject InternalStaticSelfTypeField { get; set; }
        }

        public interface IProtectedStaticSelfTypeField
        {
            [DuckField(Name = "_protectedStaticSelfTypeField")]
            IDummyFieldObject ProtectedStaticSelfTypeField { get; set; }
        }

        public interface IPrivateStaticSelfTypeField
        {
            [DuckField(Name = "_privateStaticSelfTypeField")]
            IDummyFieldObject PrivateStaticSelfTypeField { get; set; }
        }

        // *

        public interface IPublicReadonlySelfTypeField
        {
            [DuckField(Name = "_publicReadonlySelfTypeField")]
            IDummyFieldObject PublicReadonlySelfTypeField { get; }
        }

        public interface IInternalReadonlySelfTypeField
        {
            [DuckField(Name = "_internalReadonlySelfTypeField")]
            IDummyFieldObject InternalReadonlySelfTypeField { get; }
        }

        public interface IProtectedReadonlySelfTypeField
        {
            [DuckField(Name = "_protectedReadonlySelfTypeField")]
            IDummyFieldObject ProtectedReadonlySelfTypeField { get; }
        }

        public interface IPrivateReadonlySelfTypeField
        {
            [DuckField(Name = "_privateReadonlySelfTypeField")]
            IDummyFieldObject PrivateReadonlySelfTypeField { get; }
        }

        // *

        public interface IPublicSelfTypeField
        {
            [DuckField(Name = "_publicSelfTypeField")]
            IDummyFieldObject PublicSelfTypeField { get; set; }
        }

        public interface IInternalSelfTypeField
        {
            [DuckField(Name = "_internalSelfTypeField")]
            IDummyFieldObject InternalSelfTypeField { get; set; }
        }

        public interface IProtectedSelfTypeField
        {
            [DuckField(Name = "_protectedSelfTypeField")]
            IDummyFieldObject ProtectedSelfTypeField { get; set; }
        }

        public interface IPrivateSelfTypeField
        {
            [DuckField(Name = "_privateSelfTypeField")]
            IDummyFieldObject PrivateSelfTypeField { get; set; }
        }
    }
}
