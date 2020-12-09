using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Data.Common;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.Integrations;
using Datadog.Trace.Configuration;
using Npgsql;
using Xunit;

namespace Datadog.Trace.ClrProfiler.Managed.Tests
{
    public class IntegrationMethodTests
    {
        private const string TestServiceName = "test-service";

        public static Func<TracerSettings, object> CreateFunc(Func<TracerSettings, object> settingGetter)
        {
            return settingGetter;
        }

        public static IEnumerable<object[]> GetDisabledByServiceNameTestData()
        {
            yield return new object[] { (int)IntegrationIds.ElasticsearchNet5, "elasticsearch", (Func<Tracer, int, Scope>)ElasticSearchCreateScope };
            yield return new object[] { (int)IntegrationIds.ElasticsearchNet, "elasticsearch", (Func<Tracer, int, Scope>)ElasticSearchCreateScope };
            yield return new object[] { (int)IntegrationIds.MongoDb, "mongodb", (Func<Tracer, int, Scope>)MongoDbCreateScope };
            yield return new object[] { (int)IntegrationIds.ServiceStackRedis, "redis", (Func<Tracer, int, Scope>)RedisCreateScope };
            yield return new object[] { (int)IntegrationIds.StackExchangeRedis, "redis", (Func<Tracer, int, Scope>)RedisCreateScope };
            yield return new object[] { (int)IntegrationIds.HttpMessageHandler, "http-client", (Func<Tracer, int, Scope>)HttpRequestCreateScope };
            yield return new object[] { (int)IntegrationIds.WebRequest, "http-client", (Func<Tracer, int, Scope>)HttpRequestCreateScope };
            yield return new object[] { (int)IntegrationIds.AdoNet, "sql-server", (Func<Tracer, int, Scope>)SqlCommandCreateScope };
            yield return new object[] { (int)IntegrationIds.AdoNet, "postgres", (Func<Tracer, int, Scope>)PostgresCreateScope };
        }

        [Theory]
        [MemberData(nameof(GetDisabledByServiceNameTestData))]
        public void ClientSpans_DisabledByServiceName(int integration, string serviceNameSuffix, Func<Tracer, int, Scope> createScopeFunc)
        {
            // Set up tracer
            var collection = new NameValueCollection
            {
                { ConfigurationKeys.ServiceName, TestServiceName },
                { ConfigurationKeys.ExcludeServices, $"{TestServiceName}-{serviceNameSuffix}" }
            };
            IConfigurationSource source = new NameValueConfigurationSource(collection);
            var tracerSettings = new TracerSettings(source);
            var tracer = new Tracer(tracerSettings);

            // Create scope
            var scope = createScopeFunc(tracer, integration);

            Assert.Null(scope);
        }

        private static Scope ElasticSearchCreateScope(Tracer tracer, int integration) => ElasticsearchNetCommon.CreateScope(tracer, new IntegrationInfo(integration), null, null);

        private static Scope HttpRequestCreateScope(Tracer tracer, int integration) => ScopeFactory.CreateOutboundHttpScope(tracer, "GET", new Uri("http://www.contoso.com"), new IntegrationInfo(integration), out _);

        private static Scope MongoDbCreateScope(Tracer tracer, int integration) => MongoDbIntegration.CreateScope(tracer, null, null);

        private static Scope RedisCreateScope(Tracer tracer, int integration) => RedisHelper.CreateScope(tracer, new IntegrationInfo(integration), null, null, null);

        private static Scope SqlCommandCreateScope(Tracer tracer, int integration) => ScopeFactory.CreateDbCommandScope(tracer, new SqlCommand());

        private static Scope PostgresCreateScope(Tracer tracer, int integration) => ScopeFactory.CreateDbCommandScope(tracer, new NpgsqlCommand());
    }
}
