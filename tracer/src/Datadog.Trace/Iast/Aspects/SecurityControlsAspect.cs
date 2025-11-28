// <copyright file="SecurityControlsAspect.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Datadog.Trace.Iast.Dataflow;
using Datadog.Trace.Iast.Propagation;
using Datadog.Trace.Logging;
using static Datadog.Trace.Iast.Propagation.StringModuleImpl;

namespace Datadog.Trace.Iast.Aspects;

/// <summary> SecurityControlsAspect </summary>
[global::System.ComponentModel.Browsable(false)]
[global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
public static class SecurityControlsAspect
{
    /// <summary>
    /// String.Trim aspect
    /// </summary>
    /// <param name="target"> string base instance </param>
    /// <param name="mark"> secure mark to add to the object, if tainted </param>
    /// <returns> String.Trim() </returns>
    public static object? MarkAsSecure(object? target, uint mark)
    {
        try
        {
            return IastModule.OnCustomEscape(target, (SecureMarks)mark);
        }
        catch (Exception ex)
        {
            IastModule.LogAspectException(ex);
        }

        return target;
    }
}
