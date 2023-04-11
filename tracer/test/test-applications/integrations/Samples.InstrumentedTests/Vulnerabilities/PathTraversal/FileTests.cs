// <copyright file="DirectoryTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Samples.InstrumentedTests.Iast.Vulnerabilities;

public class FileTests : InstrumentationTestsBase
{

    protected string notTaintedValue = "j:\nott\\inted";
    protected string taintedPathValue = "j:\\p\\ath";

    [TestInitialize]
    public void Init()
    {
        CaptureVulnerabilities(VulnerabilityType.PATH_TRAVERSAL);
        var context = ContextHolder.Current;
        context.TaintedObjects.Add(context, "param2", taintedPathValue, VulnerabilityOriginType.PATH_VARIABLE);
    }

    [Fact]
    public void GivenAFile_WhenWriteAllTextTaintedString_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { File.WriteAllText(taintedPathValue, notTaintedValue); });
        AssertVulnerable();
    }

    [Fact]
    public void GivenAFile_WheWriteAllTextTaintedString_VulnerabilityIsLogged3()
    {
        ExecuteAction(() => { File.WriteAllText(taintedPathValue, notTaintedValue, Encoding.UTF8); });
        AssertVulnerable();
    }
#if !NET35 && !NET461
    [Fact]
    public void GivenAFile_WhenWriteAllTextAsyncTaintedString_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { File.WriteAllTextAsync(taintedPathValue, notTaintedValue, CancellationToken.None); });
        AssertVulnerable();
    }

    [Fact]
    public void GivenAFile_WheWriteAllTextAsyncTaintedString_VulnerabilityIsLogged3()
    {
        ExecuteAction(() => { File.WriteAllTextAsync(taintedPathValue, notTaintedValue, Encoding.UTF8, CancellationToken.None); });
        AssertVulnerable();
    }
#endif
#if !NET35
    [Fact]
    public void GivenAFile_WhenWriteAllLinesTaintedString_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { File.WriteAllLines(taintedPathValue, new List<String>(), Encoding.UTF8); });
        AssertVulnerable();
    }

    [Fact]
    public void GivenAFile_WhenWriteAllLinesTaintedString_VulnerabilityIsLogged2()
    {
        ExecuteAction(() => { File.WriteAllLines(taintedPathValue, new List<String>()); });
        AssertVulnerable();
    }
#if !NET461
    [Fact]
    public void GivenAFile_WhenWriteAllLinesAsyncTaintedString_VulnerabilityIsLogged2()
    {
        ExecuteAction(() => { File.WriteAllLinesAsync(taintedPathValue, new List<String>(), CancellationToken.None); });
        AssertVulnerable();
    }

    [Fact]
    public void GivenAFile_WhenWriteAllLinesAsyncTaintedString_VulnerabilityIsLogged3()
    {
        ExecuteAction(() => { File.WriteAllLinesAsync(taintedPathValue, new List<String>(), Encoding.UTF8, CancellationToken.None); });
        AssertVulnerable();
    }
#endif
#endif
    [Fact]
    public void GivenAFile_WhenWriteAllLinesTaintedString_VulnerabilityIsLogged3()
    {
        ExecuteAction(() => { File.WriteAllLines(taintedPathValue, new String[] { }, Encoding.UTF8); });
        AssertVulnerable();
    }

    [Fact]
    public void GivenAFile_WhenWriteAllLinesTaintedString_VulnerabilityIsLogged4()
    {
        ExecuteAction(() => { File.WriteAllLines(taintedPathValue, new String[] { }); });
        AssertVulnerable();
    }

    [Fact]
    public void GivenAFile_WhenWriteAllBytesTaintedString_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { File.WriteAllBytes(taintedPathValue, new byte[] { 6 }); });
        AssertVulnerable();
    }
#if !NET35 && !NET461
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
    [Fact]
    public void GivenAFile_WhenSetAttributesTaintedString_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { File.SetAttributes(taintedPathValue, FileAttributes.ReadOnly); });
        AssertVulnerable();
    }

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
#if !NET35
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

    [Fact]
    public void GivenAFile_WhenReadLinesTaintedString_VulnerabilityIsLogged2()
    {
        ExecuteAction(() => { File.ReadLines(taintedPathValue, Encoding.UTF8); });
        AssertVulnerable();
    }
#endif

    [TestCategory(testCategory)]
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


    [Fact]
    public void GivenAFile_WhenReadAllTextTaintedString_VulnerabilityIsLogged2()
    {
        ExecuteAction(() => { File.ReadAllText(taintedPathValue, Encoding.UTF8); });
        AssertVulnerable();
    }
#if !NET35 && !NET461
    [Fact]
    public void GivenAFile_WhenReadAllTextAsyncTaintedString_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { File.ReadAllTextAsync(taintedPathValue, CancellationToken.None); });
        AssertVulnerable();
    }

    [Fact]
    public void GivenAFile_WhenReadAllTextAsyncTaintedString_VulnerabilityIsLogged2()
    {
        ExecuteAction(() => { File.ReadAllTextAsync(taintedPathValue, Encoding.UTF8, CancellationToken.None); });
        AssertVulnerable();
    }

    [Fact]
    public void GivenAFile_WhenReadAllTextAsyncTaintedString_VulnerabilityIsLogged3()
    {
        ExecuteAction(() => { File.ReadAllTextAsync(taintedPathValue); });
        AssertVulnerable();
    }

    [Fact]
    public void GivenAFile_WhenReadAllTextAsyncTaintedString_VulnerabilityIsLogged4()
    {
        ExecuteAction(() => { File.ReadAllTextAsync(taintedPathValue, Encoding.UTF8); });
        AssertVulnerable();
    }
#endif
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


    [Fact]
    public void GivenAFile_WhenReadAllLinesTaintedString_VulnerabilityIsLogged2()
    {
        ExecuteAction(() => { File.ReadAllLines(taintedPathValue, Encoding.UTF8); });
        AssertVulnerable();
    }

#if !NET35 && !NET461
    [Fact]
    public void GivenAFile_WhenReadAllLinesTaintedString_VulnerabilityIsLogged3()
    {
        ExecuteAction(() => { File.ReadAllLinesAsync(taintedPathValue, CancellationToken.None); });
        AssertVulnerable();
    }

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

#if !NET461 && !NET35
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
    [Fact]
    public void GivenAFile_WhenOpenWriteTaintedString_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { File.OpenWrite(taintedPathValue); });
        AssertVulnerable();
    }

    [Fact]
    public void GivenAFile_WhenCreateTextTaintedString_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { File.CreateText(taintedPathValue); });
        AssertVulnerable();
    }

    [Fact]
    public void GivenAFile_WhenOpenTextTaintedString_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { File.OpenText(taintedPathValue); });
        AssertVulnerable();
    }

    [Fact]
    public void GivenAFile_WhenOpenReadTaintedString_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { File.OpenRead(taintedPathValue); });
        AssertVulnerable();
    }

    [Fact]
    public void GivenAFile_WhenDeleteTaintedString_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { File.Delete(taintedPathValue); });
        AssertVulnerable();
    }

    [Fact]
    public void GivenAFile_WhenOpenTaintedString_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { File.Open(taintedPathValue, FileMode.Open, FileAccess.Read, FileShare.Read); });
        AssertVulnerable();
    }

    [Fact]
    public void GivenAFile_WhenOpenTaintedString_VulnerabilityIsLogged2()
    {
        ExecuteAction(() => { File.Open(taintedPathValue, FileMode.Open, FileAccess.Read); });
        AssertVulnerable();
    }

    [Fact]
    public void GivenAFile_WhenOpenTaintedString_VulnerabilityIsLogged3()
    {
        ExecuteAction(() => { File.Open(taintedPathValue, FileMode.Open); });
        AssertVulnerable();
    }

#if NET6
        [Fact]
        public void GivenAFile_WhenOpenTaintedString_VulnerabilityIsLogged4()
        {
            ExecuteAction(() => { File.Open(taintedPathValue, new FileStreamOptions()); });
            AssertVulnerable();
        }
#endif

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
#if !NET35 && !NET461 && !NETCORE21
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

#if !NETCORE31 && !NETCORE21 && !NET50 && !NET60
    [Fact]
    public void GivenAFile_WhenCreateTaintedString_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { File.Create(taintedPathValue, 5, FileOptions.SequentialScan, new FileSecurity()); });
        AssertVulnerable();
    }
#endif

    [Fact]
    public void GivenAFile_WhenCreateTaintedString_VulnerabilityIsLogged2()
    {
        ExecuteAction(() => { File.Create(taintedPathValue, 5, FileOptions.SequentialScan); });
        AssertVulnerable();
    }

    [Fact]
    public void GivenAFile_WhenCreateTaintedString_VulnerabilityIsLogged3()
    {
        ExecuteAction(() => { File.Create(taintedPathValue, 5); });
        AssertVulnerable();
    }

    [Fact]
    public void GivenAFile_WhenCreateTaintedString_VulnerabilityIsLogged4()
    {
        ExecuteAction(() => { File.Create(taintedPathValue); });
        AssertVulnerable();
    }

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

    [Fact]
    public void GivenAFile_WhenAppendAllTextTaintedString_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { File.AppendAllText(taintedPathValue, notTaintedValue); });
        AssertVulnerable();
    }

    [Fact]
    public void GivenAFile_WheAppendAllTextTaintedString_VulnerabilityIsLogged3()
    {
        ExecuteAction(() => { File.AppendAllText(taintedPathValue, notTaintedValue, Encoding.UTF8); });
        AssertVulnerable();
    }
#if !NET35
    [Fact]
    public void GivenAFile_WhenAppendAllLinesTaintedString_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { File.AppendAllLines(taintedPathValue, new List<String>(), Encoding.UTF8); });
        AssertVulnerable();
    }

    [Fact]
    public void GivenAFile_WhenAppendAllLinesTaintedString_VulnerabilityIsLogged2()
    {
        ExecuteAction(() => { File.AppendAllLines(taintedPathValue, new List<String>()); });
        AssertVulnerable();
    }

    [Fact]
    public void GivenAFile_WhenAppendAllLinesTaintedString_VulnerabilityIsLogged3()
    {
        ExecuteAction(() => { File.AppendAllLines(taintedPathValue, new String[] { }, Encoding.UTF8); });
        AssertVulnerable();
    }

    [Fact]
    public void GivenAFile_WhenAppendAllLinesTaintedString_VulnerabilityIsLogged4()
    {
        ExecuteAction(() => { File.AppendAllLines(taintedPathValue, new String[] { }); });
        AssertVulnerable();
    }
#endif

    [Fact]
    public void GivenAFile_WhenAppendTextTaintedString_VulnerabilityIsLogged4()
    {
        ExecuteAction(() => { File.AppendText(taintedPathValue); });
        AssertVulnerable();
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    [TestCategory(testCategory)]
    public void GivenAFile_WhenCreatingFromIncorrectString_ExceptionIsThrown()
    {
        File.WriteAllText(null, null);
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
#if !NETCORE31 && !NETCORE21 && !NET50 && !NET60
        catch (ArgumentException)
        {
            //We dont have a valid file. It is normal
        }
#else
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
#endif
    }
}

}
