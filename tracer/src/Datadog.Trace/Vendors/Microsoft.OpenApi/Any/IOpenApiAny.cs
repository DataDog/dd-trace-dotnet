//------------------------------------------------------------------------------
// <auto-generated />
// This file was automatically generated by the UpdateVendoredCode tool.
//------------------------------------------------------------------------------
#pragma warning disable CS0618, CS0649, CS1574, CS1580, CS1581, CS1584, CS1591, CS1573, CS8018, SYSLIB0011, SYSLIB0023, SYSLIB0032
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

using Datadog.Trace.Vendors.Microsoft.OpenApi.Interfaces;

namespace Datadog.Trace.Vendors.Microsoft.OpenApi.Any
{
    /// <summary>
    /// Base interface for all the types that represent Open API Any.
    /// </summary>
    internal interface IOpenApiAny : IOpenApiElement, IOpenApiExtension
    {
        /// <summary>
        /// Type of an <see cref="IOpenApiAny"/>.
        /// </summary>
        AnyType AnyType { get; }
    }
}
