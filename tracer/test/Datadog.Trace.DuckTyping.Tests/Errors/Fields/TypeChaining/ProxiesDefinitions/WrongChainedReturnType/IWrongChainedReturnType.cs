// <copyright file="IWrongChainedReturnType.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.DuckTyping.Tests.Errors.Fields.TypeChaining.ProxiesDefinitions.WrongChainedReturnType
{
    public interface IWrongChainedReturnType
    {
        public interface IPublicStaticReadonlySelfTypeField
        {
            [DuckField(Name = "_publicStaticReadonlySelfTypeField")]
            IInvalidDummyFieldObject PublicStaticReadonlySelfTypeField { get; }
        }

        public interface IInternalStaticReadonlySelfTypeField
        {
            [DuckField(Name = "_internalStaticReadonlySelfTypeField")]
            IInvalidDummyFieldObject InternalStaticReadonlySelfTypeField { get; }
        }

        public interface IProtectedStaticReadonlySelfTypeField
        {
            [DuckField(Name = "_protectedStaticReadonlySelfTypeField")]
            IInvalidDummyFieldObject ProtectedStaticReadonlySelfTypeField { get; }
        }

        public interface IPrivateStaticReadonlySelfTypeField
        {
            [DuckField(Name = "_privateStaticReadonlySelfTypeField")]
            IInvalidDummyFieldObject PrivateStaticReadonlySelfTypeField { get; }
        }

        // *

        public interface IPublicStaticSelfTypeField
        {
            [DuckField(Name = "_publicStaticSelfTypeField")]
            IInvalidDummyFieldObject PublicStaticSelfTypeField { get; set; }
        }

        public interface IInternalStaticSelfTypeField
        {
            [DuckField(Name = "_internalStaticSelfTypeField")]
            IInvalidDummyFieldObject InternalStaticSelfTypeField { get; set; }
        }

        public interface IProtectedStaticSelfTypeField
        {
            [DuckField(Name = "_protectedStaticSelfTypeField")]
            IInvalidDummyFieldObject ProtectedStaticSelfTypeField { get; set; }
        }

        public interface IPrivateStaticSelfTypeField
        {
            [DuckField(Name = "_privateStaticSelfTypeField")]
            IInvalidDummyFieldObject PrivateStaticSelfTypeField { get; set; }
        }

        // *

        public interface IPublicReadonlySelfTypeField
        {
            [DuckField(Name = "_publicReadonlySelfTypeField")]
            IInvalidDummyFieldObject PublicReadonlySelfTypeField { get; }
        }

        public interface IInternalReadonlySelfTypeField
        {
            [DuckField(Name = "_internalReadonlySelfTypeField")]
            IInvalidDummyFieldObject InternalReadonlySelfTypeField { get; }
        }

        public interface IProtectedReadonlySelfTypeField
        {
            [DuckField(Name = "_protectedReadonlySelfTypeField")]
            IInvalidDummyFieldObject ProtectedReadonlySelfTypeField { get; }
        }

        public interface IPrivateReadonlySelfTypeField
        {
            [DuckField(Name = "_privateReadonlySelfTypeField")]
            IInvalidDummyFieldObject PrivateReadonlySelfTypeField { get; }
        }

        // *

        public interface IPublicSelfTypeField
        {
            [DuckField(Name = "_publicSelfTypeField")]
            IInvalidDummyFieldObject PublicSelfTypeField { get; set; }
        }

        public interface IInternalSelfTypeField
        {
            [DuckField(Name = "_internalSelfTypeField")]
            IInvalidDummyFieldObject InternalSelfTypeField { get; set; }
        }

        public interface IProtectedSelfTypeField
        {
            [DuckField(Name = "_protectedSelfTypeField")]
            IInvalidDummyFieldObject ProtectedSelfTypeField { get; set; }
        }

        public interface IPrivateSelfTypeField
        {
            [DuckField(Name = "_privateSelfTypeField")]
            IInvalidDummyFieldObject PrivateSelfTypeField { get; set; }
        }
    }
}
