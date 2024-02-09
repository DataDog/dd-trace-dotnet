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
"[AspectClass(\"EntityFramework\",[None],1,[])] Datadog.Trace.Iast.Aspects.EntityCommandAspect",
"  [AspectMethodInsertBefore(\"System.Data.Entity.Core.EntityClient.EntityCommand::ExecuteReader(System.Data.CommandBehavior)\",\"\",[1],[False],[None],0,[])] ReviewSqlCommand(System.Object)",
"  [AspectMethodInsertBefore(\"System.Data.Entity.Core.EntityClient.EntityCommand::ExecuteReaderAsync(System.Data.CommandBehavior,System.Threading.CancellationToken)\",\"\",[2],[False],[None],0,[])] ReviewSqlCommand(System.Object)",
"  [AspectMethodInsertBefore(\"System.Data.Entity.Core.EntityClient.EntityCommand::ExecuteReaderAsync(System.Data.CommandBehavior)\",\"\",[1],[False],[None],0,[])] ReviewSqlCommand(System.Object)",
"[AspectClass(\"Microsoft.AspNetCore.Http\",[None],2,[UnvalidatedRedirect])] Datadog.Trace.Iast.Aspects.AspNetCore.Http.HttpResponseAspect",
"  [AspectMethodInsertBefore(\"Microsoft.AspNetCore.Http.HttpResponse::Redirect(System.String)\",\"\",[0],[False],[None],0,[])] Redirect(System.String)",
"  [AspectMethodInsertBefore(\"Microsoft.AspNetCore.Http.HttpResponse::Redirect(System.String,System.Boolean)\",\"\",[1],[False],[None],0,[])] Redirect(System.String)",
"[AspectClass(\"Microsoft.AspNetCore.Http.Extensions\",[None],2,[TrustBoundaryViolation])] Datadog.Trace.Iast.Aspects.System.Web.SessionState.SessionExtensionsAspect",
"  [AspectMethodInsertBefore(\"Microsoft.AspNetCore.Http.SessionExtensions::SetString(Microsoft.AspNetCore.Http.ISession,System.String,System.String)\",\"\",[0,1],[False,False],[None],0,[])] ReviewTbv(System.String)",
"  [AspectMethodInsertBefore(\"Microsoft.AspNetCore.Http.SessionExtensions::SetInt32(Microsoft.AspNetCore.Http.ISession,System.String,System.Int32)\",\"\",[1],[False],[None],0,[])] ReviewTbv(System.String)",
"[AspectClass(\"System.Web.Mvc\",[None],2,[UnvalidatedRedirect])] Datadog.Trace.Iast.Aspects.System.Web.HttpControllerAspect",
"  [AspectMethodInsertBefore(\"System.Web.Mvc.Controller::Redirect(System.String)\",\"\",[0],[False],[None],0,[])] Redirect(System.String)",
"  [AspectMethodInsertBefore(\"System.Web.Mvc.Controller::RedirectPermanent(System.String)\",\"\",[0],[False],[None],0,[])] Redirect(System.String)",
"[AspectClass(\"mscorlib,System.IO.FileSystem,System.Runtime\",[None],10,[PathTraversal])] Datadog.Trace.Iast.Aspects.FileAspect",
"  [AspectMethodInsertBefore(\"System.IO.File::Create(System.String)\",\"\",[0],[False],[None],0,[])] ReviewPath(System.String)",
"  [AspectMethodInsertBefore(\"System.IO.File::CreateText(System.String)\",\"\",[0],[False],[None],0,[])] ReviewPath(System.String)",
"  [AspectMethodInsertBefore(\"System.IO.File::Delete(System.String)\",\"\",[0],[False],[None],0,[])] ReviewPath(System.String)",
"  [AspectMethodInsertBefore(\"System.IO.File::OpenRead(System.String)\",\"\",[0],[False],[None],0,[])] ReviewPath(System.String)",
"  [AspectMethodInsertBefore(\"System.IO.File::OpenText(System.String)\",\"\",[0],[False],[None],0,[])] ReviewPath(System.String)",
"  [AspectMethodInsertBefore(\"System.IO.File::OpenWrite(System.String)\",\"\",[0],[False],[None],0,[])] ReviewPath(System.String)",
"  [AspectMethodInsertBefore(\"System.IO.File::ReadAllBytes(System.String)\",\"\",[0],[False],[None],0,[])] ReviewPath(System.String)",
"  [AspectMethodInsertBefore(\"System.IO.File::ReadAllBytesAsync(System.String,System.Threading.CancellationToken)\",\"\",[1],[False],[None],0,[])] ReviewPath(System.String)",
"[AspectClass(\"mscorlib,System.IO.FileSystem,System.Runtime\",[None],2,[PathTraversal])] Datadog.Trace.Iast.Aspects.DirectoryAspect",
"  [AspectMethodInsertBefore(\"System.IO.Directory::CreateDirectory(System.String)\",\"\",[0],[False],[None],0,[])] ReviewPath(System.String)",
"  [AspectMethodInsertBefore(\"System.IO.Directory::CreateDirectory(System.String,System.IO.UnixFileMode)\",\"\",[1],[False],[None],0,[])] ReviewPath(System.String)",
"  [AspectMethodInsertBefore(\"System.IO.DirectoryInfo::EnumerateDirectories(System.String,System.IO.SearchOption)\",\"\",[1],[False],[None],0,[])] ReviewPath(System.String)",
"  [AspectMethodInsertBefore(\"System.IO.DirectoryInfo::EnumerateDirectories(System.String,System.IO.EnumerationOptions)\",\"\",[1],[False],[None],0,[])] ReviewPath(System.String)",
"[AspectClass(\"mscorlib,System.IO.FileSystem,System.Runtime\",[None],2,[PathTraversal])] Datadog.Trace.Iast.Aspects.FileInfoAspect",
"  [AspectMethodInsertBefore(\"System.IO.FileInfo::.ctor(System.String)\",\"\",[0],[False],[None],0,[])] ReviewPath(System.String)",
"  [AspectMethodInsertBefore(\"System.IO.FileInfo::CopyTo(System.String)\",\"\",[0],[False],[None],0,[])] ReviewPath(System.String)",
"  [AspectMethodInsertBefore(\"System.IO.FileInfo::CopyTo(System.String,System.Boolean)\",\"\",[1],[False],[None],0,[])] ReviewPath(System.String)",
"  [AspectMethodInsertBefore(\"System.IO.FileInfo::MoveTo(System.String,System.Boolean)\",\"\",[1],[False],[None],0,[])] ReviewPath(System.String)",
"[AspectClass(\"mscorlib,System.IO.FileSystem,System.Runtime\",[None],2,[PathTraversal])] Datadog.Trace.Iast.Aspects.StreamReaderAspect",
"  [AspectMethodInsertBefore(\"System.IO.StreamReader::.ctor(System.String)\",\"\",[0],[False],[None],0,[])] ReviewPath(System.String)",
"  [AspectMethodInsertBefore(\"System.IO.StreamReader::.ctor(System.String,System.Boolean)\",\"\",[1],[False],[None],0,[])] ReviewPath(System.String)",
"  [AspectMethodInsertBefore(\"System.IO.StreamReader::.ctor(System.String,System.IO.FileStreamOptions)\",\"\",[1],[False],[None],0,[])] ReviewPath(System.String)",
"[AspectClass(\"mscorlib,System.IO.FileSystem,System.Runtime\",[None],10,[PathTraversal])] Datadog.Trace.Iast.Aspects.StreamWriterAspect",
"  [AspectMethodInsertBefore(\"System.IO.StreamWriter::.ctor(System.String,System.Text.Encoding,System.IO.FileStreamOptions)\",\"\",[2],[False],[None],0,[])] ReviewPath(System.String)",
"  [AspectMethodInsertBefore(\"System.IO.StreamWriter::.ctor(System.String,System.Boolean,System.Text.Encoding,System.Int32)\",\"\",[3],[False],[None],0,[])] ReviewPath(System.String)",
"[AspectClass(\"mscorlib,netstandard,System.Runtime\",[None],1,[])] Datadog.Trace.Iast.Aspects.System.Text.StringBuilderAspects",
"  [AspectCtorReplace(\"System.Text.StringBuilder::.ctor(System.String)\",\"\",[0],[False],[StringLiteral_1],0,[])] Init(System.String)",
"  [AspectCtorReplace(\"System.Text.StringBuilder::.ctor(System.String,System.Int32,System.Int32,System.Int32)\",\"\",[0],[False],[StringLiteral_1],0,[])] Init(System.String,System.Int32,System.Int32,System.Int32)",
"  [AspectMethodReplace(\"System.Object::ToString()\",\"System.Text.StringBuilder\",[0],[False],[None],0,[])] ToString(System.Object)",
"  [AspectMethodReplace(\"System.Text.StringBuilder::ToString(System.Int32,System.Int32)\",\"\",[0],[False],[None],0,[])] ToString(System.Text.StringBuilder,System.Int32,System.Int32)",
        };

        var raspAspects = Instrumentation.GetRaspAspects(aspects, out var error);
        error.Should().BeFalse();
        raspAspects.Should().NotBeNull();
        raspAspects.Count().Should().Be(12);
    }
}
