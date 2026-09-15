// <copyright file="JsonArrayPoolAnalyzerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#pragma warning disable CA1707 // Test method names contain underscores by convention

extern alias AnalyzerCodeFixes;

using System.Threading.Tasks;
using Datadog.Trace.Tools.Analyzers.AllocationAnalyzers;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using Verifier = Microsoft.CodeAnalysis.CSharp.Testing.CSharpCodeFixVerifier<
    Datadog.Trace.Tools.Analyzers.AllocationAnalyzers.JsonArrayPoolAnalyzer,
    AnalyzerCodeFixes::Datadog.Trace.Tools.Analyzers.AllocationAnalyzers.JsonArrayPoolCodeFixProvider,
    Microsoft.CodeAnalysis.Testing.DefaultVerifier>;

namespace Datadog.Trace.Tools.Analyzers.Tests.AllocationAnalyzers;

public class JsonArrayPoolAnalyzerTests
{
    // Stub types that simulate the vendored Newtonsoft.Json types and JsonArrayPool
    private const string StubTypes = """
        namespace Datadog.Trace.Vendors.Newtonsoft.Json
        {
            public interface IArrayPool<T>
            {
                T[] Rent(int minimumLength);
                void Return(T[] array);
            }

            public class JsonTextReader
            {
                public JsonTextReader(System.IO.TextReader reader) { }
                public IArrayPool<char> ArrayPool { get; set; }
                public bool CloseInput { get; set; }
            }

            public class JsonTextWriter
            {
                public JsonTextWriter(System.IO.TextWriter writer) { }
                public IArrayPool<char> ArrayPool { get; set; }
                public int Formatting { get; set; }
            }
        }

        namespace Datadog.Trace.Util.Json
        {
            public sealed class JsonArrayPool : Datadog.Trace.Vendors.Newtonsoft.Json.IArrayPool<char>
            {
                public static readonly JsonArrayPool Shared = new JsonArrayPool();
                public char[] Rent(int minimumLength) => new char[minimumLength];
                public void Return(char[] array) { }
            }
        }

        """;

    [Fact]
    public async Task JsonTextReader_WithArrayPool_NoDiagnostic()
    {
        var source =
            Usings.Both + StubTypes + """
            class TestClass
            {
                void TestMethod()
                {
                    var sr = new System.IO.StringReader("{}");
                    var reader = new JsonTextReader(sr) { ArrayPool = JsonArrayPool.Shared };
                }
            }
            """;

        await Verifier.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task JsonTextWriter_WithArrayPool_NoDiagnostic()
    {
        var source =
            Usings.Both + StubTypes + """
            class TestClass
            {
                void TestMethod()
                {
                    var sw = new System.IO.StringWriter();
                    var writer = new JsonTextWriter(sw) { ArrayPool = JsonArrayPool.Shared };
                }
            }
            """;

        await Verifier.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task JsonTextReader_WithArrayPoolAndOtherProperties_NoDiagnostic()
    {
        var source =
            Usings.Both + StubTypes + """
            class TestClass
            {
                void TestMethod()
                {
                    var sr = new System.IO.StringReader("{}");
                    var reader = new JsonTextReader(sr) { ArrayPool = JsonArrayPool.Shared, CloseInput = false };
                }
            }
            """;

        await Verifier.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task JsonTextReader_WithOtherArrayPool_NoDiagnostic()
    {
        var source =
            Usings.NewtonsoftOnly + StubTypes + """
            class TestClass
            {
                void TestMethod()
                {
                    IArrayPool<char> pool = null;
                    var sr = new System.IO.StringReader("{}");
                    var reader = new JsonTextReader(sr) { ArrayPool = pool };
                }
            }
            """;

        await Verifier.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task UnrelatedType_NoDiagnostic()
    {
        var source =
            StubTypes + """
            class SomeReader
            {
                public SomeReader(System.IO.TextReader reader) { }
            }

            class TestClass
            {
                void TestMethod()
                {
                    var sr = new System.IO.StringReader("{}");
                    var reader = new SomeReader(sr);
                }
            }
            """;

        await Verifier.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task JsonTextReader_NoInitializer_Diagnostic()
    {
        var source =
            Usings.NewtonsoftOnly + StubTypes + """
            class TestClass
            {
                void TestMethod()
                {
                    var sr = new System.IO.StringReader("{}");
                    var reader = {|#0:new JsonTextReader(sr)|};
                }
            }
            """;

        var fixedSource =
            Usings.BothFixed + StubTypes + """
            class TestClass
            {
                void TestMethod()
                {
                    var sr = new System.IO.StringReader("{}");
                    var reader = new JsonTextReader(sr) { ArrayPool = JsonArrayPool.Shared };
                }
            }
            """;

        var diagnostic = Verifier
            .Diagnostic(Diagnostics.JsonArrayPoolDiagnosticId)
            .WithArguments("JsonTextReader")
            .WithLocation(0);
        await Verifier.VerifyCodeFixAsync(source, diagnostic, fixedSource);
    }

    [Fact]
    public async Task JsonTextWriter_NoInitializer_Diagnostic()
    {
        var source =
            Usings.NewtonsoftOnly + StubTypes + """
            class TestClass
            {
                void TestMethod()
                {
                    var sw = new System.IO.StringWriter();
                    var writer = {|#0:new JsonTextWriter(sw)|};
                }
            }
            """;

        var fixedSource =
            Usings.BothFixed + StubTypes + """
            class TestClass
            {
                void TestMethod()
                {
                    var sw = new System.IO.StringWriter();
                    var writer = new JsonTextWriter(sw) { ArrayPool = JsonArrayPool.Shared };
                }
            }
            """;

        var diagnostic = Verifier
            .Diagnostic(Diagnostics.JsonArrayPoolDiagnosticId)
            .WithArguments("JsonTextWriter")
            .WithLocation(0);
        await Verifier.VerifyCodeFixAsync(source, diagnostic, fixedSource);
    }

    [Fact]
    public async Task JsonTextReader_ExistingInitializerWithoutArrayPool_Diagnostic()
    {
        var source =
            Usings.NewtonsoftOnly + StubTypes + """
            class TestClass
            {
                void TestMethod()
                {
                    var sr = new System.IO.StringReader("{}");
                    var reader = {|#0:new JsonTextReader(sr) { CloseInput = false }|};
                }
            }
            """;

        var fixedSource =
            Usings.BothFixed + StubTypes + """
            class TestClass
            {
                void TestMethod()
                {
                    var sr = new System.IO.StringReader("{}");
                    var reader = new JsonTextReader(sr) { CloseInput = false, ArrayPool = JsonArrayPool.Shared };
                }
            }
            """;

        var diagnostic = Verifier
            .Diagnostic(Diagnostics.JsonArrayPoolDiagnosticId)
            .WithArguments("JsonTextReader")
            .WithLocation(0);
        await Verifier.VerifyCodeFixAsync(source, diagnostic, fixedSource);
    }

    [Fact]
    public async Task JsonTextWriter_ExistingInitializerWithoutArrayPool_Diagnostic()
    {
        var source =
            Usings.NewtonsoftOnly + StubTypes + """
            class TestClass
            {
                void TestMethod()
                {
                    var sw = new System.IO.StringWriter();
                    var writer = {|#0:new JsonTextWriter(sw) { Formatting = 0 }|};
                }
            }
            """;

        var fixedSource =
            Usings.BothFixed + StubTypes + """
            class TestClass
            {
                void TestMethod()
                {
                    var sw = new System.IO.StringWriter();
                    var writer = new JsonTextWriter(sw) { Formatting = 0, ArrayPool = JsonArrayPool.Shared };
                }
            }
            """;

        var diagnostic = Verifier
            .Diagnostic(Diagnostics.JsonArrayPoolDiagnosticId)
            .WithArguments("JsonTextWriter")
            .WithLocation(0);
        await Verifier.VerifyCodeFixAsync(source, diagnostic, fixedSource);
    }

    /// <summary>
    /// Using directive constants for test source code.
    /// </summary>
    private static class Usings
    {
        public const string NewtonsoftOnly = "using Datadog.Trace.Vendors.Newtonsoft.Json;\n";

        public const string Both =
            "using Datadog.Trace.Vendors.Newtonsoft.Json;\nusing Datadog.Trace.Util.Json;\n";

        // After code fix adds the using, it appends after existing usings
        public const string BothFixed =
            "using Datadog.Trace.Vendors.Newtonsoft.Json;\nusing Datadog.Trace.Util.Json;\n";
    }
}
