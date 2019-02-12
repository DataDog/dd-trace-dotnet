using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Configuration;
using Microsoft.AspNet.Identity;

namespace Samples.WebForms
{
    public class CoffeehouseApiUserStore : IUserStore<User>, IUserPasswordStore<User>
    {
        private static readonly string _apiBaseUrl = WebConfigurationManager.AppSettings.Get("CoffeehouseApiBaseUrl") ?? "http://localhost:8084";
        private readonly string _usersBaseUrl;

        private readonly HttpClient _apiClient = new HttpClient();

        public CoffeehouseApiUserStore()
        {
            _usersBaseUrl = _apiBaseUrl + "/users";
        }

        public void Dispose()
        {
            _apiClient.Dispose();
        }

        public async Task CreateAsync(User user)
        {
            var existingUser = await FindByIdAsync(user.Id);

            if (existingUser != null)
            {
                throw new ApplicationException("User with that ID already exists");
            }

            var httpResponse = _apiClient.PostAsJsonAsync(_usersBaseUrl, user).GetAwaiter().GetResult();

            httpResponse.EnsureSuccessStatusCode();
        }

        public Task UpdateAsync(User user)
        {
            var url = _usersBaseUrl + $"/{user.Id}";

            var httpResponse = _apiClient.PutAsJsonAsync(url, user).GetAwaiter().GetResult();

            httpResponse.EnsureSuccessStatusCode();

            return Task.FromResult(result: 0);
        }

        public Task DeleteAsync(User user)
        {
            var url = _usersBaseUrl + $"/{user.Id}";

            var httpResponse = _apiClient.DeleteAsync(url).GetAwaiter().GetResult();

            httpResponse.EnsureSuccessStatusCode();

            return Task.FromResult(result: 0);
        }

        public async Task<User> FindByIdAsync(string userId)
        {
            var url = _usersBaseUrl + $"/{userId}";

            var httpResponse = _apiClient.GetAsync(url).GetAwaiter().GetResult();

            var user = httpResponse.IsSuccessStatusCode
                           ? await httpResponse.Content.ReadAsAsync<User>()
                           : null;

            return user;
        }

        public async Task<User> FindByNameAsync(string userName)
            => await FindByIdAsync(userName);

        public Task SetPasswordHashAsync(User user, string passwordHash)
        {
            user.PasswordHash = passwordHash;

            return Task.FromResult(result: 0);
        }

        public async Task<string> GetPasswordHashAsync(User user)
        {
            if (!string.IsNullOrEmpty(user.PasswordHash))
            {
                return user.PasswordHash;
            }

            var apiUser = await FindByIdAsync(user.Id);

            return apiUser.PasswordHash;
        }

        public async Task<bool> HasPasswordAsync(User user)
        {
            var hash = await GetPasswordHashAsync(user);

            return !string.IsNullOrEmpty(hash);
        }
    }
}
