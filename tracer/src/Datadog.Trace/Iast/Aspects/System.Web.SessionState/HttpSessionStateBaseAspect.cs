// <copyright file="HttpSessionStateBaseAspect.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#if NETFRAMEWORK

using Datadog.Trace.Iast.Dataflow;

// Every time that HttpCookie.Value is called in net framework, a new string is generated because
// it calls _multiValue.ToString(), which builds a new string from a new stringbuilder,
// so we need to taint the result of all these calls.

namespace Datadog.Trace.Iast.Aspects.System.Web.SessionState;

/// <summary> HttpSessionStateAspect class aspects </summary>
[AspectClass("System.Web", AspectType.Sink, VulnerabilityType.TrustBoundaryViolation)]
[global::System.ComponentModel.Browsable(false)]
[global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
public class HttpSessionStateBaseAspect
{
    /// <summary>
    /// Launches a SSRF vulnerability if the url string is tainted
    /// </summary>
    /// <param name="value">first sensitive parameter of the method</param>
    /// <returns> Consumed params </returns>
    [AspectMethodInsertBefore("System.Web.HttpSessionStateBase::Add(System.String,System.Object)", new int[] { 0, 1 })]
    [AspectMethodInsertBefore("System.Web.HttpSessionStateBase::set_Item(System.Int32,System.Object)", 0)]
    [AspectMethodInsertBefore("System.Web.HttpSessionStateBase::set_Item(System.String,System.Object)", new int[] { 0, 1 })]
    [AspectMethodInsertBefore("System.Web.HttpSessionStateBase::Remove(System.String)", 0)]
    public static object Add(object value)
    {
        try
        {
            if (value is string valueStr)
            {
                IastModule.OnTrustBoundaryViolation(valueStr);
            }

            return value;
        }
        catch (global::System.Exception ex)
        {
            IastModule.Log.Error(ex, $"Error invoking {nameof(HttpSessionStateBaseAspect)}.{nameof(Add)}");
            return value;
        }
    }
}

#endif
