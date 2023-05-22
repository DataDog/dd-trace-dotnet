// <copyright file="IastRequestContextTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using Datadog.Trace.AppSec;
using Datadog.Trace.Iast;
using FluentAssertions;
using Moq;
using Xunit;

namespace Datadog.Trace.Security.Unit.Tests.IAST
{
    public class IastRequestContextTests
    {
        private static string _value = "value1";

        public static IEnumerable<object[]> GetRequestBodyTestData()
        {
            yield return new object[] { new BodyClassTest() { Value = _value } };
            yield return new object[] { new BodyClassTest { BodyClassTestProperty = new() { Value = _value } } };
            yield return new object[] { new BodyClassTest { BodyClassTestProperty = new() { ValueList = new() { _value } } } };
            yield return new object[] { new BodyClassTest { BodyClassTestProperty = new() { ValueListObject = new() { _value } } } };
            yield return new object[] { new BodyClassTest { BodyClassTestProperty = new() { ValueDicObject = new() { { "key", _value } } } } };
            yield return new object[] { new BodyClassTest { BodyClassTestProperty = new() { ValueDicObject = new() { { _value, "values" } } } } };
            yield return new object[] { new BodyClassTest { BodyClassTestProperty = new() { ValueDicObject = new() { { "key", new List<string> { _value } } } } } };
            yield return new object[] { new BodyClassTest { BodyClassTestProperty = new() { ValueDic = new() { { "key", _value } } } } };
            yield return new object[] { new BodyClassTest { BodyClassTestProperty = new() { ValueDic = new() { { _value, "values" } } } } };
            yield return new object[] { new BodyClassTest { BodyClassTestProperty = new(1, _value) } };
        }

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

#if !NETFRAMEWORK
        [Fact]
        public void GivenAnIastRequestContext_WhenAddRequestDataWithHeaders_HeadersAreTainted()
        {
            IastRequestContext iastContext = new();
            // var dictionary = new IHeaderDictionaryForTest();

            var key1 = "key1";
            var key2 = "key2";
            var value1 = "value1";
            var value2 = "value2";
            var value3 = "value3";

            var innerDic = new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>()
            {
                { key1, new Microsoft.Extensions.Primitives.StringValues(new string[] { value1, value2 }) },
                { key2, value3 },
            };
            var dictionary = new Mock<Microsoft.AspNetCore.Http.IHeaderDictionary>();
            Mock<Microsoft.AspNetCore.Http.HttpRequest> request = new();

            var queryMock = new Mock<Microsoft.AspNetCore.Http.IQueryCollection>();
            dictionary.Setup(x => x.GetEnumerator()).Returns(innerDic.GetEnumerator());
            request.Setup(x => x.Headers).Returns(dictionary.Object);

            iastContext.AddRequestData(request.Object, null);
            Assert.NotNull(iastContext.GetTainted(key1));
            Assert.NotNull(iastContext.GetTainted(key2));
            Assert.NotNull(iastContext.GetTainted(value1));
            Assert.NotNull(iastContext.GetTainted(value2));
            Assert.NotNull(iastContext.GetTainted(value3));
        }
#endif

        [Theory]
        [MemberData(nameof(GetRequestBodyTestData))]
        public void GivenAnIastRequestContext_WhenAddRequestBody_ValuesAreTainted(BodyClassTest sample)
        {
            IastRequestContext iastContext = new();
            var extracted = ObjectExtractor.Extract(sample);
            iastContext.AddRequestBody(sample, extracted);
            iastContext.GetTainted(_value).Should().NotBeNull();
        }

        public class BodyClassTest
        {
            public BodyClassTest()
            {
            }

            public BodyClassTest(int index, object value)
            {
                ObjectArray[index] = value;
            }

            public string Value { get; set; }

            public List<string> ValueList { get; set; } = new();

            public List<object> ValueListObject { get; set; } = new();

            public Dictionary<string, string> ValueDic { get; set; } = new();

            public Dictionary<object, object> ValueDicObject { get; set; } = new();

            public object[] ObjectArray { get; set; } = new object[2];

            public BodyClassTest BodyClassTestProperty { get; set; } = null;
        }
    }
}
