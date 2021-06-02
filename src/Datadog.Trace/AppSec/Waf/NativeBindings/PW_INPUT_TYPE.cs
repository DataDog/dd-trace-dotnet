// <copyright file="PW_INPUT_TYPE.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Text;

namespace Datadog.Trace.AppSec.Waf.NativeBindings
{
    internal enum PW_INPUT_TYPE
    {
        PWI_INVALID = 0,
        PWI_SIGNED_NUMBER = 1 << 0, // `value` shall be decoded as a int64_t (or int32_t on 32bits platforms)
        PWI_UNSIGNED_NUMBER = 1 << 1, // `value` shall be decoded as a uint64_t (or uint32_t on 32bits platforms)
        PWI_STRING = 1 << 2, // `value` shall be decoded as a UTF-8 string of length `nbEntries`
        PWI_ARRAY = 1 << 3, // `value` shall be decoded as an array of PWArgs of length `nbEntries`, each item having no `parameterName`
        PWI_MAP = 1 << 4, // `value` shall be decoded as an array of PWArgs of length `nbEntries`, each item having a `parameterName`
    }
}
