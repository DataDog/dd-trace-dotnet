using Xunit;
using RestSharp;
using System.Threading;

namespace Samples.InstrumentedTests.Iast.Vulnerabilities.SSRF;

public class RestClientTests : SSRFTests
{
    [Fact]
    public void GivenARestSharpClient_WhenExecute_Vulnerable()
    {
        var client = new RestClient(taintedUrlValue);
        var request = new RestRequest("", Method.Get);
        ExecuteFunc(() => client.Execute(request));
        AssertVulnerable("SSRF", sourceType: sourceType);
    }

    [Fact]
    public void GivenARestSharpClient_WhenExecute_Vulnerable2()
    {
        var client = new RestClient(taintedUrlValue);
        var request = new RestRequest("", Method.Get);
        ExecuteFunc(() => client.ExecuteAsync(request, CancellationToken.None));
        AssertVulnerable("SSRF", sourceType: sourceType);
    }

    [Fact]
    public void GivenARestSharpClient_WhenExecute_Vulnerable3()
    {
        var client = new RestClient(taintedUrlValue);
        var request = new RestRequest("", Method.Get);
        ExecuteFunc(() => client.Delete(request));
        AssertVulnerable("SSRF", sourceType: sourceType);
    }

    [Fact]
    public void GivenARestSharpClient_WhenExecute_Vulnerable4()
    {
        var client = new RestClient(taintedUrlValue);
        var request = new RestRequest("", Method.Get);
        ExecuteFunc(() => client.Post(request));
        AssertVulnerable("SSRF", sourceType: sourceType);
    }

    [Fact]
    public void GivenARestSharpClient_WhenExecute_Vulnerable5()
    {
        var client = new RestClient(taintedUrlValue);
        var request = new RestRequest("", Method.Get);
        ExecuteFunc(() => client.Get(request));
        AssertVulnerable("SSRF", sourceType: sourceType);
    }
}
