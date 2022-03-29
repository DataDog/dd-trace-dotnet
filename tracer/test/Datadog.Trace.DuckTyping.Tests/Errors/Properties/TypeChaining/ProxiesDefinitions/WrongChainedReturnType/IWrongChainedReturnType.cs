// <copyright file="IWrongChainedReturnType.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.DuckTyping.Tests.Errors.Properties.TypeChaining.ProxiesDefinitions.WrongChainedReturnType
{
    public interface IWrongChainedReturnType
    {
        public interface IPublicStaticGetSelfType
        {
            IWrongFieldObject PublicStaticGetSelfType { get; }
        }

        public interface IInternalStaticGetSelfType
        {
            IWrongFieldObject InternalStaticGetSelfType { get; }
        }

        public interface IProtectedStaticGetSelfType
        {
            IWrongFieldObject ProtectedStaticGetSelfType { get; }
        }

        public interface IPrivateStaticGetSelfType
        {
            IWrongFieldObject PrivateStaticGetSelfType { get; }
        }

        // *

        public interface IPublicStaticGetSetSelfType
        {
            IWrongFieldObject PublicStaticGetSetSelfType { get; set; }
        }

        public interface IInternalStaticGetSetSelfType
        {
            IWrongFieldObject InternalStaticGetSetSelfType { get; set; }
        }

        public interface IProtectedStaticGetSetSelfType
        {
            IWrongFieldObject ProtectedStaticGetSetSelfType { get; set; }
        }

        public interface IPrivateStaticGetSetSelfType
        {
            IWrongFieldObject PrivateStaticGetSetSelfType { get; set; }
        }

        // *

        public interface IPublicGetSelfType
        {
            IWrongFieldObject PublicGetSelfType { get; }
        }

        public interface IInternalGetSelfType
        {
            IWrongFieldObject InternalGetSelfType { get; }
        }

        public interface IProtectedGetSelfType
        {
            IWrongFieldObject ProtectedGetSelfType { get; }
        }

        public interface IPrivateGetSelfType
        {
            IWrongFieldObject PrivateGetSelfType { get; }
        }

        // *

        public interface IPublicGetSetSelfType
        {
            IWrongFieldObject PublicGetSetSelfType { get; set; }
        }

        public interface IInternalGetSetSelfType
        {
            IWrongFieldObject InternalGetSetSelfType { get; set; }
        }

        public interface IProtectedGetSetSelfType
        {
            IWrongFieldObject ProtectedGetSetSelfType { get; set; }
        }

        public interface IPrivateGetSetSelfType
        {
            IWrongFieldObject PrivateGetSetSelfType { get; set; }
        }

        // *

        public interface IPrivateWrongGetSetSelfType
        {
            IWrongFieldObject PrivateWrongGetSetSelfType { get; set; }
        }
    }
}
