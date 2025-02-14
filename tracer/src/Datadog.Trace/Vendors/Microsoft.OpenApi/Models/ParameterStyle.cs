//------------------------------------------------------------------------------
// <auto-generated />
// This file was automatically generated by the UpdateVendoredCode tool.
//------------------------------------------------------------------------------
#pragma warning disable CS0618, CS0649, CS1574, CS1580, CS1581, CS1584, CS1591, CS1573, CS8018, SYSLIB0011, SYSLIB0023, SYSLIB0032
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

using Datadog.Trace.Vendors.Microsoft.OpenApi.Attributes;

namespace Datadog.Trace.Vendors.Microsoft.OpenApi.Models
{
    /// <summary>
    /// The style of the parameter.
    /// </summary>
    internal enum ParameterStyle
    {
        /// <summary>
        /// Path-style parameters.
        /// </summary>
        [Display("matrix")] Matrix,

        /// <summary>
        /// Label style parameters.
        /// </summary>
        [Display("label")] Label,

        /// <summary>
        /// Form style parameters.
        /// </summary>
        [Display("form")] Form,

        /// <summary>
        /// Simple style parameters.
        /// </summary>
        [Display("simple")] Simple,

        /// <summary>
        /// Space separated array values.
        /// </summary>
        [Display("spaceDelimited")] SpaceDelimited,

        /// <summary>
        /// Pipe separated array values.
        /// </summary>
        [Display("pipeDelimited")] PipeDelimited,

        /// <summary>
        /// Provides a simple way of rendering nested objects using form parameters.
        /// </summary>
        [Display("deepObject")] DeepObject
    }
}
