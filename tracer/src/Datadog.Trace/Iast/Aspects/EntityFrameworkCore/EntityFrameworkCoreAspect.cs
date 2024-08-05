// <copyright file="EntityFrameworkCoreAspect.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.AppSec;
using Datadog.Trace.Configuration;
using Datadog.Trace.Iast.Dataflow;

#nullable enable

namespace Datadog.Trace.Iast.Aspects;

#if !NETFRAMEWORK
/// <summary> EntityFrameworkCoreAspect class aspect </summary>
[AspectClass("Microsoft.EntityFrameworkCore.Relational", AspectType.RaspIastSink, VulnerabilityType.SqlInjection)]
[global::System.ComponentModel.Browsable(false)]
[global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
public class EntityFrameworkCoreAspect
{
    /// <summary>
    /// EntityFrameworkCoreAspect aspect
    /// </summary>
    /// <param name="sqlAsString"> sql query </param>
    /// <returns> resulting sql query </returns>
    [AspectMethodInsertBefore("Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions::ExecuteSqlRaw(Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade,System.String,System.Object[])", 1)]
    [AspectMethodInsertBefore("Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions::ExecuteSqlRaw(Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade,System.String,System.Collections.Generic.IEnumerable`1<System.Object>)", 1)]
    [AspectMethodInsertBefore("Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions::ExecuteSqlRawAsync(Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade,System.String,System.Threading.CancellationToken)", 1)]
    [AspectMethodInsertBefore("Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions::ExecuteSqlRawAsync(Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade,System.String,System.Object[])", 1)]
    [AspectMethodInsertBefore("Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions::ExecuteSqlRawAsync(Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade,System.String,System.Collections.Generic.IEnumerable`1<System.Object>,System.Threading.CancellationToken)", 2)]
    [AspectMethodInsertBefore("Microsoft.EntityFrameworkCore.RelationalQueryableExtensions::FromSqlRaw(Microsoft.EntityFrameworkCore.DbSet`1<!!0>,System.String,System.Object[])", 1)]
    public static object ReviewSqlString(string sqlAsString)
    {
        try
        {
            VulnerabilitiesModule.OnSqlQuery(sqlAsString, IntegrationId.SqlClient);
            return sqlAsString;
        }
        catch (Exception ex) when (ex is not BlockException)
        {
            IastModule.Log.Error(ex, $"Error invoking {nameof(EntityFrameworkCoreAspect)}.{nameof(ReviewSqlString)}");
            return sqlAsString;
        }
    }
}
#endif

