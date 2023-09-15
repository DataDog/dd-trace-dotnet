// <copyright file="DDWAF_OBJ_TYPE.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.AppSec.Waf.NativeBindings
{
    internal enum DDWAF_OBJ_TYPE
    {
        DDWAF_OBJ_INVALID = 0,
        /** Value shall be decoded as a int64_t (or int32_t on 32bits platforms). **/
        DDWAF_OBJ_SIGNED = 1 << 0,
        /** Value shall be decoded as a uint64_t (or uint32_t on 32bits platforms). **/
        DDWAF_OBJ_UNSIGNED = 1 << 1,
        /** Value shall be decoded as a UTF-8 string of length nbEntries. **/
        DDWAF_OBJ_STRING = 1 << 2,
        /** Value shall be decoded as an array of ddwaf_object of length nbEntries, each item having no parameterName. **/
        DDWAF_OBJ_ARRAY = 1 << 3,
        /** Value shall be decoded as an array of ddwaf_object of length nbEntries, each item having a parameterName. **/
        DDWAF_OBJ_MAP = 1 << 4,
        /** Value shall be decoded as a bool **/
        DDWAF_OBJ_BOOL = 1 << 5,
        /** Value shall be decoded as a double (float64) **/
        DDWAF_OBJ_DOUBLE = 1 << 6,
        /** Null type, only used for its semantic value **/
        DDWAF_OBJ_NULL = 1 << 7,
    }
}
