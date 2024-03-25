// <copyright file="IQueryAspect.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.Iast.Dataflow;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Iast.Aspects.NHibernate;

/// <summary> NHibernate class aspect </summary>
[AspectClass("NHibernate", AspectType.Sink)]
[global::System.ComponentModel.Browsable(false)]
[global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
public class IQueryAspect
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(IQueryAspect));

    /// <summary>
    ///     MongoDB Driver aspect
    /// </summary>
    /// <param name="command"> the mongodb command </param>
    /// <returns> the original command </returns>
    [AspectMethodInsertBefore("NHibernate.ISession::CreateQuery(string)", 0)]
    public static object? AnalyzeCommand(object? command)
    {
        return null;
    }
}
