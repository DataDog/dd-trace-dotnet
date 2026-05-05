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
            { "FormValue", "View" },
        };

        private static readonly RouteValueDictionary Defaults = new()
        {
            { "controller", "Home" },
            { "action", "Index" },
            { "id", new object() }, // it won't necessarily be a string in here, e.g. for optional values
            { "FormValue", "View" }
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
        [InlineData("{controller}/{action}/{FormValue}", "/home/index/{formvalue}", false)]
        [InlineData("{controller}/{action}/{FormValue}", "/home/index/view", true)]
        [InlineData("{controller}/{action}/{FormValue?}", "/home/index/{formvalue?}", false)]
        [InlineData("{controller}/{action}/{FormValue?}", "/home/index/view", true)]
        [InlineData("{controller}/{action}/{FormValue=Edit}", "/home/index/{formvalue=edit}", false)]
        [InlineData("{controller}/{action}/{FormValue=Edit}", "/home/index/view", true)]
        [InlineData("{controller}/{action}/{FormValue:int}", "/home/index/{formvalue:int}", false)]
        [InlineData("{controller}/{action}/{FormValue:int}", "/home/index/view", true)]
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
        [InlineData("{controller}/{action}/{FormValue}", "/home/index/{formvalue}", false)]
        [InlineData("{controller}/{action}/{FormValue}", "/home/index/view", true)]
        [InlineData("{controller}/{action}/{FormValue?}", "/home/index/{formvalue?}", false)]
        [InlineData("{controller}/{action}/{FormValue?}", "/home/index/view", true)]
        [InlineData("{controller}/{action}/{FormValue=2}", "/home/index/{formvalue=2}", false)]
        [InlineData("{controller}/{action}/{FormValue=2}", "/home/index/view", true)]
        [InlineData("{controller}/{action}/{FormValue:int}", "/home/index/{formvalue:int}", false)]
        [InlineData("{controller}/{action}/{FormValue:int}", "/home/index/view", true)]
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

        /// <summary>
        /// Verifies that when expandRouteTemplates is true, the route path extracted
        /// from the resource name is consistent and can be used as the http.route tag value.
        /// This is the pattern used by AspNetMvcIntegration and AspNetWebApi2Integration
        /// to keep http.route consistent with resource_name.
        /// </summary>
        [Theory]
        [InlineData("{tenantId}/{controller}/{id}", "/home/index/{id}", false, "{tenantId}/{controller}/{id}")]
        [InlineData("{tenantId}/{controller}/{id}", "/home/index/{id}", true, "/{tenantid}/home/{id}")]
        [InlineData("{controller}/{action}/{nonid}", "/home/index/{nonid}", false, "{controller}/{action}/{nonid}")]
        [InlineData("{controller}/{action}/{nonid}", "/home/index/oops", true, "/home/index/oops")]
        [InlineData("{controller}", "/home", false, "{controller}")]
        [InlineData("{controller}", "/home", true, "/home")]
        public void ExpandedRoute_ExtractedFromResourceName_IsConsistentWithResourceName(
            string template, string expectedRoute, bool expandRouteTemplates, string expectedHttpRouteWhenNotExpanded)
        {
            const string method = "GET";

            var routeValues = new RouteValueDictionary
            {
                { "controller", "Home" },
                { "action", "Index" },
                { "tenantId", "abc123" },
                { "nonid", "oops" },
                { "id", 42 },
            };

            var resource = AspNetResourceNameHelper.CalculateResourceName(
                httpMethod: method,
                routeTemplate: template,
                routeValues: routeValues,
                defaults: null,
                out _,
                out _,
                out _,
                expandRouteTemplates);

            // Extract route from resource name (same pattern used in the integration code)
            string httpRouteValue = template;
            if (expandRouteTemplates && resource != null)
            {
                var spaceIndex = resource.IndexOf(' ');
                if (spaceIndex >= 0 && spaceIndex + 1 < resource.Length)
                {
                    httpRouteValue = resource.Substring(spaceIndex + 1);
                }
            }

            using var a = new AssertionScope();
            resource.Should().Be($"{method} {expectedRoute}");

            if (expandRouteTemplates)
            {
                // When expanding, http.route should match the route part of resource_name
                httpRouteValue.Should().Be(expectedRoute,
                    "http.route should be consistent with resource_name when expandRouteTemplates is true");
            }
            else
            {
                // When not expanding, http.route stays as the raw template
                httpRouteValue.Should().Be(template,
                    "http.route should remain as the raw route template when expandRouteTemplates is false");
            }
        }

        /// <summary>
        /// Regression test: verifies that with a generic {controller} route template,
        /// multiple different controllers produce distinct http.route values when
        /// expandRouteTemplates is true, preventing duplicate endpoints in the UI.
        /// </summary>
        [Fact]
        public void ExpandedRoute_DifferentControllers_ProduceDifferentHttpRoutes()
        {
            const string method = "GET";
            const string template = "api/{controller}/{id}";

            var controllers = new[] { "users", "orders", "products", "settings" };
            var httpRoutes = new System.Collections.Generic.HashSet<string>();

            foreach (var controllerName in controllers)
            {
                var routeValues = new RouteValueDictionary
                {
                    { "controller", controllerName },
                    { "id", 42 },
                };

                var resource = AspNetResourceNameHelper.CalculateResourceName(
                    httpMethod: method,
                    routeTemplate: template,
                    routeValues: routeValues,
                    defaults: null,
                    out _,
                    out _,
                    out _,
                    expandRouteTemplates: true);

                // Extract the route from the resource name
                var spaceIndex = resource.IndexOf(' ');
                var httpRouteValue = resource.Substring(spaceIndex + 1);

                httpRoutes.Add(httpRouteValue);

                // Each controller should produce a unique route containing the controller name
                httpRouteValue.Should().Contain(controllerName,
                    $"http.route should contain the expanded controller name '{controllerName}'");
            }

            // All routes should be distinct (no duplicates)
            httpRoutes.Should().HaveCount(controllers.Length,
                "each controller should produce a distinct http.route value");
        }
    }
}
#endif
