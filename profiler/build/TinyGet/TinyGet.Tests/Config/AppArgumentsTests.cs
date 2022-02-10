using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using TinyGet.Config;

namespace TinyGet.Tests.Config
{
    [TestFixture]
    public class AppArgumentsTests
    {
        private NameValueCollection _mandatoryArguments;

        [SetUp]
        public void Setup()
        {
            _mandatoryArguments = new NameValueCollection
            {
                {"-srv", "localhost"}
            };
        }

        [Test]
        public void Should_Return_Default_Values()
        {
            // Act
            AppArguments result = AppArguments.Parse(_mandatoryArguments);

            // Assert
            Assert.That(result.Loop, Is.EqualTo(1));
            Assert.That(result.Method, Is.EqualTo(HttpMethod.Get));
            Assert.That(result.Port, Is.EqualTo(80));
            Assert.That(result.Srv, Is.EqualTo("localhost"));
            Assert.That(result.Status, Is.EqualTo(200));
            Assert.That(result.Threads, Is.EqualTo(1));
            Assert.That(result.Uri, Is.EqualTo("/"));
        }

        [Test]
        public void GetUrl_Should_Return_Absolute_Url()
        {
            // Arrange
            NameValueCollection rawArguments = new NameValueCollection
            {
                {"-srv", "test.test"},
                {"-port", "8080"},
                {"-uri", "api/users"}
            };
            AppArguments arguments = AppArguments.Parse(rawArguments);

            // Act
            string result = arguments.GetUrl();

            // Assert
            Assert.That(result, Is.EqualTo("http://test.test:8080/api/users"));
        }
    }
}
