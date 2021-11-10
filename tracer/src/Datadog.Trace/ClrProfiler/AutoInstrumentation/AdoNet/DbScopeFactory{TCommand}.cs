// <copyright file="DbScopeFactory{TCommand}.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Data;
using Datadog.Trace.Configuration;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AdoNet
{
    /// <summary>
    /// A generic wrapper around <see cref="DbScopeFactory"/> which caches values for each TCommand type.
    /// </summary>
    /// <typeparam name="TCommand">The db command type.</typeparam>
    internal static class DbScopeFactory<TCommand>
    {
        private static readonly Type _commandType;
        private static readonly string _dbTypeName;
        private static readonly string _operationName;
        private static readonly IntegrationInfo? _integrationInfo;

        static DbScopeFactory()
        {
            _commandType = typeof(TCommand);

            if (DbScopeFactory.TryGetIntegrationDetails(_commandType, out var integrationId, out var dbTypeName))
            {
                // cache values for this TCommand type
                _dbTypeName = dbTypeName;
                _operationName = $"{_dbTypeName}.query";
                _integrationInfo = IntegrationRegistry.GetIntegrationInfo(integrationId.ToString());
            }
        }

        public static Scope CreateDbCommandScope(Tracer tracer, IDbCommand command)
        {
            var commandType = command.GetType();

            if (commandType == _commandType && _integrationInfo != null)
            {
                // use the cached values if command type is TCommand
                return DbScopeFactory.CreateDbCommandScope(tracer, command, _integrationInfo.Value, _dbTypeName, _operationName);
            }

            // if command type is not TCommand, we are probably instrumenting a method
            // defined in a base class like DbCommand and we can't use the cached values
            return DbScopeFactory.CreateDbCommandScope(tracer, command);
        }
    }
}
