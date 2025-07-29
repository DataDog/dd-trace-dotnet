// <copyright file="ResultTag.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
namespace Datadog.Trace.LibDatadog.HandsOffConfiguration.InteropStructs;

/// <summary>
/// Do not change the values of this enum unless you really need to update the interop mapping.
/// Cf type ddog_Result_VecLibraryConfig_Tag  in common.h headers.
/// typedef enum ddog_Result_VecLibraryConfig_Tag {
///   DDOG_RESULT_VEC_LIBRARY_CONFIG_OK_VEC_LIBRARY_CONFIG,
///   DDOG_RESULT_VEC_LIBRARY_CONFIG_ERR_VEC_LIBRARY_CONFIG,
/// } ddog_Result_VecLibraryConfig_Tag;
/// </summary>
internal enum ResultTag
{
    Ok = 0, // DDOG_RESULT_VEC_LIBRARY_CONFIG_OK_VEC_LIBRARY_CONFIG
    Err = 1 // DDOG_RESULT_VEC_LIBRARY_CONFIG_ERR_VEC_LIBRARY_CONFIG
}
