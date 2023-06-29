// <copyright file="FileTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
#if NETFRAMEWORK
using System.Security.AccessControl;
#else
using System.Threading;
#endif
using System.Text;
using Xunit;

namespace Samples.InstrumentedTests.Iast.Vulnerabilities;

public class FileTests : InstrumentationTestsBase
{

    protected string notTaintedValue = "j:\nott\\inted";
    protected string taintedPathValue = "j:\\p\\ath";

    public FileTests()
    {
        AddTainted(taintedPathValue);
    }

    // Cover System.IO.File::WriteAllText(System.String,System.String)

    [Fact]
    public void GivenAFile_WhenWriteAllTextTaintedString_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { File.WriteAllText(taintedPathValue, notTaintedValue); });
        AssertVulnerable();
    }

    [Fact]
    public void GivenAFile_WhenCreatingFromIncorrectString_ExceptionIsThrown()
    {
        Assert.Throws<ArgumentNullException>(() => File.WriteAllText(null, null));
    }

    // Cover System.IO.File::WriteAllText(System.String,System.String,System.Text.Encoding)

    [Fact]
    public void GivenAFile_WheWriteAllTextTaintedString_VulnerabilityIsLogged3()
    {
        ExecuteAction(() => { File.WriteAllText(taintedPathValue, notTaintedValue, Encoding.UTF8); });
        AssertVulnerable();
    }

    // Cover System.IO.File::WriteAllTextAsync(System.String,System.String,System.Threading.CancellationToken)

#if !NETFRAMEWORK
    [Fact]
    public void GivenAFile_WhenWriteAllTextAsyncTaintedString_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { File.WriteAllTextAsync(taintedPathValue, notTaintedValue, CancellationToken.None); });
        AssertVulnerable();
    }

    // Cover System.IO.File::WriteAllTextAsync(System.String,System.String,System.Text.Encoding,System.Threading.CancellationToken)

    [Fact]
    public void GivenAFile_WheWriteAllTextAsyncTaintedString_VulnerabilityIsLogged3()
    {
        ExecuteAction(() => { File.WriteAllTextAsync(taintedPathValue, notTaintedValue, Encoding.UTF8, CancellationToken.None); });
        AssertVulnerable();
    }
#endif

    [Fact]
    public void GivenAFile_WhenWriteAllLinesTaintedString_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { File.WriteAllLines(taintedPathValue, new List<String>(), Encoding.UTF8); });
        AssertVulnerable();
    }

    // Cover System.IO.File::WriteAllLines(System.String, System.String[])

    [Fact]
    public void GivenAFile_WhenWriteAllLinesTaintedString_VulnerabilityIsLogged4()
    {
        ExecuteAction(() => { File.WriteAllLines(taintedPathValue, new String[] { }); });
        AssertVulnerable();
    }

    // Cover System.IO.File::WriteAllLines(System.String,System.Collections.Generic.IEnumerable`1<System.String>)

    [Fact]
    public void GivenAFile_WhenWriteAllLinesTaintedString_VulnerabilityIsLogged2()
    {
        ExecuteAction(() => { File.WriteAllLines(taintedPathValue, new List<String>()); });
        AssertVulnerable();
    }

    // Cover System.IO.File::WriteAllLines(System.String,System.Collections.Generic.IEnumerable`1<System.String>,System.Text.Encoding)
    [Fact]
    public void GivenAFile_WhenWriteAllLinesTaintedString_VulnerabilityIsLogged5()
    {
        ExecuteAction(() => { File.WriteAllLines(taintedPathValue, new List<String>(), Encoding.UTF8); });
        AssertVulnerable();
    }

#if !NETFRAMEWORK

    // Cover System.IO.File::WriteAllLinesAsync(System.String,System.Collections.Generic.IEnumerable`1<System.String>,System.Threading.CancellationToken)
    [Fact]
    public void GivenAFile_WhenWriteAllLinesAsyncTaintedString_VulnerabilityIsLogged2()
    {
        ExecuteAction(() => { File.WriteAllLinesAsync(taintedPathValue, new List<String>(), CancellationToken.None); });
        AssertVulnerable();
    }

    // Cover System.IO.File::WriteAllLinesAsync(System.String,System.Collections.Generic.IEnumerable`1<System.String>,System.Text.Encoding,System.Threading.CancellationToken)

    [Fact]
    public void GivenAFile_WhenWriteAllLinesAsyncTaintedString_VulnerabilityIsLogged3()
    {
        ExecuteAction(() => { File.WriteAllLinesAsync(taintedPathValue, new List<String>(), Encoding.UTF8, CancellationToken.None); });
        AssertVulnerable();
    }
#endif

    // Cover System.IO.File::WriteAllLines(System.String,System.String[],System.Text.Encoding)

    [Fact]
    public void GivenAFile_WhenWriteAllLinesTaintedString_VulnerabilityIsLogged3()
    {
        ExecuteAction(() => { File.WriteAllLines(taintedPathValue, new String[] { }, Encoding.UTF8); });
        AssertVulnerable();
    }

    // System.IO.File::WriteAllBytes(System.String,System.Byte[])

    [Fact]
    public void GivenAFile_WhenWriteAllBytesTaintedString_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { File.WriteAllBytes(taintedPathValue, new byte[] { 6 }); });
        AssertVulnerable();
    }

#if !NETFRAMEWORK

    // Cover System.IO.File::WriteAllBytesAsync(System.String, System.Byte[], System.Threading.CancellationToken)

    [Fact]
    public void GivenAFile_WhenWriteAllBytesAsyncTaintedString_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { File.WriteAllBytesAsync(taintedPathValue, new byte[] { 6 }, CancellationToken.None); });
        AssertVulnerable();
    }

    [Fact]
    public void GivenAFile_WhenWriteAllBytesAsyncTaintedString_VulnerabilityIsLogged2()
    {
        ExecuteAction(() => { File.WriteAllBytesAsync(taintedPathValue, new byte[] { 6 }); });
        AssertVulnerable();
    }
#endif

    // Cover System.IO.File::SetAttributes(System.String,System.IO.FileAttributes)

    [Fact]
    public void GivenAFile_WhenSetAttributesTaintedString_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { File.SetAttributes(taintedPathValue, FileAttributes.ReadOnly); });
        AssertVulnerable();
    }

    // Cover System.IO.File::Replace(System.String,System.String,System.String)

    [Fact]
    public void GivenAFile_WhenReplaceTaintedString_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { File.Replace(taintedPathValue, notTaintedValue, notTaintedValue); });
        AssertVulnerable();
    }

    [Fact]
    public void GivenAFile_WhenReplaceTaintedString_VulnerabilityIsLogged5()
    {
        ExecuteAction(() => { File.Replace(notTaintedValue, notTaintedValue, taintedPathValue); });
        AssertVulnerable();
    }

    [Fact]
    public void GivenAFile_WhenReplaceTaintedString_VulnerabilityIsLogged2()
    {
        ExecuteAction(() => { File.Replace(notTaintedValue, taintedPathValue, notTaintedValue); });
        AssertVulnerable();
    }

    // Cover System.IO.File::Replace(System.String,System.String,System.String,System.Boolean)

    [Fact]
    public void GivenAFile_WhenReplaceTaintedString_VulnerabilityIsLogged3()
    {
        ExecuteAction(() => { File.Replace(taintedPathValue, notTaintedValue, notTaintedValue, true); });
        AssertVulnerable();
    }

    [Fact]
    public void GivenAFile_WhenReplaceTaintedString_VulnerabilityIsLogged4()
    {
        ExecuteAction(() => { File.Replace(notTaintedValue, notTaintedValue, taintedPathValue, true); });
        AssertVulnerable();
    }

    [Fact]
    public void GivenAFile_WhenReplaceTaintedString_VulnerabilityIsLogged6()
    {
        ExecuteAction(() => { File.Replace(notTaintedValue, taintedPathValue, notTaintedValue, true); });
        AssertVulnerable();
    }

    // Cover System.IO.File::ReadLines(System.String)

    [Fact]
    public void GivenAFile_WhenReadLinesTaintedString_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { File.ReadLines(taintedPathValue); });
        AssertVulnerable();
    }

    [Fact]
    public void GivenAFile_WhenReadLinesNotTaintedString_VulnerabilityIsNotLogged()
    {
        ExecuteAction(() => { File.ReadLines(notTaintedValue); });
        AssertNotVulnerable();
    }

    // System.IO.File::ReadLines(System.String,System.Text.Encoding)

    [Fact]
    public void GivenAFile_WhenReadLinesTaintedString_VulnerabilityIsLogged2()
    {
        ExecuteAction(() => { File.ReadLines(taintedPathValue, Encoding.UTF8); });
        AssertVulnerable();
    }

#if NET7_0_OR_GREATER

    // Cover System.IO.File::ReadLinesAsync(System.String, System.Threading.CancellationToken)

    [Fact]
    public void GivenAFile_WhenReadLinesAsyncTaintedString_VulnerabilityIsLogged2()
    {
        ExecuteAction(() => { File.ReadLinesAsync(taintedPathValue, CancellationToken.None); });
        AssertVulnerable();
    }

    // Cover System.IO.File::ReadLinesAsync(System.String, System.Text.Encoding, System.Threading.CancellationToken)

    [Fact]
    public void GivenAFile_WhenReadLinesAsyncTaintedString_VulnerabilityIsLogged3()
    {
        ExecuteAction(() => { File.ReadLinesAsync(taintedPathValue, Encoding.UTF8, CancellationToken.None); });
        AssertVulnerable();
    }
#endif

    // Cover System.IO.File::ReadAllText(System.String)

    [Fact]
    public void GivenAFile_WhenReadAllTextTaintedString_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { File.ReadAllText(taintedPathValue); });
        AssertVulnerable();
    }

    [Fact]
    public void GivenAFile_WhenReadAllTextNotTaintedString_VulnerabilityIsNotLogged()
    {
        ExecuteAction(() => { File.ReadAllText(notTaintedValue); });
        AssertNotVulnerable();
    }

    // Cover System.IO.File::ReadAllText(System.String,System.Text.Encoding)

    [Fact]
    public void GivenAFile_WhenReadAllTextTaintedString_VulnerabilityIsLogged2()
    {
        ExecuteAction(() => { File.ReadAllText(taintedPathValue, Encoding.UTF8); });
        AssertVulnerable();
    }
#if !NETFRAMEWORK

    // Cover "System.IO.File::ReadAllTextAsync(System.String,System.Threading.CancellationToken)"

    [Fact]
    public void GivenAFile_WhenReadAllTextAsyncTaintedString_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { File.ReadAllTextAsync(taintedPathValue, CancellationToken.None); });
        AssertVulnerable();
    }

    [Fact]
    public void GivenAFile_WhenReadAllTextAsyncTaintedString_VulnerabilityIsLogged3()
    {
        ExecuteAction(() => { File.ReadAllTextAsync(taintedPathValue); });
        AssertVulnerable();
    }

    // Cover "System.IO.File::ReadAllTextAsync(System.String,System.Text.Encoding,System.Threading.CancellationToken)"

    [Fact]
    public void GivenAFile_WhenReadAllTextAsyncTaintedString_VulnerabilityIsLogged2()
    {
        ExecuteAction(() => { File.ReadAllTextAsync(taintedPathValue, Encoding.UTF8, CancellationToken.None); });
        AssertVulnerable();
    }

    [Fact]
    public void GivenAFile_WhenReadAllTextAsyncTaintedString_VulnerabilityIsLogged4()
    {
        ExecuteAction(() => { File.ReadAllTextAsync(taintedPathValue, Encoding.UTF8); });
        AssertVulnerable();
    }
#endif

    // Cover AspectMethodInsertBefore("System.IO.File::ReadAllLines(System.String)

    [Fact]
    public void GivenAFile_WhenReadAllLinesTaintedString_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { File.ReadAllLines(taintedPathValue); });
        AssertVulnerable();
    }

    [Fact]
    public void GivenAFile_WhenReadAllLinesNotTaintedString_VulnerabilityIsNotLogged()
    {
        ExecuteAction(() => { File.ReadAllLines(notTaintedValue); });
        AssertNotVulnerable();
    }

    // Cover "System.IO.File::ReadAllLines(System.String,System.Text.Encoding)

    [Fact]
    public void GivenAFile_WhenReadAllLinesTaintedString_VulnerabilityIsLogged2()
    {
        ExecuteAction(() => { File.ReadAllLines(taintedPathValue, Encoding.UTF8); });
        AssertVulnerable();
    }

#if !NETFRAMEWORK

    // Cover System.IO.Filein(System.String,System.Threading.CancellationToken)

    [Fact]
    public void GivenAFile_WhenReadAllLinesTaintedString_VulnerabilityIsLogged3()
    {
        ExecuteAction(() => { File.ReadAllLinesAsync(taintedPathValue, CancellationToken.None); });
        AssertVulnerable();
    }

    // Cover System.IO.File::ReadAllLinesAsync(System.String,System.Text.Encoding,System.Threading.CancellationToken)

    [Fact]
    public void GivenAFile_WhenReadAllLinesTaintedString_VulnerabilityIsLogged4()
    {
        ExecuteAction(() => { File.ReadAllLinesAsync(taintedPathValue, Encoding.UTF8); });
        AssertVulnerable();
    }

    [Fact]
    public void GivenAFile_WhenReadAllLinesTaintedString_VulnerabilityIsLogged5()
    {
        ExecuteAction(() => { File.ReadAllLinesAsync(taintedPathValue, Encoding.UTF8, CancellationToken.None); });
        AssertVulnerable();
    }
#endif

    // Cover System.IO.File::ReadAllBytes(System.String)

    [Fact]
    public void GivenAFile_WhenReadAllBytesTaintedString_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { File.ReadAllBytes(taintedPathValue); });
        AssertVulnerable();
    }

    [Fact]
    public void GivenAFile_WhenReadAllBytesNotTaintedString_VulnerabilityIsNotLogged()
    {
        ExecuteAction(() => { File.ReadAllBytes(notTaintedValue); });
        AssertNotVulnerable();
    }

#if !NETFRAMEWORK

    // Cover System.IO.File::ReadAllBytesAsync(System.String,System.Threading.CancellationToken)

    [Fact]
    public void GivenAFile_WhenReadAllBytesAsyncTaintedString_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { File.ReadAllBytesAsync(taintedPathValue); });
        AssertVulnerable();
    }

    [Fact]
    public void GivenAFile_WhenReadAllBytesAsyncTaintedString_VulnerabilityIsLogged2()
    {
        ExecuteAction(() => { File.ReadAllBytesAsync(taintedPathValue, CancellationToken.None); });
        AssertVulnerable();
    }

    [Fact]
    public void GivenAFile_WhenReadAllBytesAsyncNotTaintedString_VulnerabilityIsNotLogged()
    {
        ExecuteAction(() => { File.ReadAllBytesAsync(notTaintedValue, CancellationToken.None); });
        AssertNotVulnerable();
    }
#endif

    // Cover System.IO.File::OpenWrite(System.String)

    [Fact]
    public void GivenAFile_WhenOpenWriteTaintedString_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { File.OpenWrite(taintedPathValue); });
        AssertVulnerable();
    }

    // Cover System.IO.File::CreateText(System.String)

    [Fact]
    public void GivenAFile_WhenCreateTextTaintedString_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { File.CreateText(taintedPathValue); });
        AssertVulnerable();
    }

    // Cover System.IO.File::OpenText(System.String)

    [Fact]
    public void GivenAFile_WhenOpenTextTaintedString_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { File.OpenText(taintedPathValue); });
        AssertVulnerable();
    }

    // Cover System.IO.File::OpenRead(System.String)

    [Fact]
    public void GivenAFile_WhenOpenReadTaintedString_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { File.OpenRead(taintedPathValue); });
        AssertVulnerable();
    }

    // Cover System.IO.File::Delete(System.String)

    [Fact]
    public void GivenAFile_WhenDeleteTaintedString_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { File.Delete(taintedPathValue); });
        AssertVulnerable();
    }

    // Cover System.IO.File::Open(System.String,System.IO.FileMode,System.IO.FileAccess,System.IO.FileShare)

    [Fact]
    public void GivenAFile_WhenOpenTaintedString_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { File.Open(taintedPathValue, FileMode.Open, FileAccess.Read, FileShare.Read); });
        AssertVulnerable();
    }

    // Cover System.IO.File::Open(System.String,System.IO.FileMode,System.IO.FileAccess)

    [Fact]
    public void GivenAFile_WhenOpenTaintedString_VulnerabilityIsLogged2()
    {
        ExecuteAction(() => { File.Open(taintedPathValue, FileMode.Open, FileAccess.Read); });
        AssertVulnerable();
    }

    // Cover System.IO.File::Open(System.String,System.IO.FileMode)

    [Fact]
    public void GivenAFile_WhenOpenTaintedString_VulnerabilityIsLogged3()
    {
        ExecuteAction(() => { File.Open(taintedPathValue, FileMode.Open); });
        AssertVulnerable();
    }

#if NET6_0_OR_GREATER

    // Cover System.IO.File::Open(System.String,System.IO.FileStreamOptions)

    [Fact]
    public void GivenAFile_WhenOpenTaintedString_VulnerabilityIsLogged4()
    {
        ExecuteAction(() => { File.Open(taintedPathValue, new FileStreamOptions()); });
        AssertVulnerable();
    }

    // Cover System.IO.File::OpenHandle(System.String,System.IO.FileMode,System.IO.FileAccess,System.IO.FileShare,System.IO.FileOptions,System.Int64)

    [Fact]
    public void GivenAFile_WhenOpenHandleTaintedString_VulnerabilityIsLogged4()
    {
        ExecuteAction(() => { File.OpenHandle(taintedPathValue, FileMode.Append, FileAccess.ReadWrite, FileShare.None, FileOptions.None, 0); });
        AssertVulnerable();
    }
#endif

    // Cover System.IO.File::Move(System.String,System.String)

    [Fact]
    public void GivenAFile_WhenMoveTaintedString_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { File.Move(taintedPathValue, notTaintedValue); });
        AssertVulnerable();
    }

    [Fact]
    public void GivenAFile_WhenMoveTaintedString_VulnerabilityIsLogged2()
    {
        ExecuteAction(() => { File.Move(notTaintedValue, taintedPathValue); });
        AssertVulnerable();
    }
#if NETCOREAPP3_0_OR_GREATER

    // Cover System.IO.File::Move(System.String,System.String,System.Boolean)

    [Fact]
    public void GivenAFile_WhenMoveTaintedString_VulnerabilityIsLogged3()
    {
        ExecuteAction(() => { File.Move(taintedPathValue, notTaintedValue, false); });
        AssertVulnerable();
    }

    [Fact]
    public void GivenAFile_WhenMoveTaintedString_VulnerabilityIsLogged4()
    {
        ExecuteAction(() => { File.Move(notTaintedValue, taintedPathValue, false); });
        AssertVulnerable();
    }
#endif

#if NETFRAMEWORK
    // Cover System.IO.File::Create(System.String,System.Int32,System.IO.FileOptions,System.Security.AccessControl.FileSecurity)

    [Fact]
    public void GivenAFile_WhenCreateTaintedString_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { File.Create(taintedPathValue, 5, FileOptions.SequentialScan, new FileSecurity()); });
        AssertVulnerable();
    }
#endif

    // Cover System.IO.File::Create(System.String,System.Int32,System.IO.FileOptions)

    [Fact]
    public void GivenAFile_WhenCreateTaintedString_VulnerabilityIsLogged2()
    {
        ExecuteAction(() => { File.Create(taintedPathValue, 5, FileOptions.SequentialScan); });
        AssertVulnerable();
    }

    // Cover System.IO.File::Create(System.String,System.Int32)

    [Fact]
    public void GivenAFile_WhenCreateTaintedString_VulnerabilityIsLogged3()
    {
        ExecuteAction(() => { File.Create(taintedPathValue, 5); });
        AssertVulnerable();
    }

    // Cover System.IO.File::Create(System.String)

    [Fact]
    public void GivenAFile_WhenCreateTaintedString_VulnerabilityIsLogged4()
    {
        ExecuteAction(() => { File.Create(taintedPathValue); });
        AssertVulnerable();
    }

    // Cover System.IO.File::Copy(System.String,System.String)

    [Fact]
    public void GivenAFile_WhenCopyTaintedString_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { File.Copy(taintedPathValue, notTaintedValue); });
        AssertVulnerable();
    }

    [Fact]
    public void GivenAFile_WhenCopyTaintedString_VulnerabilityIsLogged1()
    {
        ExecuteAction(() => { File.Copy(notTaintedValue, taintedPathValue); });
        AssertVulnerable();
    }

    // Cover System.IO.File::Copy(System.String,System.String,System.Boolean)

    [Fact]
    public void GivenAFile_WhenCopyTaintedString_VulnerabilityIsLogged2()
    {
        ExecuteAction(() => { File.Copy(taintedPathValue, notTaintedValue, true); });
        AssertVulnerable();
    }

    [Fact]
    public void GivenAFile_WhenCopyTaintedString_VulnerabilityIsLogged3()
    {
        ExecuteAction(() => { File.Copy(notTaintedValue, taintedPathValue, true); });
        AssertVulnerable();
    }

    // Cover System.IO.File::AppendAllText(System.String,System.String)

    [Fact]
    public void GivenAFile_WhenAppendAllTextTaintedString_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { File.AppendAllText(taintedPathValue, notTaintedValue); });
        AssertVulnerable();
    }

    // Cover System.IO.File::AppendAllText(System.String,System.String,System.Text.Encoding)

    [Fact]
    public void GivenAFile_WheAppendAllTextTaintedString_VulnerabilityIsLogged3()
    {
        ExecuteAction(() => { File.AppendAllText(taintedPathValue, notTaintedValue, Encoding.UTF8); });
        AssertVulnerable();
    }
#if !NETFRAMEWORK

    // Cover System.IO.File::AppendAllTextAsync(System.String, System.String, System.Threading.CancellationToken)

    [Fact]
    public void GivenAFile_WheAppendAllTextAsyncTaintedString_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { File.AppendAllTextAsync(taintedPathValue, notTaintedValue, CancellationToken.None); });
        AssertVulnerable();
    }

    // Cover System.IO.File::AppendAllTextAsync(System.String, System.String, System.Text.Encoding, System.Threading.CancellationToken)

    [Fact]
    public void GivenAFile_WheAppendAllTextAsyncTaintedString_VulnerabilityIsLogged2()
    {
        ExecuteAction(() => { File.AppendAllTextAsync(taintedPathValue, notTaintedValue, Encoding.UTF8, CancellationToken.None); });
        AssertVulnerable();
    }

    // Cover System.IO.File::AppendAllLinesAsync(System.String, System.Collections.Generic.IEnumerable`1[System.String], System.Threading.CancellationToken)

    [Fact]
    public void GivenAFile_WheAppendAllLinesAsyncAsyncTaintedString_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { File.AppendAllLinesAsync(taintedPathValue, new List<string> { notTaintedValue }, CancellationToken.None); });
        AssertVulnerable();
    }

    // Cover System.IO.File::AppendAllLinesAsync(System.String, System.Collections.Generic.IEnumerable`1[System.String], System.Text.Encoding, System.Threading.CancellationToken)

    [Fact]
    public void GivenAFile_WheAppendAllLinesAsyncAsyncTaintedString_VulnerabilityIsLogged2()
    {
        ExecuteAction(() => { File.AppendAllLinesAsync(taintedPathValue, new List<string> { notTaintedValue }, Encoding.UTF8, CancellationToken.None); });
        AssertVulnerable();
    }
#endif

    // Cover System.IO.File::AppendAllLines(System.String,System.Collections.Generic.IEnumerable`1<System.String>,System.Text.Encoding)

    [Fact]
    public void GivenAFile_WhenAppendAllLinesTaintedString_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { File.AppendAllLines(taintedPathValue, new List<String>(), Encoding.UTF8); });
        AssertVulnerable();
    }

    [Fact]
    public void GivenAFile_WhenAppendAllLinesTaintedString_VulnerabilityIsLogged3()
    {
        ExecuteAction(() => { File.AppendAllLines(taintedPathValue, new String[] { }, Encoding.UTF8); });
        AssertVulnerable();
    }

    // Cover System.IO.File::AppendAllLines(System.String,System.Collections.Generic.IEnumerable`1<System.String>)

    [Fact]
    public void GivenAFile_WhenAppendAllLinesTaintedString_VulnerabilityIsLogged2()
    {
        ExecuteAction(() => { File.AppendAllLines(taintedPathValue, new List<String>()); });
        AssertVulnerable();
    }

    [Fact]
    public void GivenAFile_WhenAppendAllLinesTaintedString_VulnerabilityIsLogged4()
    {
        ExecuteAction(() => { File.AppendAllLines(taintedPathValue, new String[] { }); });
        AssertVulnerable();
    }

    // Cover System.IO.File::AppendText(System.String)

    [Fact]
    public void GivenAFile_WhenAppendTextTaintedString_VulnerabilityIsLogged4()
    {
        ExecuteAction(() => { File.AppendText(taintedPathValue); });
        AssertVulnerable();
    }

    void ExecuteAction(Action c)
    {
        try
        {
            c.Invoke();
        }
        catch (DirectoryNotFoundException)
        {
            //We dont have a valid file. It is normal
        }
        catch (ArgumentException)
        {
            //We dont have a valid file. It is normal
        }
        catch (FileNotFoundException)
        {
            //We dont have a valid file. It is normal
        }
        catch (IOException)
        {
            //We dont have a valid file. It is normal
        }
        catch (UnauthorizedAccessException)
        {
            //For Linux files. It is normal
        }
    }
}
