// <copyright file="DDWAF_OBJ_TYPE.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.AppSec.Waf.NativeBindings
{
    /// <summary>
    /// Mirrors DDWAF_OBJ_TYPE in ddwaf.h. The backing type must stay <see cref="byte"/>: since
    /// libddwaf 2.0 the type is stored in the first byte of the 16 byte ddwaf_object union.
    /// </summary>
    internal enum DDWAF_OBJ_TYPE : byte
    {
        /** Unknown or uninitialised type **/
        DDWAF_OBJ_INVALID = 0x00,
        /** Null type, only used for its semantic value **/
        DDWAF_OBJ_NULL = 0x01,
        /** Boolean type **/
        DDWAF_OBJ_BOOL = 0x02,
        /** 64-bit signed integer type **/
        DDWAF_OBJ_SIGNED = 0x04,
        /** 64-bit unsigned integer type **/
        DDWAF_OBJ_UNSIGNED = 0x06,
        /** 64-bit float (or double) type **/
        DDWAF_OBJ_FLOAT = 0x08,
        /** Dynamic UTF-8 string of up to max(uint32) length **/
        DDWAF_OBJ_STRING = 0x10,
        /** Literal UTF-8 string of up to max(uint32) length, these are never freed **/
        DDWAF_OBJ_LITERAL_STRING = 0x12,
        /** UTF-8 string of up to 14 bytes, stored inline in the object itself **/
        DDWAF_OBJ_SMALL_STRING = 0x14,
        /** Array of ddwaf_object, up to max(uint16) capacity **/
        DDWAF_OBJ_ARRAY = 0x20,
        /** Array of ddwaf_object_kv, up to max(uint16) capacity **/
        DDWAF_OBJ_MAP = 0x40,
    }
}
