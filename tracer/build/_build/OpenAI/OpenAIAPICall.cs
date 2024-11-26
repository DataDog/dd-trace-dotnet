using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace OpenAI;

public class OpenAiApiCall
{
    private static readonly HttpClient httpClient = new HttpClient();

    public static async Task<string> GetResponseAsync(string prompt, string key)
    {
        var url = "https://api.openai.com/v1/chat/completions";
        int maxTokensForResponse = 10000;

        var requestContent = new
        {
            model = "gpt-4o",
            messages = new[] 
            {
                new { role = "system", content = "You are a helpful assistant." },
                new { role = "user", content = prompt }
            },
            max_tokens = maxTokensForResponse
        };

        var jsonContent = JsonSerializer.Serialize(requestContent);
        var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

        using (var request = new HttpRequestMessage(HttpMethod.Post, url))
        {
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", key);
            request.Content = httpContent;

            try
            {
                var response = await httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();

                var responseString = await response.Content.ReadAsStringAsync();
                return ParseResponseText(responseString);
            }
            catch (HttpRequestException e)
            {
                Console.WriteLine($"Request error: {e.Message}");
                return string.Empty;
            }
        }
    }

    private static string ParseResponseText(string response)
    {
        try
        {
            using var jsonDoc = JsonDocument.Parse(response);
            var root = jsonDoc.RootElement;
            var text = root.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
            return text;
        }
        catch (JsonException e)
        {
            Console.WriteLine($"JSON parsing error: {e.Message}");
            return string.Empty;
        }
    }
}
