// <copyright file="InstrumentationUnitTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Linq;
using Datadog.Trace.ClrProfiler;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Security.Unit.Tests.RASP;

public class InstrumentationUnitTests
{
    [Fact]
    public void TestRaspInstrumentation()
    {
        var aspects = Instrumentation.GetRaspAspects(AspectDefinitions.Aspects, out var error);
        error.Should().BeFalse();
    }

    [Fact]
    public void TestRaspAspectDefinitions()
    {
        var aspects = new string[]
        {
"[AspectClass(\"EntityFramework\",[None],Propagation,[])] Datadog.Trace.Iast.Aspects.EntityCommandAspect",
"  [AspectMethodInsertBefore(\"System.Data.Entity.Core.EntityClient.EntityCommand::ExecuteReader(System.Data.CommandBehavior)\",\"\",[1],[False],[None],Default,[])] ReviewSqlCommand(System.Object)",
"  [AspectMethodInsertBefore(\"System.Data.Entity.Core.EntityClient.EntityCommand::ExecuteReaderAsync(System.Data.CommandBehavior,System.Threading.CancellationToken)\",\"\",[2],[False],[None],Default,[])] ReviewSqlCommand(System.Object)",
"  [AspectMethodInsertBefore(\"System.Data.Entity.Core.EntityClient.EntityCommand::ExecuteReaderAsync(System.Data.CommandBehavior)\",\"\",[1],[False],[None],Default,[])] ReviewSqlCommand(System.Object)",
"[AspectClass(\"Microsoft.AspNetCore.Http\",[None],Sink,[UnvalidatedRedirect])] Datadog.Trace.Iast.Aspects.AspNetCore.Http.HttpResponseAspect",
"  [AspectMethodInsertBefore(\"Microsoft.AspNetCore.Http.HttpResponse::Redirect(System.String)\",\"\",[0],[False],[None],Default,[])] Redirect(System.String)",
"  [AspectMethodInsertBefore(\"Microsoft.AspNetCore.Http.HttpResponse::Redirect(System.String,System.Boolean)\",\"\",[1],[False],[None],Default,[])] Redirect(System.String)",
"[AspectClass(\"Microsoft.AspNetCore.Mvc\",[None],Sink,[UnvalidatedRedirect])] Datadog.Trace.Iast.Aspects.AspNetCore.Mvc.ControllerBaseAspect",
"  [AspectMethodInsertBefore(\"Microsoft.AspNetCore.Mvc.ControllerBase::Redirect(System.String)\",\"\",[0],[False],[None],Default,[])] Redirect(System.String)",
"  [AspectMethodInsertBefore(\"Microsoft.AspNetCore.Mvc.ControllerBase::RedirectPermanent(System.String)\",\"\",[0],[False],[None],Default,[])] Redirect(System.String)",
"  [AspectMethodInsertBefore(\"Microsoft.AspNetCore.Mvc.ControllerBase::RedirectPreserveMethod(System.String)\",\"\",[0],[False],[None],Default,[])] Redirect(System.String)",
"  [AspectMethodInsertBefore(\"Microsoft.AspNetCore.Mvc.ControllerBase::RedirectPermanentPreserveMethod(System.String)\",\"\",[0],[False],[None],Default,[])] Redirect(System.String)",
"  [AspectMethodInsertBefore(\"Microsoft.AspNetCore.Mvc.ControllerBase::LocalRedirect(System.String)\",\"\",[0],[False],[None],Default,[])] Redirect(System.String)",
"  [AspectMethodInsertBefore(\"Microsoft.AspNetCore.Mvc.ControllerBase::LocalRedirectPermanent(System.String)\",\"\",[0],[False],[None],Default,[])] Redirect(System.String)",
"  [AspectMethodInsertBefore(\"Microsoft.AspNetCore.Mvc.ControllerBase::LocalRedirectPreserveMethod(System.String)\",\"\",[0],[False],[None],Default,[])] Redirect(System.String)",
"  [AspectMethodInsertBefore(\"Microsoft.AspNetCore.Mvc.ControllerBase::LocalRedirectPermanentPreserveMethod(System.String)\",\"\",[0],[False],[None],Default,[])] Redirect(System.String)",
"[AspectClass(\"MongoDB.Bson\",[None],Sink,[NoSqlMongoDbInjection])] Datadog.Trace.Iast.Aspects.MongoDB.BsonAspect",
"  [AspectMethodInsertBefore(\"MongoDB.Bson.Serialization.BsonSerializer::Deserialize(System.String,System.Action`1<Builder>)\",\"\",[1],[False],[None],Default,[])] AnalyzeJsonString(System.String)",
"  [AspectMethodInsertBefore(\"MongoDB.Bson.Serialization.BsonSerializer::Deserialize(System.String,System.Type,System.Action`1<Builder>)\",\"\",[2],[False],[None],Default,[])] AnalyzeJsonString(System.String)",
"  [AspectMethodInsertBefore(\"MongoDB.Bson.BsonDocument::Parse(System.String)\",\"\",[0],[False],[None],Default,[])] AnalyzeJsonString(System.String)",
"  [AspectMethodInsertBefore(\"MongoDB.Bson.IO.JsonReader::.ctor(System.String)\",\"\",[0],[False],[None],Default,[])] AnalyzeJsonString(System.String)",
"[AspectClass(\"System,System.Runtime\",[StringOptimization],Propagation,[])] Datadog.Trace.Iast.Aspects.System.UriAspect",
"  [AspectCtorReplace(\"System.Uri::.ctor(System.String)\",\"\",[0],[False],[StringLiteral_1],Default,[])] Init(System.String)",
"  [AspectCtorReplace(\"System.Uri::.ctor(System.Uri,System.String,System.Boolean)\",\"\",[0],[False],[None],Default,[])] Init(System.Uri,System.String,System.Boolean)",
"  [AspectCtorReplace(\"System.Uri::.ctor(System.Uri,System.String)\",\"\",[0],[False],[None],Default,[])] Init(System.Uri,System.String)",
"  [AspectCtorReplace(\"System.Uri::.ctor(System.Uri,System.Uri)\",\"\",[0],[False],[None],Default,[])] Init(System.Uri,System.Uri)",
"  [AspectCtorReplace(\"System.Uri::.ctor(System.String,System.Boolean)\",\"\",[0],[False],[StringLiteral_1],Default,[])] Init(System.String,System.Boolean)",
"  [AspectCtorReplace(\"System.Uri::.ctor(System.String,System.UriKind)\",\"\",[0],[False],[StringLiteral_1],Default,[])] Init(System.String,System.UriKind)",
        };

        var raspAspects = Instrumentation.GetRaspAspects(aspects, out var error);
        error.Should().BeFalse();
        raspAspects.Should().NotBeNull();
        raspAspects.Count().Should().Be(17);
    }
}
