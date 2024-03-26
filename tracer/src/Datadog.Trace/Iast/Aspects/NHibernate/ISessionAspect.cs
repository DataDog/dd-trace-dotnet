// <copyright file="ISessionAspect.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.Configuration;
using Datadog.Trace.Iast.Dataflow;

namespace Datadog.Trace.Iast.Aspects.NHibernate;

/// <summary> NHibernate class aspect </summary>
[AspectClass("NHibernate", AspectType.Sink, VulnerabilityType.SqlInjection)]
[global::System.ComponentModel.Browsable(false)]
[global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
public class ISessionAspect
{
    /// <summary>
    ///     NHibernate aspect
    /// </summary>
    /// <param name="query"> the mongodb command </param>
    /// <returns> the original command </returns>
    [AspectMethodInsertBefore("NHibernate.ISession::CreateQuery(System.String)", 0)]
    [AspectMethodInsertBefore("NHibernate.ISession::CreateSQLQuery(System.String)", 0)]
    public static object AnalyzeQuery(string query)
    {
        IastModule.OnSqlQuery(query, IntegrationId.NHibernate);
        return query;
    }
}
