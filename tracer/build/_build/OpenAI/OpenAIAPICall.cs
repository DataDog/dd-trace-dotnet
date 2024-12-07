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
    private static readonly HttpClient httpClient = new HttpClient() { Timeout = TimeSpan.FromSeconds(30) };
    //That is the current maximum number of tokens that can be requested in a single call.
    private static readonly int maxTokens = 16383;

    private static int GetApproxTokenCount(string prompt)
    {
        // This is a rough estimate of the number of tokens in the prompt.
        // More info in https://help.openai.com/en/articles/4936856-what-are-tokens-and-how-to-count-them
        return prompt.Length / 4;
    }

    // We try the full promt first, if it fails we try a smaller prompt
    public static string TryGetReponse(ref string prompt, string key)
    {
        // We can do a first request without counting tokens
        var result = GetResponse(prompt, key).Result;

        // If result is null, we probably are facing a situation where we have too many tokens in the prompt
        // In that case, we just truncate the request instead of doing multiple queries (which would increase the cost)
        if (string.IsNullOrEmpty(result) && GetApproxTokenCount(prompt) > maxTokens)
        {
            Console.WriteLine("Warning: Prompt too long, trying a smaller prompt");
            prompt = prompt.Substring(0, (int)(maxTokens * 0.95));
            result = GetResponse(prompt, key).Result;
        }

        return result;
    }

    public static async Task<string> GetResponse(string prompt, string key)
    {
        var url = "https://api.openai.com/v1/chat/completions";

        var requestContent = new
        {
            model = "gpt-4o",
            messages = new[] 
            {
                new { role = "system", content = "You are a helpful assistant." },
                new { role = "user", content = prompt }
            },
            max_tokens = maxTokens
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
                return ParseResponseText(await response.Content.ReadAsStringAsync());
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
