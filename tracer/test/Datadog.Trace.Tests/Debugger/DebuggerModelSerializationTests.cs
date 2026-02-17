// <copyright file="DebuggerModelSerializationTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using Datadog.Trace.Debugger.Sink.Models;
using Datadog.Trace.Debugger.Symbols.Model;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using FluentAssertions;
using Xunit;
using SymRoot = Datadog.Trace.Debugger.Symbols.Model.Root;
using SymScope = Datadog.Trace.Debugger.Symbols.Model.Scope;

namespace Datadog.Trace.Tests.Debugger;

/// <summary>
/// Baseline serialization tests for Debugger JSON models.
/// These capture the exact JSON format before any JSON library migration.
/// </summary>
public class DebuggerModelSerializationTests
{
    // ===== Sink Models =====

    [Fact]
    public void Status_SerializesAsString()
    {
        // Status enum uses [JsonConverter(typeof(StringEnumConverter))]
        var json = JsonConvert.SerializeObject(Status.INSTALLED);
        json.Should().Be("\"INSTALLED\"");

        var deserialized = JsonConvert.DeserializeObject<Status>("\"EMITTING\"");
        deserialized.Should().Be(Status.EMITTING);
    }

    [Fact]
    public void Status_AllValues_RoundTrip()
    {
        var allValues = new[]
        {
            (Status.RECEIVED, "\"RECEIVED\""),
            (Status.INSTALLED, "\"INSTALLED\""),
            (Status.EMITTING, "\"EMITTING\""),
            (Status.BLOCKED, "\"BLOCKED\""),
            (Status.ERROR, "\"ERROR\""),
            (Status.INSTRUMENTED, "\"INSTRUMENTED\""),
        };

        foreach (var (status, expectedJson) in allValues)
        {
            var json = JsonConvert.SerializeObject(status);
            json.Should().Be(expectedJson);

            var deserialized = JsonConvert.DeserializeObject<Status>(json);
            deserialized.Should().Be(status);
        }
    }

    [Fact]
    public void ProbeException_AllFieldsPopulated_RoundTrips()
    {
        // language=json
        var json = """{"type":"NullReferenceException","message":"Object reference not set","stacktrace":"at Foo.Bar()"}""";
        var result = JsonConvert.DeserializeObject<ProbeException>(json);

        result.Type.Should().Be("NullReferenceException");
        result.Message.Should().Be("Object reference not set");
        result.StackTrace.Should().Be("at Foo.Bar()");

        var reserialized = JsonConvert.SerializeObject(result);
        var result2 = JsonConvert.DeserializeObject<ProbeException>(reserialized);
        result2.Type.Should().Be("NullReferenceException");
    }

    [Fact]
    public void Diagnostics_AllFieldsPopulated_RoundTrips()
    {
        // language=json
        var json = """
            {
                "probeId":"probe-abc",
                "status":"INSTALLED",
                "probeVersion":3,
                "runtimeId":"rt-123",
                "exception":{"type":"Err","message":"msg","stacktrace":"stack"}
            }
            """;

        var result = JsonConvert.DeserializeObject<Diagnostics>(json);

        result.ProbeId.Should().Be("probe-abc");
        result.Status.Should().Be(Status.INSTALLED);
        result.ProbeVersion.Should().Be(3);
        // RuntimeId is always assigned from Util.RuntimeId.Get() in the constructor
        // (no public setter), so the deserialized JSON value is overwritten.
        result.RuntimeId.Should().Be(Datadog.Trace.Util.RuntimeId.Get());
        result.Exception.Should().NotBeNull();
        result.Exception.Type.Should().Be("Err");
    }

    [Fact]
    public void DebuggerDiagnostics_Nested_RoundTrips()
    {
        // language=json
        var json = """{"diagnostics":{"probeId":"p1","status":"ERROR","probeVersion":1,"runtimeId":"r1"}}""";
        var result = JsonConvert.DeserializeObject<DebuggerDiagnostics>(json);

        result.Diagnostics.ProbeId.Should().Be("p1");
        result.Diagnostics.Status.Should().Be(Status.ERROR);

        var reserialized = JsonConvert.SerializeObject(result);
        var result2 = JsonConvert.DeserializeObject<DebuggerDiagnostics>(reserialized);
        result2.Diagnostics.ProbeId.Should().Be("p1");
    }

    // ===== Symbols Models =====

    [Fact]
    public void LanguageSpecifics_AllFieldsPopulated_RoundTrips()
    {
        // language=json
        var json = """
            {
                "access_modifiers":["public","static"],
                "annotations":["ObsoleteAttribute"],
                "super_classes":["BaseClass"],
                "interfaces":["IDisposable","ICloneable"],
                "return_type":"void",
                "start_column":5,
                "end_column":42,
                "pdb_exist":true
            }
            """;

        var result = JsonConvert.DeserializeObject<LanguageSpecifics>(json);

        result.AccessModifiers.Should().BeEquivalentTo(["public", "static"]);
        result.Annotations.Should().ContainSingle().Which.Should().Be("ObsoleteAttribute");
        result.SuperClasses.Should().ContainSingle().Which.Should().Be("BaseClass");
        result.Interfaces.Should().HaveCount(2);
        result.ReturnType.Should().Be("void");
        result.StartColumn.Should().Be(5);
        result.EndColumn.Should().Be(42);
        result.IsPdbExist.Should().BeTrue();

        var reserialized = JsonConvert.SerializeObject(result);
        var result2 = JsonConvert.DeserializeObject<LanguageSpecifics>(reserialized);
        result2.ReturnType.Should().Be("void");
        result2.IsPdbExist.Should().BeTrue();
    }

    [Fact]
    public void LanguageSpecifics_NullFields_RoundTrips()
    {
        var json = @"{}";
        var result = JsonConvert.DeserializeObject<LanguageSpecifics>(json);

        result.AccessModifiers.Should().BeNull();
        result.ReturnType.Should().BeNull();
        result.StartColumn.Should().BeNull();
        result.IsPdbExist.Should().BeNull();
    }

    [Fact]
    public void Symbol_AllFieldsPopulated_RoundTrips()
    {
        // SymbolType uses [JsonConverter(typeof(StringEnumConverter), converterParameters: typeof(SnakeCaseNamingStrategy))]
        // language=json
        var json = """{"name":"myVar","type":"System.String","symbol_type":"local","line":42,"language_specifics":{"return_type":"string"}}""";
        var result = JsonConvert.DeserializeObject<Symbol>(json);

        result.Name.Should().Be("myVar");
        result.Type.Should().Be("System.String");
        result.SymbolType.Should().Be(SymbolType.Local);
        result.Line.Should().Be(42);
        result.LanguageSpecifics.Should().NotBeNull();

        var reserialized = JsonConvert.SerializeObject(result);
        reserialized.Should().Contain("\"symbol_type\":\"local\"");
        var result2 = JsonConvert.DeserializeObject<Symbol>(reserialized);
        result2.SymbolType.Should().Be(SymbolType.Local);
    }

    [Fact]
    public void SymbolType_AllValues_SerializeAsSnakeCase()
    {
        var allValues = new[]
        {
            (SymbolType.Field, "field"),
            (SymbolType.StaticField, "static_field"),
            (SymbolType.Arg, "arg"),
            (SymbolType.Local, "local"),
        };

        foreach (var (symbolType, expectedJsonValue) in allValues)
        {
            var symbol = new Symbol { Name = "x", Type = "int", SymbolType = symbolType };
            var json = JsonConvert.SerializeObject(symbol);
            json.Should().Contain($"\"symbol_type\":\"{expectedJsonValue}\"");
        }
    }

    [Fact]
    public void ScopeType_AllValues_SerializeAsSnakeCase()
    {
        var allValues = new[]
        {
            (ScopeType.Assembly, "assembly"),
            (ScopeType.Class, "class"),
            (ScopeType.Method, "method"),
            (ScopeType.Closure, "closure"),
            (ScopeType.Local, "local"),
        };

        foreach (var (scopeType, expectedJsonValue) in allValues)
        {
            var scope = new SymScope { ScopeType = scopeType, Name = "test", StartLine = 1, EndLine = 10 };
            var json = JsonConvert.SerializeObject(scope);
            json.Should().Contain($"\"scope_type\":\"{expectedJsonValue}\"");
        }
    }

    [Fact]
    public void Scope_WithNestedScopes_RoundTrips()
    {
        // language=json
        var json = """
            {
                "scope_type":"class",
                "name":"MyClass",
                "source_file":"/src/MyClass.cs",
                "start_line":1,
                "end_line":100,
                "language_specifics":{"access_modifiers":["public"]},
                "symbols":[{"name":"field1","type":"int","symbol_type":"field"}],
                "scopes":[{"scope_type":"method","name":"DoWork","start_line":10,"end_line":20}]
            }
            """;

        var result = JsonConvert.DeserializeObject<SymScope>(json);

        result.ScopeType.Should().Be(ScopeType.Class);
        result.Name.Should().Be("MyClass");
        result.SourceFile.Should().Be("/src/MyClass.cs");
        result.StartLine.Should().Be(1);
        result.EndLine.Should().Be(100);
        result.Symbols.Should().ContainSingle();
        result.Symbols[0].Name.Should().Be("field1");
        result.Scopes.Should().ContainSingle();
        result.Scopes[0].ScopeType.Should().Be(ScopeType.Method);
        result.Scopes[0].Name.Should().Be("DoWork");
    }

    [Fact]
    public void Root_AllFieldsPopulated_RoundTrips()
    {
        // language=json
        var json = """
            {
                "service":"my-service",
                "env":"prod",
                "version":"1.0.0",
                "language":"dotnet",
                "scopes":[
                    {"scope_type":"assembly","name":"MyApp","start_line":0,"end_line":0}
                ]
            }
            """;

        var result = JsonConvert.DeserializeObject<SymRoot>(json);

        result.Service.Should().Be("my-service");
        result.Env.Should().Be("prod");
        result.Version.Should().Be("1.0.0");
        result.Language.Should().Be("dotnet");
        result.Scopes.Should().ContainSingle();
        result.Scopes[0].Name.Should().Be("MyApp");

        var reserialized = JsonConvert.SerializeObject(result);
        var result2 = JsonConvert.DeserializeObject<SymRoot>(reserialized);
        result2.Service.Should().Be("my-service");
        result2.Scopes.Should().ContainSingle();
    }
}
