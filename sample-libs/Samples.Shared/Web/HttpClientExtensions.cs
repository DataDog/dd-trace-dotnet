using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace Samples.Shared.Web
{
    public static class HttpClientExtensions
    {
        public static async Task<T> GetAsync<T>(this HttpClient client, string url)
        {
            if (client == null)
            {
                throw new ArgumentNullException(nameof(client));
            }

            if (url == null)
            {
                throw new ArgumentNullException(nameof(url));
            }

            var response = await client.GetAsync(url);
            return await response.Content.ReadAsAsync<T>();
        }
    }
}
