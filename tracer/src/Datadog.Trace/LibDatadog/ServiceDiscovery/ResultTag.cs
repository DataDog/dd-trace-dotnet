// <copyright file="ResultTag.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable
namespace Datadog.Trace.LibDatadog.ServiceDiscovery;

/// <summary>
/// Do not change the values of this enum unless you really need to update the interop mapping.
/// Libdatadog interop mapping of https://github.com/DataDog/libdatadog/blob/60583218a8de6768f67d04fcd5bc6443f67f516b/ddcommon-ffi/src/result.rs#L44
/// Cf type ddog_Result_TracerMemfdHandle_Tag in common.h headers.
/// typedef enum ddog_Result_TracerMemfdHandle_Tag {
///      DDOG_RESULT_TRACER_MEMFD_HANDLE_OK_TRACER_MEMFD_HANDLE,
///      DDOG_RESULT_TRACER_MEMFD_HANDLE_ERR_TRACER_MEMFD_HANDLE,
///  } ddog_Result_TracerMemfdHandle_Tag;
/// </summary>
internal enum ResultTag
{
    Ok = 0,
    Error = 1,
}
