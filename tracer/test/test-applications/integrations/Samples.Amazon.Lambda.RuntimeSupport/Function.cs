using System.Net;
using System.Text.Json.Serialization;
using Amazon.Lambda.Core;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;
using Samples;

void MakeRequest(string url)
{
    Console.WriteLine("Calling url " + url);
    var request = WebRequest.Create(url);
    request.Credentials = CredentialCache.DefaultCredentials;
    var response = (HttpWebResponse)request.GetResponse();
    using (var dataStream = response.GetResponseStream())
    {
        var reader = new StreamReader(dataStream);
        reader.ReadToEnd();
    }

    response.Close();
}

// The function handler that will be called for each Lambda event
var handler = (CustomInput input, ILambdaContext context) =>
{
    using var scope = SampleHelpers.CreateScope("manual.ToplevelStatements");
    var host = Environment.GetEnvironmentVariable("DUMMY_API_HOST")!;
    var url = $"{host}/function/ToplevelStatements";

    MakeRequest(url);
    return new { statusCode = 200, body = "ok!" };
};


// Build the Lambda runtime client passing in the handler to call for each
// event and the JSON serializer to use for translating Lambda JSON documents
// to .NET types.
await LambdaBootstrapBuilder.Create(handler, new DefaultLambdaJsonSerializer())
        .Build()
        .RunAsync();

public class CustomInput
{
    public string Field1 { get; set; }
    public int Field2 { get; set; }
}
