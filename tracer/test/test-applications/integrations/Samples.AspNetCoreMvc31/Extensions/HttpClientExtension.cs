using Newtonsoft.Json;
using System.Net.Http;
using System.Threading.Tasks;

namespace WebService.Extensions
{
    public static class HttpClientExtension
    {
        public static async Task<T> GetAsync<T>(this HttpClient client, string url)
        {
            var response = await client.GetAsync(url);
            var content = await response.Content.ReadAsStringAsync();
            var result = JsonConvert.DeserializeObject<T>(content);
            return result;
        }
    }
}
