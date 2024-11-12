// <copyright file="SessionExtensionsAspect.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#if !NETFRAMEWORK

using System;
using System.Runtime.InteropServices;
using System.Web;
using Datadog.Trace.Iast.Dataflow;
using Datadog.Trace.Iast.Propagation;
using Microsoft.AspNetCore.Http;

// Every time that HttpCookie.Value is called in net framework, a new string is generated because
// it calls _multiValue.ToString(), which builds a new string from a new stringbuilder,
// so we need to taint the result of all these calls.

namespace Datadog.Trace.Iast.Aspects.System.Web.SessionState;

/// <summary> SessionExtensionsAspect class aspects </summary>
[AspectClass("Microsoft.AspNetCore.Http.Extensions", AspectType.Sink, VulnerabilityType.TrustBoundaryViolation)]
[global::System.ComponentModel.Browsable(false)]
[global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
public class SessionExtensionsAspect
{
    /// <summary>
    /// Launches a SSRF vulnerability if the url string is tainted
    /// </summary>
    /// <param name="value">first sensitive parameter of the method</param>
    /// <returns> Consumed params </returns>
    [AspectMethodInsertBefore("Microsoft.AspNetCore.Http.SessionExtensions::SetString(Microsoft.AspNetCore.Http.ISession,System.String,System.String)", new int[] { 0, 1 })]
    [AspectMethodInsertBefore("Microsoft.AspNetCore.Http.SessionExtensions::SetInt32(Microsoft.AspNetCore.Http.ISession,System.String,System.Int32)", 1)]
    public static string ReviewTbv(string value)
    {
        try
        {
            if (value != null)
            {
                IastModule.OnTrustBoundaryViolation(value);
            }

            return value;
        }
        catch (global::System.Exception ex)
        {
            IastModule.Log.Error(ex, $"Error invoking {nameof(SessionExtensionsAspect)}.{nameof(ReviewTbv)}");
            return value;
        }
    }
}
#endif
