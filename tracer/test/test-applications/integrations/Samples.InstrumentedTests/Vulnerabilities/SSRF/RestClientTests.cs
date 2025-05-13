using Xunit;
using RestSharp;
using System.Threading;
using System.Security.Policy;

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

    [Fact]
    public void GivenATaintedQueryStringParameterEscaped_WhenExecute_NotVulnerable()
    {
        var client = new RestClient("https://www.google.com");
        var request = new RestRequest("/search");
        request.AddParameter("q1", taintedSafeParam, ParameterType.QueryString);
        request.AddParameter("q2", taintedQuery, ParameterType.QueryString);
        var fullUrl = client.BuildUri(request);
        ExecuteFunc(() => client.ExecuteGet(request).ToString());
        AssertNotVulnerable();
    }

    [Fact]
    public void TaintedUrlSegmentParameterEscaped_WhenExecute_NotVulnerable()
    {
        var client = new RestClient("https://www.google.com");
        var request = new RestRequest("/search{q1}{q2}");
        request.AddParameter("q1", taintedSafeParam, ParameterType.UrlSegment);
        request.AddParameter("q2", taintedQuery, ParameterType.UrlSegment);
        var fullUrl = client.BuildUri(request);
        ExecuteFunc(() => client.ExecuteGet(request).ToString());
        AssertNotVulnerable();
    }

    [Fact]
    public void TaintedQueryStringParameterNotEscaped_WhenExecute_Vulnerable()
    {
        var client = new RestClient("https://www.google.com");
        var request = new RestRequest("/search");
        request.AddParameter("q1", taintedSafeParam, ParameterType.QueryString, false);
        request.AddParameter("q2", taintedQuery, ParameterType.QueryString, false);
        var fullUrl = client.BuildUri(request);
        ExecuteFunc(() => client.ExecuteGet(request).ToString());
        AssertVulnerable();
    }

    [Fact]
    public void TaintedUrlSegmentParameterNotEscaped_WhenExecute_Vulnerable()
    {
        var client = new RestClient("https://www.google.com");
        var request = new RestRequest("/search{q1}{q2}");
        request.AddParameter("q1", taintedSafeParam, ParameterType.UrlSegment, false);
        request.AddParameter("q2", taintedQuery, ParameterType.UrlSegment, false);
        var fullUrl = client.BuildUri(request);
        ExecuteFunc(() => client.ExecuteGet(request).ToString());
        AssertVulnerable();
    }
}
