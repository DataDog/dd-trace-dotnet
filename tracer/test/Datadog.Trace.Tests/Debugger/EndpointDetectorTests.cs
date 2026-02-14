// <copyright file="EndpointDetectorTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Datadog.Trace.Debugger.SpanCodeOrigin;
using Datadog.Trace.Pdb;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Text;
using Xunit;
using Xunit.Abstractions;

#if NETCOREAPP3_1_OR_GREATER
using System.Collections.Immutable;
#else
using Datadog.Trace.VendoredMicrosoftCode.System.Collections.Immutable;
#endif

namespace Datadog.Trace.Tests.Debugger
{
    public class EndpointDetectorTests : IDisposable
    {
        private const string TestAssemblyName = "EndpointDetectorTestAssembly";
        private readonly ITestOutputHelper _output;
        private string _assemblyPath;
        private Assembly _assembly;

        public EndpointDetectorTests(ITestOutputHelper output)
        {
            _output = output;
            _assemblyPath = CreateTestAssembly();
            _assembly = Assembly.LoadFile(_assemblyPath);
        }

        public void Dispose()
        {
            try
            {
                if (!string.IsNullOrEmpty(_assemblyPath) && File.Exists(_assemblyPath))
                {
                    File.Delete(_assemblyPath);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        [Fact]
        public void GetEndpointMethodTokens_FindsAllEndpoints()
        {
            // Arrange
            var datadogMetadataReader = DatadogMetadataReader.CreatePdbReader(_assembly);

            // Act
            var endpointTokens = EndpointDetector.GetEndpointMethodTokens(datadogMetadataReader);

            // Create a mapping of method tokens to more friendly names for debugging
            var tokenToMethodMap = new Dictionary<int, string>();
            var expectedEndpoints = new HashSet<int>();

            var namespace_ = "EndpointDetectorTestNamespace";
            foreach (var type in _assembly.GetTypes().Where(t => t.Namespace == namespace_))
            {
                foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
                {
                    var token = method.MetadataToken;
                    var fullMethodName = $"{type.Name}.{method.Name}";
                    tokenToMethodMap[token] = fullMethodName;

                    // Add to expected endpoints for the types and methods we expect to be detected
                    bool isExpectedEndpoint = false;

                    // Controllers with action attributes
                    if (type.Name is "ControllerAttributeController" or "ApiControllerAttributeController" or "RouteAttributeController" &&
                        method.Name == "Get")
                    {
                        isExpectedEndpoint = true;
                    }

                    // Controllers inheriting from base classes
                    else if (type.Name is "ControllerBaseInheritanceController" or "ControllerInheritanceController" &&
                             method.Name == "Get")
                    {
                        isExpectedEndpoint = true;
                    }

                    // Controller with HTTP methods
                    else if (type.Name == "HttpMethodController" &&
                            method.Name is "Get" or "Post" or "Put" or "Delete" or "Patch" or "Head" or "Options" or "Custom")
                    {
                        isExpectedEndpoint = true;
                    }

                    // .NET Framework MVC/WebApi2 controllers (convention-based)
                    else if (type.Name == "NetFxMvcController" && method.Name == "Index")
                    {
                        isExpectedEndpoint = true;
                    }
                    else if (type.Name == "NetFxWebApiController" && method.Name == "Get")
                    {
                        isExpectedEndpoint = true;
                    }

                    // PageModel handlers
                    else if (type.Name == "TestPageModel" &&
                            method.Name is "OnGet" or "OnGetAsync" or "OnPost" or "OnPostAsync" or "OnPut" or "OnPutAsync" or "OnDelete" or "OnDeleteAsync" or "OnHead" or "OnHeadAsync" or "OnPatch" or "OnPatchAsync" or "OnOptions" or "OnOptionsAsync" &&
                            method.Name != "OnGetWithNonHandlerAttribute")
                    {
                        isExpectedEndpoint = true;
                    }

                    // SignalR hub methods
                    else if (type.Name is "TestHub" or "TestGenericHub" &&
                             method.Name is "Send" or "Receive" &&
                             !method.IsStatic)
                    {
                        isExpectedEndpoint = true;
                    }

                    // minimal api endpoints
                    else if (type.Name.Contains("<>") || type.Name.Contains("__DisplayClass"))
                    {
                        if (method.Name.Contains("b__"))
                        {
                            isExpectedEndpoint = true;
                        }
                    }

                    if (isExpectedEndpoint)
                    {
                        expectedEndpoints.Add(token);
                    }
                }
            }

            // Log and assert
            LogEndpointDetails(endpointTokens, tokenToMethodMap, expectedEndpoints);

            endpointTokens.Count.Should().BeGreaterThanOrEqualTo(expectedEndpoints.Count, "The EndpointDetector should find all controller, page handler, and SignalR hub methods");
        }

        [Fact]
        public void GetEndpointMethodTokens_DoesNotDetectNonEndpoints()
        {
            // Arrange
            var datadogMetadataReader = DatadogMetadataReader.CreatePdbReader(_assembly);

            // Act
            var endpointTokens = EndpointDetector.GetEndpointMethodTokens(datadogMetadataReader);

            // Create a list of methods that should NOT be endpoints
            var nonEndpointTokens = new List<int>();
            var nonEndpointMethods = new List<(string TypeName, string MethodName, int Token)>();

            var namespace_ = "EndpointDetectorTestNamespace";
            foreach (var type in _assembly.GetTypes().Where(t => t.Namespace == namespace_))
            {
                foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
                {
                    var token = method.MetadataToken;
                    bool shouldNotBeEndpoint = false;

                    // Abstract controllers
                    if (type.Name == "AbstractController")
                    {
                        shouldNotBeEndpoint = true;
                    }

                    // Interface methods
                    else if (type.Name == "ControllerInterface")
                    {
                        shouldNotBeEndpoint = true;
                    }

                    // Private methods
                    else if (method.IsPrivate)
                    {
                        shouldNotBeEndpoint = true;
                    }

                    // Static methods
                    else if (method.IsStatic)
                    {
                        shouldNotBeEndpoint = true;
                    }

                    // Regular methods with no HTTP attribute
                    else if (type.Name == "ValidController" && method.Name == "RegularMethod")
                    {
                        shouldNotBeEndpoint = true;
                    }

                    // .NET Framework MVC/WebApi2 non-actions
                    else if (type.Name is "NetFxMvcController" or "NetFxWebApiController" && method.Name == "Helper")
                    {
                        shouldNotBeEndpoint = true;
                    }

                    // PageModel methods with NonHandler attribute
                    else if (type.Name == "TestPageModel" && method.Name == "OnGetWithNonHandlerAttribute")
                    {
                        shouldNotBeEndpoint = true;
                    }

                    // Regular methods in PageModels
                    else if (type.Name == "TestPageModel" && method.Name == "RegularMethod")
                    {
                        shouldNotBeEndpoint = true;
                    }

                    if (shouldNotBeEndpoint)
                    {
                        nonEndpointTokens.Add(token);
                        nonEndpointMethods.Add((type.Name, method.Name, token));
                    }
                }
            }

            // Log which non-endpoints were incorrectly detected
            var incorrectlyDetectedEndpoints = nonEndpointMethods
                .Where(e => endpointTokens.Contains(e.Token))
                .ToList();

            _output.WriteLine($"Found {incorrectlyDetectedEndpoints.Count} incorrectly detected endpoints:");
            foreach (var endpoint in incorrectlyDetectedEndpoints)
            {
                _output.WriteLine($"  {endpoint.TypeName}.{endpoint.MethodName} (Token: {endpoint.Token})");
            }

            // Assert that none of the non-endpoints were detected
            nonEndpointTokens.Should().NotContain(token => endpointTokens.Contains(token), "Non-endpoints should not be detected as endpoints");
        }

        private void LogEndpointDetails(ImmutableHashSet<int> endpointTokens, Dictionary<int, string> tokenToMethodMap, HashSet<int> expectedEndpoints)
        {
            // Log expected endpoints (using the HashSet to avoid duplicates)
            _output.WriteLine($"\nExpected {expectedEndpoints.Count} endpoints:");
            foreach (var token in expectedEndpoints)
            {
                bool found = endpointTokens.Contains(token);
                string methodName = tokenToMethodMap.TryGetValue(token, out var value) ? value : $"Unknown ({token})";
                _output.WriteLine($"  {methodName} (Token: {token}) - {(found ? "FOUND" : "NOT FOUND")}");
            }

            // Log which expected endpoints were not found
            var missingEndpoints = expectedEndpoints.Where(e => !endpointTokens.Contains(e)).ToList();
            if (missingEndpoints.Any())
            {
                _output.WriteLine($"\nMissing endpoints ({missingEndpoints.Count}):");
                foreach (var token in missingEndpoints)
                {
                    string methodName = tokenToMethodMap.TryGetValue(token, out var value) ? value : $"Unknown ({token})";
                    _output.WriteLine($"  {methodName} (Token: {token})");
                }
            }

            // Log unexpected endpoints that were found
            var unexpectedEndpoints = endpointTokens.Where(t => !expectedEndpoints.Contains(t)).ToList();
            if (unexpectedEndpoints.Any())
            {
                // due to the way we are identifying minimal api endpoints, we are getting also unexpected method, if this number is too high we might want to rethink our minimal api endpoints detection
                _output.WriteLine($"\nUnexpected endpoints ({unexpectedEndpoints.Count}):");
                foreach (var token in unexpectedEndpoints)
                {
                    string methodName = tokenToMethodMap.TryGetValue(token, out var value) ? value : $"Unknown ({token})";
                    _output.WriteLine($"  {methodName} (Token: {token})");
                }
            }
        }

        private string CreateTestAssembly()
        {
            var compilation = CSharpCompilation.Create(
                TestAssemblyName,
                syntaxTrees: new[] { CSharpSyntaxTree.ParseText(GetTestAssemblyCode()) },
                references: GetMetadataReferences(),
                options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            var uniqueId = Guid.NewGuid().ToString("N").Substring(0, 8);
            var assemblyPath = Path.Combine(Path.GetTempPath(), $"{TestAssemblyName}_{uniqueId}.dll");

            EmitResult emitResult = compilation.Emit(assemblyPath);
            if (!emitResult.Success)
            {
                string errors = string.Join(
                    Environment.NewLine,
                    emitResult.Diagnostics
                              .Where(d => d.Severity == DiagnosticSeverity.Error)
                              .Select(d => $"{d.Id}: {d.GetMessage()}"));

                throw new InvalidOperationException($"Failed to create test assembly: {errors}");
            }

            return assemblyPath;
        }

        private MetadataReference[] GetMetadataReferences()
        {
            return
            [
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Task).Assembly.Location),
                MetadataReference.CreateFromFile(Path.Combine(Path.GetDirectoryName(typeof(object).Assembly.Location), "System.Runtime.dll"))
            ];
        }

        private SourceText GetTestAssemblyCode()
        {
            return SourceText.From(@"
using System;
using System.Threading.Tasks;

namespace Microsoft.AspNetCore.Mvc
{
    [AttributeUsage(AttributeTargets.Class)]
    public class ControllerAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Class)]
    public class ApiControllerAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class RouteAttribute : Attribute
    {
        public RouteAttribute(string template = null) { }
    }

    [AttributeUsage(AttributeTargets.Method)]
    public class HttpGetAttribute : Attribute 
    {
        public HttpGetAttribute() { }
        public HttpGetAttribute(string template) { }
    }

    [AttributeUsage(AttributeTargets.Method)]
    public class HttpPostAttribute : Attribute 
    {
        public HttpPostAttribute() { }
        public HttpPostAttribute(string template) { }
    }

    [AttributeUsage(AttributeTargets.Method)]
    public class HttpPutAttribute : Attribute 
    {
        public HttpPutAttribute() { }
        public HttpPutAttribute(string template) { }
    }

    [AttributeUsage(AttributeTargets.Method)]
    public class HttpDeleteAttribute : Attribute 
    {
        public HttpDeleteAttribute() { }
        public HttpDeleteAttribute(string template) { }
    }

    [AttributeUsage(AttributeTargets.Method)]
    public class HttpPatchAttribute : Attribute 
    {
        public HttpPatchAttribute() { }
        public HttpPatchAttribute(string template) { }
    }

    [AttributeUsage(AttributeTargets.Method)]
    public class HttpHeadAttribute : Attribute 
    {
        public HttpHeadAttribute() { }
        public HttpHeadAttribute(string template) { }
    }

    [AttributeUsage(AttributeTargets.Method)]
    public class HttpOptionsAttribute : Attribute 
    {
        public HttpOptionsAttribute() { }
        public HttpOptionsAttribute(string template) { }
    }

    namespace Routing
    {
        [AttributeUsage(AttributeTargets.Method)]
        public class HttpMethodAttribute : Attribute 
        {
            public HttpMethodAttribute() { }
            public HttpMethodAttribute(string template) { }
        }
    }

    public class ControllerBase { }

    public class Controller : ControllerBase { }
}

namespace Microsoft.AspNetCore.Mvc.RazorPages
{
    [AttributeUsage(AttributeTargets.Method)]
    public class NonHandlerAttribute : Attribute { }

    public class PageModel { }
}

namespace Microsoft.AspNetCore.SignalR
{
    public class Hub { }

    public class Hub<T> { }
}

namespace Microsoft.AspNetCore.Builder
{
    public static class MinimalApiExtensions
    {
        [AttributeUsage(AttributeTargets.Method)]
        public class HttpMethodAttribute : Attribute
        {
            public HttpMethodAttribute(string httpMethod) { }
        }
        
        public static void MapGet(this object app, string pattern, Delegate handler) { }
        public static void MapPost(this object app, string pattern, Delegate handler) { }
    }
}

namespace System.Web.Mvc
{
    [AttributeUsage(AttributeTargets.Method)]
    public class NonActionAttribute : Attribute { }

    public class ControllerBase { }

    public class Controller : ControllerBase { }
}

namespace System.Web.Http
{
    [AttributeUsage(AttributeTargets.Method)]
    public class NonActionAttribute : Attribute { }

    public class ApiController { }
}

namespace EndpointDetectorTestNamespace
{
    // Controller with ControllerAttribute
    [Microsoft.AspNetCore.Mvc.Controller]
    public class ControllerAttributeController
    {
        [Microsoft.AspNetCore.Mvc.HttpGet]
        public object Get() => null;
    }

    // Controller with ApiControllerAttribute
    [Microsoft.AspNetCore.Mvc.ApiControllerAttribute]
    public class ApiControllerAttributeController
    {
        [Microsoft.AspNetCore.Mvc.HttpGet]
        public object Get() => null;
    }

    // Controller with RouteAttribute
    [Microsoft.AspNetCore.Mvc.Route(""api/test"")]
    public class RouteAttributeController
    {
        [Microsoft.AspNetCore.Mvc.HttpGet]
        public object Get() => null;
    }

    // Controller inheriting from ControllerBase
    public class ControllerBaseInheritanceController : Microsoft.AspNetCore.Mvc.ControllerBase
    {
        [Microsoft.AspNetCore.Mvc.HttpGet]
        public object Get() => null;
    }

    // Controller inheriting from Controller
    public class ControllerInheritanceController : Microsoft.AspNetCore.Mvc.Controller
    {
        [Microsoft.AspNetCore.Mvc.HttpGet]
        public object Get() => null;
    }

    // Controller with various HTTP method attributes
    public class HttpMethodController : Microsoft.AspNetCore.Mvc.ControllerBase
    {
        [Microsoft.AspNetCore.Mvc.HttpGet]
        public object Get() => null;

        [Microsoft.AspNetCore.Mvc.HttpPost]
        public object Post() => null;

        [Microsoft.AspNetCore.Mvc.HttpPut]
        public object Put() => null;

        [Microsoft.AspNetCore.Mvc.HttpDelete]
        public object Delete() => null;

        [Microsoft.AspNetCore.Mvc.HttpPatch]
        public object Patch() => null;

        [Microsoft.AspNetCore.Mvc.HttpHead]
        public object Head() => null;

        [Microsoft.AspNetCore.Mvc.HttpOptions]
        public object Options() => null;

        [Microsoft.AspNetCore.Mvc.Routing.HttpMethodAttribute]
        public object Custom() => null;
    }

    // PageModel with handler methods
    public class TestPageModel : Microsoft.AspNetCore.Mvc.RazorPages.PageModel
    {
        public void OnGet() { }
        public Task OnGetAsync() => Task.CompletedTask;
        public void OnPost() { }
        public Task OnPostAsync() => Task.CompletedTask;
        public void OnPut() { }
        public Task OnPutAsync() => Task.CompletedTask;
        public void OnDelete() { }
        public Task OnDeleteAsync() => Task.CompletedTask;
        public void OnHead() { }
        public Task OnHeadAsync() => Task.CompletedTask;
        public void OnPatch() { }
        public Task OnPatchAsync() => Task.CompletedTask;
        public void OnOptions() { }
        public Task OnOptionsAsync() => Task.CompletedTask;

        [Microsoft.AspNetCore.Mvc.RazorPages.NonHandler]
        public void OnGetWithNonHandlerAttribute() { }
        
        // Regular method - should not be detected
        public void RegularMethod() { }
    }

    // SignalR Hub
    public class TestHub : Microsoft.AspNetCore.SignalR.Hub
    {
        public void Send(string message) { }
        public void Receive() { }
        public static void StaticMethod() { }
    }

    // Generic SignalR Hub
    public class TestGenericHub : Microsoft.AspNetCore.SignalR.Hub<string>
    {
        public void Send(string message) { }
        public void Receive() { }
    }

    // Abstract controller - should be ignored
    [Microsoft.AspNetCore.Mvc.ApiControllerAttribute]
    public abstract class AbstractController
    {
        [Microsoft.AspNetCore.Mvc.HttpGet]
        public abstract object Get();
    }

    // Interface controller - should be ignored
    public interface ControllerInterface
    {
        // Even though we can't apply the ApiControllerAttribute to an interface,
        // the EndpointDetector should still ignore interfaces
        object Get();
    }

    // Valid controller with methods that should be excluded
    public class ValidController : Microsoft.AspNetCore.Mvc.ControllerBase
    {
        // Private method - should be excluded
        [Microsoft.AspNetCore.Mvc.HttpGet(""private"")]
        private object PrivateMethod() => null;

        // Static method - should be excluded
        [Microsoft.AspNetCore.Mvc.HttpGet(""static"")]
        public static object StaticMethod() => null;

        // Regular method with no HTTP attribute - should be excluded
        public object RegularMethod() => null;
    }

    // Minimal API setup to generate compiler-generated methods
    public static class MinimalApiProgram
    {
        // Define some delegate types for our handlers
        public delegate object RequestHandler();
        public delegate object RequestHandlerWithInput(object input);
        
        public static void SetupEndpoints()
        {
            // These lambdas will compile into methods with names like ""<SetupEndpoints>b__0_0""
            RequestHandler getHandler = () => ""Hello World"";
            RequestHandlerWithInput postHandler = (input) => $""Received {input}"";
            
            // Use the handlers to prevent compiler optimizations
            var app = new object();
            UseHandlers(app, getHandler, postHandler);
        }
        
        // Method to use the handlers (prevents compiler from optimizing away)
        public static void UseHandlers(object app, RequestHandler handler1, RequestHandlerWithInput handler2)
        {
            // This empty method ensures the delegates are used
        }
    }

    // .NET Framework MVC controller
    public class NetFxMvcController : System.Web.Mvc.Controller
    {
        public object Index() => null;

        [System.Web.Mvc.NonAction]
        public object Helper() => null;

        private object PrivateMethod() => null;

        public static object StaticMethod() => null;
    }

    // .NET Framework Web API 2 controller
    public class NetFxWebApiController : System.Web.Http.ApiController
    {
        public object Get() => null;

        [System.Web.Http.NonAction]
        public object Helper() => null;
    }
}");
        }
    }
}
