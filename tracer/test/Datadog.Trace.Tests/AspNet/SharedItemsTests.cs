// <copyright file="SharedItemsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK
using System.IO;
using System.Web;
using Datadog.Trace.AspNet;
using Xunit;

namespace Datadog.Trace.Tests.AspNet
{
    public class SharedItemsTests
    {
        [Fact]
        public void TestEmpty()
        {
            // Arrange
            const string key = "key";
            var context = CreateContext();

            // Assert
            Assert.Null(SharedItems.TryPopScope(context, key));
        }

        [Fact]
        public void TestOneItem()
        {
            // Arrange
            const string key = "key";
            var scope = new Scope(null, null, null, false);
            var context = CreateContext();

            // Act
            SharedItems.PushScope(context, key, scope);

            // Assert
            Assert.Equal(scope, SharedItems.TryPopScope(context, key));
        }

        [Fact]
        public void TestStackingItems()
        {
            // Arrange
            const string key = "key";
            var scope1 = new Scope(null, null, null, false);
            var scope2 = new Scope(scope1, null, null, false);
            var scope3 = new Scope(scope2, null, null, false);
            var context = CreateContext();
            // Act
            SharedItems.PushScope(context, key, scope1);
            SharedItems.PushScope(context, key, scope2);
            SharedItems.PushScope(context, key, scope3);

            // Assert
            Assert.Equal(scope3, SharedItems.TryPopScope(context, key));
            Assert.Equal(scope2, SharedItems.TryPopScope(context, key));
            Assert.Equal(scope1, SharedItems.TryPopScope(context, key));
        }

        private HttpContext CreateContext()
        {
            return new HttpContext(new HttpRequest(string.Empty, "http://datadog.com", string.Empty), new HttpResponse(new StringWriter()));
        }
    }
}
#endif
