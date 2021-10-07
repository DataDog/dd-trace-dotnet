// <copyright file="IWrongReturnType.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.DuckTyping.Tests.Errors.Fields.TypeChaining.ProxiesDefinitions.WrongReturnType
{
    public interface IWrongReturnType
    {
        public interface IPublicStaticReadonlySelfTypeField
        {
            [DuckField(Name = "_publicStaticReadonlySelfTypeField")]
            int PublicStaticReadonlySelfTypeField { get; }
        }

        public interface IInternalStaticReadonlySelfTypeField
        {
            [DuckField(Name = "_internalStaticReadonlySelfTypeField")]
            int InternalStaticReadonlySelfTypeField { get; }
        }

        public interface IProtectedStaticReadonlySelfTypeField
        {
            [DuckField(Name = "_protectedStaticReadonlySelfTypeField")]
            int ProtectedStaticReadonlySelfTypeField { get; }
        }

        public interface IPrivateStaticReadonlySelfTypeField
        {
            [DuckField(Name = "_privateStaticReadonlySelfTypeField")]
            int PrivateStaticReadonlySelfTypeField { get; }
        }

        // *

        public interface IPublicStaticSelfTypeField
        {
            [DuckField(Name = "_publicStaticSelfTypeField")]
            int PublicStaticSelfTypeField { get; set; }
        }

        public interface IInternalStaticSelfTypeField
        {
            [DuckField(Name = "_internalStaticSelfTypeField")]
            int InternalStaticSelfTypeField { get; set; }
        }

        public interface IProtectedStaticSelfTypeField
        {
            [DuckField(Name = "_protectedStaticSelfTypeField")]
            int ProtectedStaticSelfTypeField { get; set; }
        }

        public interface IPrivateStaticSelfTypeField
        {
            [DuckField(Name = "_privateStaticSelfTypeField")]
            int PrivateStaticSelfTypeField { get; set; }
        }

        // *

        public interface IPublicReadonlySelfTypeField
        {
            [DuckField(Name = "_publicReadonlySelfTypeField")]
            int PublicReadonlySelfTypeField { get; }
        }

        public interface IInternalReadonlySelfTypeField
        {
            [DuckField(Name = "_internalReadonlySelfTypeField")]
            int InternalReadonlySelfTypeField { get; }
        }

        public interface IProtectedReadonlySelfTypeField
        {
            [DuckField(Name = "_protectedReadonlySelfTypeField")]
            int ProtectedReadonlySelfTypeField { get; }
        }

        public interface IPrivateReadonlySelfTypeField
        {
            [DuckField(Name = "_privateReadonlySelfTypeField")]
            int PrivateReadonlySelfTypeField { get; }
        }

        // *

        public interface IPublicSelfTypeField
        {
            [DuckField(Name = "_publicSelfTypeField")]
            int PublicSelfTypeField { get; set; }
        }

        public interface IInternalSelfTypeField
        {
            [DuckField(Name = "_internalSelfTypeField")]
            int InternalSelfTypeField { get; set; }
        }

        public interface IProtectedSelfTypeField
        {
            [DuckField(Name = "_protectedSelfTypeField")]
            int ProtectedSelfTypeField { get; set; }
        }

        public interface IPrivateSelfTypeField
        {
            [DuckField(Name = "_privateSelfTypeField")]
            int PrivateSelfTypeField { get; set; }
        }
    }
}
