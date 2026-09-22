// <copyright file="IDuckTypeUnion.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.DuckTyping.Tests.Properties.ReferenceType.ProxiesDefinitions
{
    public interface IDuckTypeUnion :
        IDuckType,
        IPublicReferenceType,
        IInternalReferenceType,
        IProtectedReferenceType,
        IPrivateReferenceType
    {
    }

    public interface IPublicReferenceType
    {
        [Duck(FallbackToBaseTypes = true)]
        string PublicGetSetReferenceType { get; set; }
    }

    public interface IInternalReferenceType
    {
        [Duck(FallbackToBaseTypes = true)]
        string InternalGetSetReferenceType { get; set; }
    }

    public interface IProtectedReferenceType
    {
        [Duck(FallbackToBaseTypes = true)]
        string ProtectedGetSetReferenceType { get; set; }
    }

    public interface IPrivateReferenceType
    {
        [Duck(FallbackToBaseTypes = true)]
        string PrivateGetSetReferenceType { get; set; }
    }
}
