// <copyright file="IWrongFieldName.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.DuckTyping.Tests.Errors.Fields.TypeChaining.ProxiesDefinitions.WrongFieldName
{
    public interface IWrongFieldName
    {
        public interface IPublicStaticReadonlySelfTypeField
        {
            [DuckField(Name = "publicStaticReadonlySelfTypeField")]
            IDummyFieldObject PublicStaticReadonlySelfTypeField { get; }
        }

        public interface IInternalStaticReadonlySelfTypeField
        {
            [DuckField(Name = "internalStaticReadonlySelfTypeField")]
            IDummyFieldObject InternalStaticReadonlySelfTypeField { get; }
        }

        public interface IProtectedStaticReadonlySelfTypeField
        {
            [DuckField(Name = "protectedStaticReadonlySelfTypeField")]
            IDummyFieldObject ProtectedStaticReadonlySelfTypeField { get; }
        }

        public interface IPrivateStaticReadonlySelfTypeField
        {
            [DuckField(Name = "privateStaticReadonlySelfTypeField")]
            IDummyFieldObject PrivateStaticReadonlySelfTypeField { get; }
        }

        // *

        public interface IPublicStaticSelfTypeField
        {
            [DuckField(Name = "publicStaticSelfTypeField")]
            IDummyFieldObject PublicStaticSelfTypeField { get; set; }
        }

        public interface IInternalStaticSelfTypeField
        {
            [DuckField(Name = "internalStaticSelfTypeField")]
            IDummyFieldObject InternalStaticSelfTypeField { get; set; }
        }

        public interface IProtectedStaticSelfTypeField
        {
            [DuckField(Name = "protectedStaticSelfTypeField")]
            IDummyFieldObject ProtectedStaticSelfTypeField { get; set; }
        }

        public interface IPrivateStaticSelfTypeField
        {
            [DuckField(Name = "privateStaticSelfTypeField")]
            IDummyFieldObject PrivateStaticSelfTypeField { get; set; }
        }

        // *

        public interface IPublicReadonlySelfTypeField
        {
            [DuckField(Name = "publicReadonlySelfTypeField")]
            IDummyFieldObject PublicReadonlySelfTypeField { get; }
        }

        public interface IInternalReadonlySelfTypeField
        {
            [DuckField(Name = "internalReadonlySelfTypeField")]
            IDummyFieldObject InternalReadonlySelfTypeField { get; }
        }

        public interface IProtectedReadonlySelfTypeField
        {
            [DuckField(Name = "protectedReadonlySelfTypeField")]
            IDummyFieldObject ProtectedReadonlySelfTypeField { get; }
        }

        public interface IPrivateReadonlySelfTypeField
        {
            [DuckField(Name = "privateReadonlySelfTypeField")]
            IDummyFieldObject PrivateReadonlySelfTypeField { get; }
        }

        // *

        public interface IPublicSelfTypeField
        {
            [DuckField(Name = "publicSelfTypeField")]
            IDummyFieldObject PublicSelfTypeField { get; set; }
        }

        public interface IInternalSelfTypeField
        {
            [DuckField(Name = "internalSelfTypeField")]
            IDummyFieldObject InternalSelfTypeField { get; set; }
        }

        public interface IProtectedSelfTypeField
        {
            [DuckField(Name = "protectedSelfTypeField")]
            IDummyFieldObject ProtectedSelfTypeField { get; set; }
        }

        public interface IPrivateSelfTypeField
        {
            [DuckField(Name = "privateSelfTypeField")]
            IDummyFieldObject PrivateSelfTypeField { get; set; }
        }
    }
}
