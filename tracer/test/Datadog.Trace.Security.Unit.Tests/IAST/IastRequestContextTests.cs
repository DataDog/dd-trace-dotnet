// <copyright file="IastRequestContextTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.IO;
using Datadog.Trace.Iast;
using Moq;
using Xunit;

namespace Datadog.Trace.Security.Unit.Tests.IAST
{
    public class IastRequestContextTests
    {
#if NETFRAMEWORK

        [Fact]
        public void GivenAnIastRequestContext_WhenAddRequestData_DataIsTainted()
        {
            IastRequestContext iastContext = new();
            var path = "/apiname";
            var url = "htpp://site.com" + path;
            string idName = "id";
            var idValue = "idvalue";
            System.Web.HttpRequest request = new("file", url, string.Empty);

            Dictionary<string, object> routeData = new Dictionary<string, object> { { idName, idValue } };
            iastContext.AddRequestData(request, routeData);
            Assert.NotNull(iastContext.GetTainted(request.Path));
            Assert.NotNull(iastContext.GetTainted(idValue));
        }
#else
        [Fact]
        public void GivenAnIastRequestContext_WhenAddRequestData_DataIsTainted()
        {
            IastRequestContext iastContext = new();
            Mock<Microsoft.AspNetCore.Http.HttpRequest> request = new();

            string idName = "id";
            var idValue = "idvalue";
            var queryStringStr = "?var1=4";
            var path = new Microsoft.AspNetCore.Http.PathString("/path");
            var queryMock = new Mock<Microsoft.AspNetCore.Http.IQueryCollection>();
            request.Setup(x => x.Path).Returns(path);
            request.Setup(x => x.QueryString).Returns(new Microsoft.AspNetCore.Http.QueryString(queryStringStr));
            Dictionary<string, object> routeParameters = new()
            {
                { idName, idValue }
            };
            iastContext.AddRequestData(request.Object, routeParameters);
            Assert.NotNull(iastContext.GetTainted(idValue));
            Assert.NotNull(iastContext.GetTainted(path.Value));
            Assert.NotNull(iastContext.GetTainted(queryStringStr));
        }
#endif
    }
}
