// <copyright file="EntityCommandAspect.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Data.Common;
using Datadog.Trace.AppSec.Rasp;
using Datadog.Trace.Configuration;
using Datadog.Trace.Iast.Dataflow;

namespace Datadog.Trace.Iast.Aspects;

#nullable enable

/// <summary> EntityCommandAspect class aspect </summary>
[AspectClass("EntityFramework", AspectType.RaspIastSink, VulnerabilityType.SqlInjection)]
[global::System.ComponentModel.Browsable(false)]
[global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
public class EntityCommandAspect
{
    /// <summary>
    /// ExecuteReader aspect
    /// </summary>
    /// <param name="command"> the DbCommand </param>
    /// <returns> resulting sql query </returns>
    [AspectMethodInsertBefore("System.Data.Entity.Core.EntityClient.EntityCommand::ExecuteReader(System.Data.CommandBehavior)", 1)]
    [AspectMethodInsertBefore("System.Data.Entity.Core.EntityClient.EntityCommand::ExecuteReaderAsync(System.Data.CommandBehavior,System.Threading.CancellationToken)", 2)]
    [AspectMethodInsertBefore("System.Data.Entity.Core.EntityClient.EntityCommand::ExecuteReaderAsync(System.Data.CommandBehavior)", 1)]
    public static object ReviewSqlCommand(object command)
    {
        if (command is DbCommand dbCommand)
        {
            var commandText = dbCommand.CommandText;
            if (!string.IsNullOrEmpty(commandText))
            {
                IastModule.OnSqlQuery(commandText, IntegrationId.SqlClient);
                RaspModule.OnSqlI(commandText, IntegrationId.SqlClient);
            }
        }

        return command;
    }
}
