// <copyright file="SqlTags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.SourceGenerators;

#pragma warning disable SA1402 // File must contain single type
namespace Datadog.Trace.Tagging
{
    internal partial class SqlTags : InstrumentationTags
    {
        [Tag(Trace.Tags.SpanKind)]
        public override string SpanKind => SpanKinds.Client;

        /// <summary>
        /// Gets or sets the database management system. Serialized as "db.type" with the Datadog
        /// vocabulary ("sql-server", "postgres", ...) under Datadog semantics, and as
        /// "db.system.name" with the OpenTelemetry vocabulary ("microsoft.sql_server",
        /// "postgresql", ...) under OpenTelemetry semantics.
        /// </summary>
        [Tag(Tags.DbType, OtelName = Tags.DbSystemName)]
        public string DbType { get; set; }

        [Tag(Tags.InstrumentationName)]
        public string InstrumentationName { get; set; }

        /// <summary>
        /// Gets or sets the database. Serialized as "db.name" with the database name alone under
        /// Datadog semantics, and as "db.namespace" under OpenTelemetry semantics, where the name
        /// is qualified by the other namespace components the DBMS defines (for example, a SQL
        /// Server named instance).
        /// </summary>
        [Tag(Tags.DbName, OtelName = Tags.DbNamespace)]
        public string DbName { get; set; }

        [Tag(Tags.DbUser)]
        public string DbUser { get; set; }

        /// <summary>
        /// Gets or sets the database server. Serialized as "out.host" with the connection string's
        /// value verbatim under Datadog semantics, and as "server.address" with the host alone
        /// (no protocol prefix, instance name, or port) under OpenTelemetry semantics.
        /// </summary>
        [Tag(Tags.OutHost, OtelName = Tags.ServerAddress)]
        public string OutHost { get; set; }

        /// <summary>
        /// Gets or sets the port of the database server, when it is not the default port of the
        /// DBMS. We have never reported a port for database spans with Datadog semantics, so this
        /// is only set when OpenTelemetry semantics are enabled.
        /// </summary>
        [Tag(Tags.ServerPort)]
        public int? ServerPort { get; set; }

        /// <summary>
        /// Gets or sets the sanitized text of the query being executed. This is an OpenTelemetry-only
        /// concept (the query is reported in the resource name with Datadog semantics), so it is
        /// only set when OpenTelemetry semantics are enabled.
        /// </summary>
        [Tag(Tags.DbQueryText)]
        public string DbQueryText { get; set; }

        /// <summary>
        /// Gets or sets the low-cardinality summary of the query being executed. This is an
        /// OpenTelemetry-only concept, so it is only set when OpenTelemetry semantics are enabled.
        /// </summary>
        [Tag(Tags.DbQuerySummary)]
        public string DbQuerySummary { get; set; }

        /// <summary>
        /// Gets or sets the name of the operation being executed. This is an OpenTelemetry-only
        /// concept, so it is only set when OpenTelemetry semantics are enabled.
        /// </summary>
        [Tag(Tags.DbOperationName)]
        public string DbOperationName { get; set; }

        /// <summary>
        /// Gets or sets the name of the stored procedure being executed. This is an
        /// OpenTelemetry-only concept, so it is only set when OpenTelemetry semantics are enabled.
        /// </summary>
        [Tag(Tags.DbStoredProcedureName)]
        public string DbStoredProcedureName { get; set; }

        /// <summary>
        /// Gets or sets the name of the collection (table) the call acts on. This is an
        /// OpenTelemetry-only concept, so it is only set when OpenTelemetry semantics are enabled.
        /// </summary>
        [Tag(Tags.DbCollectionName)]
        public string DbCollectionName { get; set; }

        /// <summary>
        /// Gets or sets the status code returned by the database for a failed call. This is an
        /// OpenTelemetry-only concept, so it is only set when OpenTelemetry semantics are enabled.
        /// </summary>
        [Tag(Tags.DbResponseStatusCode)]
        public string DbResponseStatusCode { get; set; }

        /// <summary>
        /// Gets or sets the type of the error that made the call fail. Datadog semantics report the
        /// same tag name through <see cref="Span.SetException(System.Exception)"/>, which writes a
        /// span event instead when OpenTelemetry semantics are enabled, so the database
        /// instrumentation sets this explicitly in that mode.
        /// </summary>
        [Tag(Tags.ErrorType)]
        public string ErrorType { get; set; }

        [Tag(Tags.DbmTraceInjected)]
        public string DbmTraceInjected { get; set; }

        [Tag(Tags.BaseHash)]
        public string BaseHash { get; set; }

        /// <summary>
        /// Gets or sets the command text verbatim. This is never serialized: it is only used to
        /// recognize the nested calls that belong to a command we are already instrumenting, which
        /// the resource name cannot be used for when OpenTelemetry semantics are enabled because it
        /// then holds the low-cardinality span name. It is only set in that mode.
        /// </summary>
        internal string RawCommandText { get; set; }
    }

    internal sealed partial class SqlV1Tags : SqlTags
    {
        private string _peerServiceOverride;

        // Use a private setter for setting the "peer.service" tag so we avoid
        // accidentally setting the value ourselves and instead calculate the
        // value from predefined precursor attributes.
        // However, this can still be set from ITags.SetTag so the user can
        // customize the value if they wish.
        [Tag(Tags.PeerService)]
        public string PeerService
        {
            get => _peerServiceOverride ?? DbName ?? OutHost;
            private set => _peerServiceOverride = value;
        }

        [Tag(Tags.PeerServiceSource)]
        public string PeerServiceSource
        {
            get
            {
                return _peerServiceOverride is not null
                        ? "peer.service"
                        : DbName is not null
                            ? "db.name"
                            : "out.host";
            }
        }
    }
}
