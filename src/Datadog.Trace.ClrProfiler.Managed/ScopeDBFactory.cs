using System;
using System.Data;
using Datadog.Trace.ClrProfiler.Integrations.AdoNet;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler
{
    internal static class ScopeDBFactory<T>
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(ScopeDBFactory<T>));

        private static readonly Type _type;
        private static readonly string _fullName;
        private static readonly string _dbTypeName;
        private static readonly string _operationName;

        static ScopeDBFactory()
        {
            _type = typeof(T);
            _fullName = _type.FullName;
            _dbTypeName = ScopeFactory.GetDbType(_type.Namespace, _type.Name);
            _operationName = $"{_dbTypeName}.query";
        }

        public static Scope CreateDbCommandScope(Tracer tracer, IDbCommand command)
        {
            if (command.GetType() != _type)
            {
                // if the type of the instance is different than the factory type
                // (if the method instrumented is defined in a base class)
                // we fallback to the previous factory.
                return ScopeFactory.CreateDbCommandScope(tracer, command);
            }

            if (_dbTypeName == null)
            {
                // don't create a scope, skip this span
                return null;
            }

            if (!tracer.Settings.IsIntegrationEnabled(AdoNetConstants.IntegrationId))
            {
                // integration disabled, don't create a scope, skip this span
                return null;
            }

            if (tracer.Settings.AdoNetExcludedTypes.Count > 0 && tracer.Settings.AdoNetExcludedTypes.Contains(_fullName))
            {
                // AdoNet type disabled, don't create a scope, skip this span
                return null;
            }

            Scope scope = null;

            try
            {
                Span parent = tracer.ActiveScope?.Span;

                if (parent != null &&
                    parent.Type == SpanTypes.Sql &&
                    parent.GetTag(Tags.DbType) == _dbTypeName &&
                    parent.ResourceName == command.CommandText)
                {
                    // we are already instrumenting this,
                    // don't instrument nested methods that belong to the same stacktrace
                    // e.g. ExecuteReader() -> ExecuteReader(commandBehavior)
                    return null;
                }

                string serviceName = tracer.Settings.GetServiceName(tracer, _dbTypeName);

                var tags = new SqlTags();
                scope = tracer.StartActiveWithTags(_operationName, tags: tags, serviceName: serviceName);
                scope.Span.AddTagsFromDbCommand(command);

                tags.DbType = _dbTypeName;

                tags.SetAnalyticsSampleRate(AdoNetConstants.IntegrationId, tracer.Settings, enabledWithGlobalSetting: false);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error creating or populating scope.");
            }

            return scope;
        }
    }
}
