// <copyright file="EntityFrameworkCoreAspect.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Configuration;
using Datadog.Trace.Iast.Dataflow;

namespace Datadog.Trace.Iast.Aspects;

#if !NETFRAMEWORK
/// <summary> EntityFrameworkCoreAspect class aspect </summary>
[AspectClass("Microsoft.EntityFrameworkCore.Relational")]
[global::System.ComponentModel.Browsable(false)]
[global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
public class EntityFrameworkCoreAspect
{
    /// <summary>
    /// ReviewSqlString aspect
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
        IastModule.OnSqlQuery(sqlAsString, IntegrationId.SqlClient);
        return sqlAsString;
    }
}
#endif

