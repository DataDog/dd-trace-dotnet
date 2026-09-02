// <copyright file="DbSemanticConventions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Concurrent;
using System.Data;
using System.Globalization;
using System.Reflection;
using Datadog.Trace.Processors;
using Datadog.Trace.Tagging;
using Datadog.Trace.Util;
using AdoNetDbType = Datadog.Trace.ClrProfiler.AutoInstrumentation.AdoNet.DbType;

namespace Datadog.Trace.OpenTelemetry
{
    /// <summary>
    /// Shapes the values required by the
    /// <see href="https://opentelemetry.io/docs/specs/semconv/database/database-spans/">OpenTelemetry
    /// database semantic conventions</see> and its
    /// <see href="https://opentelemetry.io/docs/specs/semconv/database/sql/">SQL</see> profile, and
    /// stores them on the corresponding <c>ITags</c> properties.
    /// Only used when OpenTelemetry semantics are enabled.
    /// </summary>
    internal static class DbSemanticConventions
    {
        /// <summary>
        /// The value reported in "db.system.name" when the database management system behind an
        /// ADO.NET provider cannot be identified.
        /// </summary>
        internal const string OtherSqlSystem = "other_sql";

        /// <summary>
        /// The value reported in "db.operation.name" when Microsoft SQL Server executes a stored
        /// procedure. It does not support the SQL standard <c>CALL</c> keyword and uses
        /// <c>EXECUTE</c> instead, which is also what the OpenTelemetry specification uses in its
        /// .NET example.
        /// </summary>
        internal const string ExecuteOperation = "EXECUTE";

        /// <summary>
        /// The value reported in "db.operation.name" when a database management system other than
        /// Microsoft SQL Server executes a stored procedure.
        /// </summary>
        internal const string CallOperation = "CALL";

        /// <summary>
        /// The value reported in "db.system.name" for Microsoft SQL Server.
        /// </summary>
        internal const string SqlServerSystem = "microsoft.sql_server";

        /// <summary>
        /// The value reported in "db.operation.name" for a <see cref="CommandType.TableDirect"/>
        /// command, whose command text is the name of a table to read in its entirety.
        /// </summary>
        internal const string SelectOperation = "SELECT";

        /// <summary>
        /// The separator the specification defines for the components of "db.namespace", from the
        /// most general to the most specific.
        /// </summary>
        private const char NamespaceSeparator = '|';

        // The default ports of the database management systems we instrument. "server.port" is only
        // reported when the port in use is not the DBMS default.
        private const int SqlServerDefaultPort = 1433;
        private const int PostgreSqlDefaultPort = 5432;
        private const int MySqlDefaultPort = 3306;
        private const int OracleDefaultPort = 1521;

        // The prefix a SQL Server named pipe data source uses before the instance name, as in
        // "np:\\server\pipe\MSSQL$instance\sql\query".
        private const string NamedPipeInstancePrefix = "MSSQL$";

        /// <summary>
        /// The properties that carry the most specific status code an ADO.NET provider reports for
        /// a failed command, in the order the specification asks for: a vendor-specific code is
        /// preferred over SQLSTATE.
        /// </summary>
        private static readonly string[] ResponseStatusCodePropertyNames =
        [
            "Number", // Microsoft/System.Data.SqlClient, MySql.Data, MySqlConnector, Oracle
            "SqliteErrorCode", // Microsoft.Data.Sqlite
            "SqlState", // Npgsql
            "ResultCode", // System.Data.SQLite
        ];

        /// <summary>
        /// Caches the property that reports the status code of a failed command, per exception type.
        /// Bounded by the number of exception types the instrumented providers can throw.
        /// </summary>
        private static readonly ConcurrentDictionary<Type, PropertyInfo?> ResponseStatusCodeProperties = new();

        /// <summary>
        /// Gets the value to report in "db.system.name" for a Datadog "db.type". The values the
        /// specification defines must be used when one applies; any other provider reports the name
        /// we derived from its command type, which is already lowercase and version-free, as the
        /// specification requires of a custom value.
        /// </summary>
        internal static string GetDbSystemName(string? dbType)
            => dbType switch
            {
                AdoNetDbType.SqlServer => SqlServerSystem,
                AdoNetDbType.PostgreSql => "postgresql",
                AdoNetDbType.MySql => "mysql",
                AdoNetDbType.Oracle => "oracle.db",
                AdoNetDbType.Sqlite => "sqlite",
                null or "" => OtherSqlSystem,
                _ => dbType,
            };

        /// <summary>
        /// Calculates the connection-level attributes of a database client span: "server.address",
        /// "server.port", and "db.namespace". These only change with the connection string, so they
        /// are calculated once per connection string and cached by
        /// <see cref="DbCommandCache"/> rather than per command.
        /// </summary>
        /// <param name="dbType">The Datadog "db.type" of the provider, which decides how the data source is shaped.</param>
        /// <param name="dataSource">The server/data source value of the connection string.</param>
        /// <param name="port">The value of the connection string's "Port" keyword, if it has one.</param>
        /// <param name="databaseName">The database name of the connection string, if it has one.</param>
        /// <param name="serverAddress">The value to report in "server.address".</param>
        /// <param name="serverPort">The value to report in "server.port", or <c>null</c> when the DBMS default port is in use.</param>
        /// <param name="dbNamespace">The value to report in "db.namespace".</param>
        internal static void GetConnectionAttributes(
            string? dbType,
            string? dataSource,
            string? port,
            string? databaseName,
            out string? serverAddress,
            out int? serverPort,
            out string? dbNamespace)
        {
            serverAddress = null;
            serverPort = null;
            dbNamespace = NullIfEmpty(databaseName);

            switch (dbType)
            {
                case AdoNetDbType.Sqlite:
                    // SQLite is embedded, so there is no server to report. The data source (a file
                    // path or ":memory:") is what identifies the database.
                    dbNamespace ??= NullIfEmpty(dataSource);
                    break;

                case AdoNetDbType.SqlServer:
                {
                    ParseSqlServerDataSource(dataSource, out serverAddress, out var instanceName, out serverPort);
                    serverPort = OmitDefaultPort(serverPort ?? ParsePort(port), SqlServerDefaultPort);
                    dbNamespace = JoinNamespace(instanceName, dbNamespace);
                    break;
                }

                case AdoNetDbType.Oracle:
                {
                    ParseOracleDataSource(dataSource, out serverAddress, out serverPort, out var serviceName);
                    serverPort = OmitDefaultPort(serverPort ?? ParsePort(port), OracleDefaultPort);
                    dbNamespace ??= serviceName;
                    break;
                }

                default:
                {
                    ParseHostAndPort(dataSource, out serverAddress, out var parsedPort);
                    serverPort = ParsePort(port) ?? parsedPort;
                    serverPort = dbType switch
                    {
                        AdoNetDbType.PostgreSql => OmitDefaultPort(serverPort, PostgreSqlDefaultPort),
                        AdoNetDbType.MySql => OmitDefaultPort(serverPort, MySqlDefaultPort),
                        _ => serverPort,
                    };

                    break;
                }
            }

            // "server.port" is only reported when "server.address" is, as the specification requires
            if (serverAddress is null)
            {
                serverPort = null;
            }
        }

        /// <summary>
        /// Sets the span name and the command tags of a database client span, using the
        /// OpenTelemetry database semantic conventions. The connection-level tags ("db.system.name",
        /// "db.namespace", "server.address", and "server.port") must already be assigned, because
        /// the span name falls back to them.
        /// </summary>
        /// <param name="span">The database client span.</param>
        /// <param name="tags">The tags of <paramref name="span"/>.</param>
        /// <param name="commandType">How the provider interprets <paramref name="commandText"/>.</param>
        /// <param name="commandText">The command text, as provided by the application.</param>
        internal static void SetDbClientRequestValues(Span span, SqlTags tags, CommandType commandType, string commandText)
        {
            // Used to recognize the nested calls that belong to this command, which the resource
            // name can no longer be used for now that it holds the span name.
            tags.RawCommandText = commandText;

            var target = NullIfEmpty(commandText.Trim());

            switch (commandType)
            {
                case CommandType.StoredProcedure when target is not null:
                    // The command text is the name of the procedure, so there is no query text to
                    // report, and both the operation and the target are known without parsing.
                    tags.DbOperationName = tags.DbType == SqlServerSystem ? ExecuteOperation : CallOperation;
                    tags.DbStoredProcedureName = target;
                    tags.DbQuerySummary = tags.DbOperationName + " " + target;
                    break;

                case CommandType.TableDirect when target is not null:
                    // The command text is the name of a table to read in its entirety, so the
                    // collection name is readily available without parsing any query text.
                    tags.DbOperationName = SelectOperation;
                    tags.DbCollectionName = target;
                    tags.DbQuerySummary = SelectOperation + " " + target;
                    break;

                case CommandType.StoredProcedure:
                case CommandType.TableDirect:
                    // The provider was told how to interpret the command text, but there is none.
                    break;

                default:
                    // The specification only allows the query text to be reported when literals are
                    // replaced with placeholders, which is what the Datadog obfuscator does. We have
                    // no SQL parser, so there is no summary, operation, or collection to report.
                    tags.DbQueryText = NullIfEmpty(ObfuscatorTraceProcessor.ObfuscateSqlResource(commandText));
                    break;
            }

            span.ResourceName = GetSpanName(tags);
        }

        /// <summary>
        /// Determines whether <paramref name="tags"/> belong to a span that is already
        /// instrumenting <paramref name="commandText"/>, so that the nested calls a provider makes
        /// for a single command are not instrumented twice.
        /// </summary>
        internal static bool IsSameCommand(SqlTags tags, string commandText)
            => tags.RawCommandText == commandText;

        /// <summary>
        /// Sets the error tags of a database client span, using the OpenTelemetry database semantic
        /// conventions. Under Datadog semantics these are written by
        /// <see cref="Span.SetException(Exception)"/>, which records an <c>exception</c> span event
        /// instead when OpenTelemetry semantics are enabled.
        /// </summary>
        /// <param name="tags">The tags of the database client span.</param>
        /// <param name="exception">The exception the command failed with.</param>
        internal static void SetDbClientErrorValues(SqlTags tags, Exception exception)
        {
            // Follow Span.SetException and report the first inner exception of an AggregateException,
            // which is the one the provider actually threw.
            if (exception is AggregateException { InnerExceptions.Count: > 0 } aggregateException)
            {
                exception = aggregateException.InnerExceptions[0];
            }

            tags.ErrorType = exception.GetType().ToString();
            tags.DbResponseStatusCode = GetResponseStatusCode(exception);
        }

        /// <summary>
        /// Gets the name of a database client span, following the
        /// <see href="https://opentelemetry.io/docs/specs/semconv/database/database-spans/#name">specification's</see>
        /// precedence: the query summary, then "{db.operation.name} {target}", then the target
        /// alone, and finally the database management system.
        /// </summary>
        internal static string GetSpanName(SqlTags tags)
        {
            if (!StringUtil.IsNullOrEmpty(tags.DbQuerySummary))
            {
                return tags.DbQuerySummary;
            }

            // The target, in the order of preference the specification defines. Note that
            // SqlTags.DbName carries "db.namespace" and SqlTags.OutHost carries "server.address"
            // when OpenTelemetry semantics are enabled.
            var target = tags.DbCollectionName
                      ?? tags.DbStoredProcedureName
                      ?? tags.DbName
                      ?? GetServerAddressAndPort(tags);

            if (!StringUtil.IsNullOrEmpty(tags.DbOperationName))
            {
                return StringUtil.IsNullOrEmpty(target)
                           ? tags.DbOperationName
                           : tags.DbOperationName + " " + target;
            }

            // "db.system.name" is always reported, so the fallback is never empty in practice.
            return (StringUtil.IsNullOrEmpty(target) ? tags.DbType : target) ?? OtherSqlSystem;
        }

        /// <summary>
        /// Gets the value to report in "db.response.status_code" for <paramref name="exception"/>,
        /// or <c>null</c> when the provider does not expose a status code. Providers surface the
        /// code under different property names, so it is resolved by reflection and cached per
        /// exception type; this only runs on the failure path.
        /// </summary>
        internal static string? GetResponseStatusCode(Exception exception)
        {
            var property = ResponseStatusCodeProperties.GetOrAdd(exception.GetType(), static type => FindResponseStatusCodeProperty(type));
            if (property is null)
            {
                return null;
            }

            try
            {
                return property.GetValue(exception) switch
                {
                    null => null,
                    int code => code.ToString(CultureInfo.InvariantCulture),
                    string code => NullIfEmpty(code),
                    var code => NullIfEmpty(code.ToString()),
                };
            }
            catch (Exception)
            {
                // A property getter on a customer-supplied exception type can throw.
                return null;
            }
        }

        private static PropertyInfo? FindResponseStatusCodeProperty(Type exceptionType)
        {
            foreach (var propertyName in ResponseStatusCodePropertyNames)
            {
                PropertyInfo? property = null;
                try
                {
                    property = exceptionType.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
                }
                catch (AmbiguousMatchException)
                {
                    // A derived exception type can shadow the property, in which case we cannot tell
                    // which one the provider means, so move on to the next candidate.
                }

                if (property is { CanRead: true } && property.GetIndexParameters().Length == 0)
                {
                    return property;
                }
            }

            return null;
        }

        /// <summary>
        /// Gets the "{server.address}:{server.port}" form the specification uses as the last resort
        /// target of a span name, or <c>null</c> when no server address is known.
        /// </summary>
        private static string? GetServerAddressAndPort(SqlTags tags)
        {
            if (StringUtil.IsNullOrEmpty(tags.OutHost))
            {
                return null;
            }

            return tags.ServerPort is { } port
                       ? tags.OutHost + ":" + port.ToString(CultureInfo.InvariantCulture)
                       : tags.OutHost;
        }

        /// <summary>
        /// Parses a SQL Server data source, which has the shape
        /// <c>[protocol:]host[\instance][,port]</c>, into its components. Named pipe data sources
        /// (<c>np:\\host\pipe\MSSQL$instance\sql\query</c>) name the instance in the pipe itself.
        /// </summary>
        private static void ParseSqlServerDataSource(string? dataSource, out string? host, out string? instanceName, out int? port)
        {
            host = null;
            instanceName = null;
            port = null;

            if (StringUtil.IsNullOrEmpty(dataSource))
            {
                return;
            }

            var value = dataSource!.Trim();
            var isNamedPipe = value.StartsWith("\\\\", StringComparison.Ordinal);

            // An IPv6 address is bracketed, so its colons must not be mistaken for a protocol prefix.
            if (!isNamedPipe && value.Length > 0 && value[0] != '[')
            {
                var colonIndex = value.IndexOf(':');
                if (colonIndex > 0)
                {
                    var protocol = value.Substring(0, colonIndex).Trim();
                    if (protocol.Equals("np", StringComparison.OrdinalIgnoreCase))
                    {
                        isNamedPipe = true;
                    }

                    if (isNamedPipe ||
                        protocol.Equals("tcp", StringComparison.OrdinalIgnoreCase) ||
                        protocol.Equals("lpc", StringComparison.OrdinalIgnoreCase) ||
                        protocol.Equals("admin", StringComparison.OrdinalIgnoreCase))
                    {
                        value = value.Substring(colonIndex + 1).TrimStart();
                    }
                }
            }

            value = value.TrimStart('\\', '/');

            if (isNamedPipe)
            {
                var separatorIndex = value.IndexOf('\\');
                host = separatorIndex < 0 ? value : value.Substring(0, separatorIndex);

                var prefixIndex = value.IndexOf(NamedPipeInstancePrefix, StringComparison.OrdinalIgnoreCase);
                if (prefixIndex >= 0)
                {
                    var instanceStart = prefixIndex + NamedPipeInstancePrefix.Length;
                    var instanceEnd = value.IndexOf('\\', instanceStart);
                    instanceName = instanceEnd < 0
                                       ? value.Substring(instanceStart)
                                       : value.Substring(instanceStart, instanceEnd - instanceStart);
                }
            }
            else
            {
                var commaIndex = value.LastIndexOf(',');
                if (commaIndex >= 0)
                {
                    port = ParsePort(value.Substring(commaIndex + 1));
                    value = value.Substring(0, commaIndex).TrimEnd();
                }

                var backslashIndex = value.LastIndexOf('\\');
                if (backslashIndex >= 0)
                {
                    instanceName = value.Substring(backslashIndex + 1).Trim();
                    value = value.Substring(0, backslashIndex).TrimEnd();
                }

                host = value;
            }

            host = NormalizeHost(host);
            instanceName = NullIfEmpty(instanceName);
        }

        /// <summary>
        /// Parses an Oracle data source, which is either an "easy connect" string
        /// (<c>[//]host[:port][/service]</c>) or a TNS connect descriptor
        /// (<c>(DESCRIPTION=(ADDRESS=(HOST=..)(PORT=..))(CONNECT_DATA=(SERVICE_NAME=..)))</c>).
        /// A bare TNS alias is indistinguishable from a host name, so it is reported as one, which
        /// is also what the Datadog "out.host" tag has always done with it.
        /// </summary>
        private static void ParseOracleDataSource(string? dataSource, out string? host, out int? port, out string? serviceName)
        {
            host = null;
            port = null;
            serviceName = null;

            if (StringUtil.IsNullOrEmpty(dataSource))
            {
                return;
            }

            var value = dataSource!.Trim();

            if (value.IndexOf('(') >= 0)
            {
                host = NormalizeHost(GetDescriptorValue(value, "HOST"));
                port = ParsePort(GetDescriptorValue(value, "PORT"));
                serviceName = GetDescriptorValue(value, "SERVICE_NAME") ?? GetDescriptorValue(value, "SID");
                return;
            }

            if (value.StartsWith("//", StringComparison.Ordinal))
            {
                value = value.Substring(2);
            }

            var serviceIndex = value.IndexOf('/');
            if (serviceIndex >= 0)
            {
                serviceName = NullIfEmpty(value.Substring(serviceIndex + 1).Trim());
                value = value.Substring(0, serviceIndex);
            }

            ParseHostAndPort(value, out host, out port);
        }

        /// <summary>
        /// Reads the value of a <c>(KEY=value)</c> entry of an Oracle connect descriptor.
        /// </summary>
        private static string? GetDescriptorValue(string descriptor, string key)
        {
            var keyIndex = descriptor.IndexOf(key, StringComparison.OrdinalIgnoreCase);
            while (keyIndex >= 0)
            {
                var equalsIndex = keyIndex + key.Length;
                while (equalsIndex < descriptor.Length && char.IsWhiteSpace(descriptor[equalsIndex]))
                {
                    equalsIndex++;
                }

                if (equalsIndex < descriptor.Length && descriptor[equalsIndex] == '=')
                {
                    var valueStart = equalsIndex + 1;
                    var valueEnd = valueStart;
                    while (valueEnd < descriptor.Length && descriptor[valueEnd] != ')' && descriptor[valueEnd] != '(')
                    {
                        valueEnd++;
                    }

                    return NullIfEmpty(descriptor.Substring(valueStart, valueEnd - valueStart).Trim());
                }

                keyIndex = descriptor.IndexOf(key, keyIndex + key.Length, StringComparison.OrdinalIgnoreCase);
            }

            return null;
        }

        /// <summary>
        /// Splits a <c>host[:port]</c> or <c>host[,port]</c> value. A colon only separates the port
        /// when the host is a bracketed IPv6 address or holds no other colon, so an unbracketed IPv6
        /// address is left alone. When the suffix after a comma is not a port, the value is one of
        /// the host lists some providers accept for failover, and only the first host is reported.
        /// </summary>
        private static void ParseHostAndPort(string? value, out string? host, out int? port)
        {
            host = null;
            port = null;

            if (StringUtil.IsNullOrEmpty(value))
            {
                return;
            }

            var remaining = value!.Trim();
            if (remaining.Length == 0)
            {
                return;
            }

            // A bracketed IPv6 address owns every colon up to its closing bracket.
            var hostEndIndex = remaining[0] == '[' ? remaining.IndexOf(']') : -1;

            // Only the first entry of a host list is reported, and it can carry its own port.
            var commaIndex = remaining.IndexOf(',', hostEndIndex + 1);
            if (commaIndex > hostEndIndex)
            {
                port = ParsePort(remaining.Substring(commaIndex + 1));
                remaining = remaining.Substring(0, commaIndex).TrimEnd();
            }

            if (port is null)
            {
                var colonIndex = remaining.LastIndexOf(':');
                if (colonIndex > hostEndIndex &&
                    (hostEndIndex >= 0 || colonIndex == remaining.IndexOf(':')))
                {
                    port = ParsePort(remaining.Substring(colonIndex + 1));
                    if (port is not null)
                    {
                        remaining = remaining.Substring(0, colonIndex).TrimEnd();
                    }
                }
            }

            host = NormalizeHost(remaining);
        }

        /// <summary>
        /// Gets the value to report in "server.address" for a host. The brackets around an IPv6
        /// address are stripped, as OpenTelemetry expects the address itself.
        /// </summary>
        private static string? NormalizeHost(string? host)
        {
            if (StringUtil.IsNullOrEmpty(host))
            {
                return null;
            }

            var value = host!.Trim();

            if (value.Length > 1 && value[0] == '[' && value[value.Length - 1] == ']')
            {
                value = value.Substring(1, value.Length - 2);
            }

            return NullIfEmpty(value);
        }

        /// <summary>
        /// Joins the components of "db.namespace" from the most general to the most specific,
        /// omitting the components that are unavailable, along with their separator.
        /// </summary>
        private static string? JoinNamespace(string? general, string? specific)
        {
            if (general is null)
            {
                return specific;
            }

            return specific is null ? general : general + NamespaceSeparator + specific;
        }

        private static int? ParsePort(string? value)
            => !StringUtil.IsNullOrEmpty(value) &&
               int.TryParse(value!.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var port)
                   ? port
                   : null;

        /// <summary>
        /// Drops the port when it is the default of the database management system, which the
        /// specification only asks to report when a different port is in use.
        /// </summary>
        private static int? OmitDefaultPort(int? port, int defaultPort)
            => port == defaultPort ? null : port;

        private static string? NullIfEmpty(string? value)
            => StringUtil.IsNullOrEmpty(value) ? null : value;
    }
}
