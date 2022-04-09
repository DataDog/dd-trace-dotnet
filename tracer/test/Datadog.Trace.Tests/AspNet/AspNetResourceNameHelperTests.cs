// <copyright file="AspNetResourceNameHelperTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK
using System.Web.Routing;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.AspNet;
using FluentAssertions;
using FluentAssertions.Execution;
using Xunit;

namespace Datadog.Trace.Tests.AspNet
{
    public class AspNetResourceNameHelperTests
    {
        private static readonly RouteValueDictionary Values = new()
        {
            { "controller", "Home" },
            { "action", "Index" },
            { "nonid", "oops" },
            { "idlike", 123 },
        };

        private static readonly RouteValueDictionary Defaults = new()
        {
            { "controller", "Home" },
            { "action", "Index" },
            { "id", new object() } // it won't necessarily be a string in here, e.g. for optional values
        };

        [Theory]
        [InlineData("{controller}/{action}/{id}", "/home/index/{id}", false)]
        [InlineData("{controller}/{action}/{id}", "/home/index/{id}", true)]
        [InlineData("{controller}/{action}", "/home/index", false)]
        [InlineData("{controller}/{action}", "/home/index", true)]
        [InlineData("prefix/{controller}/{action}", "/prefix/home/index", false)]
        [InlineData("prefix/{controller}/{action}", "/prefix/home/index", true)]
        [InlineData("prefix-{controller}/{action}-suffix", "/prefix-home/index-suffix", false)]
        [InlineData("prefix-{controller}/{action}-suffix", "/prefix-home/index-suffix", true)]
        [InlineData("{controller}/{action}/{nonid}", "/home/index/{nonid}", false)]
        [InlineData("{controller}/{action}/{nonid}", "/home/index/oops", true)]
        [InlineData("{controller}/{action}/{nonid?}", "/home/index/{nonid?}", false)]
        [InlineData("{controller}/{action}/{nonid?}", "/home/index/oops", true)]
        [InlineData("{controller}/{action}/{nonid=2}", "/home/index/{nonid=2}", false)]
        [InlineData("{controller}/{action}/{nonid=2}", "/home/index/oops", true)]
        [InlineData("{controller}/{action}/{nonid:int}", "/home/index/{nonid:int}", false)]
        [InlineData("{controller}/{action}/{nonid:int}", "/home/index/oops", true)]
        [InlineData("{controller}/{action}/{nonidentity}", "/home/index/{nonidentity}", false)]
        [InlineData("{controller}/{action}/{nonidentity}", "/home/index/{nonidentity}", true)]
        [InlineData("{controller}/{action}/{nonidentity?}", "/home/index/{nonidentity?}", false)]
        [InlineData("{controller}/{action}/{nonidentity?}", "/home/index/{nonidentity?}", true)]
        [InlineData("{controller}/{action}/{nonidentity=2}", "/home/index/{nonidentity=2}", false)]
        [InlineData("{controller}/{action}/{nonidentity=2}", "/home/index/{nonidentity=2}", true)]
        [InlineData("{controller}/{action}/{nonidentity:int}", "/home/index/{nonidentity:int}", false)]
        [InlineData("{controller}/{action}/{nonidentity:int}", "/home/index/{nonidentity:int}", true)]
        [InlineData("{controller}/{action}/{idlike}", "/home/index/{idlike}", false)]
        [InlineData("{controller}/{action}/{idlike}", "/home/index/{idlike}", true)]
        [InlineData("{controller}/{action}/{id:int}", "/home/index/{id:int}", false)]
        [InlineData("{controller}/{action}/{id:int}", "/home/index/{id:int}", true)]
        [InlineData("{controller}/{action}/{*nonid}", "/home/index/{*nonid}", false)]
        [InlineData("{controller}/{action}/{*nonid}", "/home/index/oops", true)]
        [InlineData("{controller}", "/home", false)]
        [InlineData("{controller}", "/home", true)]
        public void CalculateResourceName_CleansValidRouteTemplates(string template, string expected, bool expandRouteTemplates)
        {
            const string method = "POST";
            var resource = AspNetResourceNameHelper.CalculateResourceName(
                httpMethod: method,
                routeTemplate: template,
                routeValues: Values,
                defaults: null,
                out var areaName,
                out var controllerName,
                out var actionName,
                expandRouteTemplates);

            using var a = new AssertionScope();
            areaName.Should().BeNull();
            controllerName.Should().Be("home");
            actionName.Should().Be("index");
            resource.Should().Be($"{method} {expected}");
        }

        [Theory]
        [InlineData("{controller}/{action}/{id}", "/home/index", false)]
        [InlineData("{controller}/{action}/{id}", "/home/index", true)]
        [InlineData("{controller}/{action}/{id?}", "/home/index", false)]
        [InlineData("{controller}/{action}/{id?}", "/home/index", true)]
        [InlineData("{controller}/{action}/{id=2}", "/home/index", false)]
        [InlineData("{controller}/{action}/{id=2}", "/home/index", true)]
        [InlineData("{controller}/{action}/{id:int}", "/home/index", false)]
        [InlineData("{controller}/{action}/{id:int}", "/home/index", true)]
        [InlineData("{controller}/{action}/{identity}", "/home/index/{identity}", false)]
        [InlineData("{controller}/{action}/{identity}", "/home/index/{identity}", true)]
        [InlineData("{controller}/{action}", "/home/index", false)]
        [InlineData("{controller}/{action}", "/home/index", true)]
        // Should we support this? I think it's fine not to, and we could add support later if needs be
        [InlineData("{controller}/{*action}", "/home/{*action}", false)]
        [InlineData("{controller}/{*action}", "/home/{*action}", true)]
        [InlineData("{controller}/{action:string}", "/home/{action:string}", false)]
        [InlineData("{controller}/{action:string}", "/home/{action:string}", true)]
        public void CalculateResourceName_CleansValidRouteTemplatesWithDefaults(string template, string expected, bool expandRouteTemplates)
        {
            const string method = "POST";
            var resource = AspNetResourceNameHelper.CalculateResourceName(
                httpMethod: method,
                routeTemplate: template,
                routeValues: Values,
                defaults: Defaults,
                out var areaName,
                out var controllerName,
                out var actionName,
                expandRouteTemplates);

            using var a = new AssertionScope();
            areaName.Should().BeNull();
            controllerName.Should().Be("home");
            actionName.Should().Be("index");
            resource.Should().Be($"{method} {expected}");
        }

        [Theory]
        [InlineData("{controller}/{action}/{id}/{", "/home/index/{", false)]
        [InlineData("{controller}/{action}/{id}/{", "/home/index/{", true)]
        [InlineData("{controller}/{action}/{id}/{something", "/home/index/{something", false)]
        [InlineData("{controller}/{action}/{id}/{something", "/home/index/{something", true)]
        [InlineData("{controller}/{action", "/home/{action", false)]
        [InlineData("{controller}/{action", "/home/{action", true)]
        [InlineData("{controller}/{action}/{noni", "/home/index/{noni", false)]
        [InlineData("{controller}/{action}/{noni", "/home/index/{noni", true)]
        [InlineData("{controller}/{action}/{nonid", "/home/index/{nonid", false)]
        [InlineData("{controller}/{action}/{nonid", "/home/index/{nonid", true)]
        [InlineData("{controller", "/{controller", false)]
        [InlineData("{controller", "/{controller", true)]
        [InlineData("{{controller", "/{{controller", false)]
        [InlineData("{{controller", "/{{controller", true)]
        [InlineData("{controller}}/{}/{action}}", "/home}/{}/index}", false)]
        [InlineData("{controller}}/{}/{action}}", "/home}/{}/index}", true)]
        public void CalculateResourceName_CleansInvalidRouteTemplates(string template, string expected, bool expandRouteTemplates)
        {
            const string method = "POST";
            var resource = AspNetResourceNameHelper.CalculateResourceName(
                httpMethod: method,
                routeTemplate: template,
                routeValues: Values,
                defaults: Defaults,
                out var areaName,
                out var controllerName,
                out var actionName,
                expandRouteTemplates);

            using var a = new AssertionScope();
            resource.Should().Be($"{method} {expected}");
        }
    }
}
#endif
