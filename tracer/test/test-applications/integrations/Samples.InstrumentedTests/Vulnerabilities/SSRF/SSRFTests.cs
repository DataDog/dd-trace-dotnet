using System;

namespace Samples.InstrumentedTests.Iast.Vulnerabilities.SSRF;

public class SSRFTests : InstrumentationTestsBase
{
    protected string notTaintedValue = "http://127.0.0.1/nottainted";
    protected string notTaintedHost = "myhost";
    protected string taintedHost = "localhost";
    protected string taintedQuery = "e=22";
    protected string taintedUrlValue = "http://127.0.0.1/invalid?q=1#e";
    protected string taintedUrlValue2 = "http://127.0.0.1";
    protected string file = "invalid@#file";
    protected byte sourceType = 5;

    public SSRFTests()
    {
        AddTainted(taintedUrlValue, sourceType);
        AddTainted(taintedUrlValue2, sourceType);
        AddTainted(taintedHost, sourceType);
        AddTainted(taintedQuery, sourceType);
    }

    protected void AssertVulnerableSSRF(string evidence = null)
    {
        AssertVulnerable("SSRF", ":+-" + (evidence ?? taintedUrlValue) + "-+:", true, sourceType);
    }
}
