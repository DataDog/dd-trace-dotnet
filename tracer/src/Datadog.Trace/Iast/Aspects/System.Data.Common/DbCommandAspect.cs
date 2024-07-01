// <copyright file="DbCommandAspect.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Data.Common;
using Datadog.Trace.AppSec.Rasp;
using Datadog.Trace.Configuration;
using Datadog.Trace.Iast.Dataflow;

namespace Datadog.Trace.Iast.Aspects;

#nullable enable

/// <summary> DbCommandAspect class aspect </summary>
[AspectClass("System.Data,System.Data.Common", AspectType.RaspIastSink, VulnerabilityType.SqlInjection)]
[global::System.ComponentModel.Browsable(false)]
[global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
public class DbCommandAspect
{
    /// <summary>
    /// DbCommandAspect aspect
    /// </summary>
    /// <param name="command"> the DbCommand </param>
    /// <returns> resulting sql query </returns>
    [AspectMethodInsertBefore("System.Data.Common.DbCommand::ExecuteNonQueryAsync(System.Threading.CancellationToken)", 1)]
    [AspectMethodInsertBefore("System.Data.Common.DbCommand::ExecuteNonQueryAsync()")]
    public static object ReviewExecuteNonQuery(object command)
    {
        if (command is DbCommand entityCommand && command.GetType().Name == "EntityCommand")
        {
            var commandText = entityCommand.CommandText;

            if (!string.IsNullOrEmpty(commandText))
            {
                IastModule.OnSqlQuery(commandText, IntegrationId.SqlClient);
                RaspModule.OnSqlI(commandText, IntegrationId.SqlClient);
            }
        }

        return command;
    }
}
