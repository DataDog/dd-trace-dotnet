// <copyright file="IWrongReturnType.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.DuckTyping.Tests.Errors.Properties.TypeChaining.ProxiesDefinitions.WrongReturnType
{
    public interface IWrongReturnType
    {
        public interface IPublicStaticGetSelfType
        {
            string PublicStaticGetSelfType { get; }
        }

        public interface IInternalStaticGetSelfType
        {
            string InternalStaticGetSelfType { get; }
        }

        public interface IProtectedStaticGetSelfType
        {
            string ProtectedStaticGetSelfType { get; }
        }

        public interface IPrivateStaticGetSelfType
        {
            string PrivateStaticGetSelfType { get; }
        }

        // *

        public interface IPublicStaticGetSetSelfType
        {
            string PublicStaticGetSetSelfType { get; set; }
        }

        public interface IInternalStaticGetSetSelfType
        {
            string InternalStaticGetSetSelfType { get; set; }
        }

        public interface IProtectedStaticGetSetSelfType
        {
            string ProtectedStaticGetSetSelfType { get; set; }
        }

        public interface IPrivateStaticGetSetSelfType
        {
            string PrivateStaticGetSetSelfType { get; set; }
        }

        // *

        public interface IPublicGetSelfType
        {
            string PublicGetSelfType { get; }
        }

        public interface IInternalGetSelfType
        {
            string InternalGetSelfType { get; }
        }

        public interface IProtectedGetSelfType
        {
            string ProtectedGetSelfType { get; }
        }

        public interface IPrivateGetSelfType
        {
            string PrivateGetSelfType { get; }
        }

        // *

        public interface IPublicGetSetSelfType
        {
            string PublicGetSetSelfType { get; set; }
        }

        public interface IInternalGetSetSelfType
        {
            string InternalGetSetSelfType { get; set; }
        }

        public interface IProtectedGetSetSelfType
        {
            string ProtectedGetSetSelfType { get; set; }
        }

        public interface IPrivateGetSetSelfType
        {
            string PrivateGetSetSelfType { get; set; }
        }

        // *

        public interface IPrivateDummyGetSetSelfType
        {
            string PrivateDummyGetSetSelfType { get; set; }
        }
    }
}
