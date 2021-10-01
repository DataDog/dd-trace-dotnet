// <copyright file="ServiceNameTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.Configuration;
using NUnit.Framework;

namespace Datadog.Trace.Tests.Configuration
{
    public class ServiceNameTests
    {
        private const string ApplicationName = "MyApplication";
        private readonly ServiceNames _serviceNames;

        public ServiceNameTests()
        {
            _serviceNames = new ServiceNames(new Dictionary<string, string>
            {
                { "sql-server", "custom-db" },
                { "http-client", "some-service" },
                { "mongodb", "my-mongo" },
            });
        }

        [TestCase("sql-server", "custom-db")]
        [TestCase("http-client", "some-service")]
        [TestCase("mongodb", "my-mongo")]
        public void RetrievesMappedServiceNames(string serviceName, string expected)
        {
            var actual = _serviceNames.GetServiceName(ApplicationName, serviceName);

            Assert.AreEqual(expected, actual);
        }

        [TestCase("elasticsearch")]
        [TestCase("postgres")]
        [TestCase("custom-service")]
        public void RetrievesUnmappedServiceNames(string serviceName)
        {
            var expected = $"{ApplicationName}-{serviceName}";

            var actual = _serviceNames.GetServiceName(ApplicationName, serviceName);

            Assert.AreEqual(expected, actual);
        }

        [TestCase("elasticsearch")]
        [TestCase("postgres")]
        [TestCase("custom-service")]
        public void DoesNotRequireAnyMappings(string serviceName)
        {
            var serviceNames = new ServiceNames(new Dictionary<string, string>());
            var expected = $"{ApplicationName}-{serviceName}";

            var actual = serviceNames.GetServiceName(ApplicationName, serviceName);

            Assert.AreEqual(expected, actual);
        }

        [Test]
        public void CanPassNullToConstructor()
        {
            var serviceName = "elasticsearch";
            var expected = $"{ApplicationName}-{serviceName}";
            var serviceNames = new ServiceNames(null);

            var actual = serviceNames.GetServiceName(ApplicationName, serviceName);

            Assert.AreEqual(expected, actual);
        }

        [Test]
        public void CanAddMappingsLater()
        {
            var serviceName = "elasticsearch";
            var expected = "custom-name";
            var serviceNames = new ServiceNames(new Dictionary<string, string>());
            serviceNames.SetServiceNameMappings(new Dictionary<string, string> { { serviceName, expected } });

            var actual = serviceNames.GetServiceName(ApplicationName, serviceName);

            Assert.AreEqual(expected, actual);
        }

        [Test]
        public void ReplacesExistingMappings()
        {
            var serviceNames = new ServiceNames(new Dictionary<string, string>
            {
                { "sql-server", "custom-db" },
                { "elasticsearch", "original-service" },
            });
            serviceNames.SetServiceNameMappings(new Dictionary<string, string> { { "elasticsearch", "custom-name" } });

            var mongodbActual = serviceNames.GetServiceName(ApplicationName, "mongodb");
            var elasticActual = serviceNames.GetServiceName(ApplicationName, "elasticsearch");
            var sqlActual = serviceNames.GetServiceName(ApplicationName, "sql-server");

            Assert.AreEqual($"{ApplicationName}-mongodb", mongodbActual);
            Assert.AreEqual("custom-name", elasticActual);
            Assert.AreEqual($"{ApplicationName}-sql-server", sqlActual);
        }
    }
}
