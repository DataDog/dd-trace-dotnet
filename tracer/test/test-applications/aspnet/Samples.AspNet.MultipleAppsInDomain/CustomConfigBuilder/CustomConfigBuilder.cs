// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See the License.txt file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;


namespace Microsoft.Configuration.ConfigurationBuilders
{
    /// <summary>
    /// A ConfigurationProvider that (theoretically) pulls secrets from a remove api
    /// Modelled on:
    /// https://github.com/aspnet/MicrosoftConfigurationBuilders/blob/main/src/Azure/AzureKeyVaultConfigBuilder.cs
    /// Doesn't _actually_ do anything useful with the data, just to get something that's instrumented _before_ we're initialized
    /// </summary>
    public class CustomConfigBuilder : KeyValueConfigBuilder
    {
        #pragma warning disable CS1591 // No xml comments for tag literals.
        #pragma warning restore CS1591 // No xml comments for tag literals.
        public const string vaultNameTag = "vaultName";
        public const string connectionStringTag = "connectionString";   // obsolete
        public const string uriTag = "uri";
        public const string versionTag = "version";
        public const string preloadTag = "preloadSecretNames";

        /// <summary>
        /// Gets or sets a property indicating whether the builder should request a list of all keys from the vault before
        /// looking up secrets. (This knowledge may reduce the number of requests made to KeyVault, but could also bring
        /// large amounts of data into memory that may be unwanted.)
        /// </summary>
        public bool Preload { get; protected set; }

        private HttpClient _kvClient;
        private Lazy<List<string>> _allKeys;

        public static List<KeyVaultSecret> Secrets =
        [
            new("DummyKey1", "DummyValue1 - from custom config"),
            new("DummyKey2", "DummyValue2 - from custom config"),
            new("DummyKey3", "DummyValue3 - from custom config"),
            new("DummyKey4", "DummyValue4 - from custom config"),
            new("DummyKey5", "DummyValue5 - from custom config"),
        ];

        /// <summary>
        /// Initializes the configuration builder lazily.
        /// </summary>
        /// <param name="name">The friendly name of the provider.</param>
        /// <param name="config">A collection of the name/value pairs representing builder-specific attributes specified in the configuration for this provider.</param>
        protected override void LazyInitialize(string name, NameValueCollection config)
        {
            // Default to 'Enabled'. base.LazyInitialize() will override if specified in config.
            Enabled = KeyValueEnabled.Enabled;

            // Key Vault names can only contain [a-zA-Z0-9] and '-'.
            // https://docs.microsoft.com/en-us/azure/key-vault/general/about-keys-secrets-certificates
            // That's a lot of disallowed characters to map away. Fortunately, 'charMap' allows users
            // to do this on a per-case basis. But let's cover some common cases by default.
            // Don't add '/' to the map though, as that will mess up versioned keys.
            CharacterMap.Add(":", "-");
            CharacterMap.Add("_", "-");
            CharacterMap.Add(".", "-");
            CharacterMap.Add("+", "-");
            CharacterMap.Add(@"\", "-");

            base.LazyInitialize(name, config);

            // At this point, we have our 'Enabled' choice. If we are disabled, we can stop right here.
            if (Enabled == KeyValueEnabled.Disabled) return;

            // It's lazy, but if something goes off-track before we do this... well, we'd at least like to
            // work with an empty list rather than a null list. So do this up front.
            _allKeys = new Lazy<List<string>>(() => GetAllKeys(), System.Threading.LazyThreadSafetyMode.ExecutionAndPublication);

            Preload = true;

            // Connect to http
            try
            {
                _kvClient = new HttpClient();
            }
            catch (Exception)
            {
                if (!IsOptional)
                    throw;
                _kvClient = null;
            }

        }

        /// <summary>
        /// Looks up a single 'value' for the given 'key.'
        /// </summary>
        /// <param name="key">The 'key' for the secret to look up in the configured Key Vault. (Prefix handling is not needed here.)</param>
        /// <returns>The value corresponding to the given 'key' or null if no value is found.</returns>
        public override string GetValue(string key)
        {
            // hit the network!
            // Only hit the network if we didn't preload, or if we know the key exists after preloading.
            if (!Preload || _allKeys.Value.Contains(key, StringComparer.OrdinalIgnoreCase))
            {
                // Azure Key Vault keys are case-insensitive, so this should be fine.
                // vKey.Version here is either the same as this.Version or this.Version is null
                // Also, this is a synchronous method. And in single-threaded contexts like ASP.Net
                // it can be bad/dangerous to block on async calls. So lets work some TPL voodoo
                // to avoid potential deadlocks.
                return Task.Run(async () => { return await GetValueAsync(key, string.Empty); }).Result?.Value;
            }

            return null;
        }

        /// <summary>
        /// Returns a Boolean value indicating whether the given exception is should be considered an optional issue that
        /// should be ignored or whether the exception should bubble up. This should consult <see cref="KeyValueConfigBuilder.IsOptional"/>.
        /// </summary>
        /// <param name="e"></param>
        /// <returns>A Boolean to indicate whether the exception should be ignored.</returns>
        // TODO: This should be considered for moving into KeyValueConfigBuilder as a virtual method in a major update.
        // But for now, leave it here since we don't want to force a hard tie between minor versions of these packages.
        protected bool ExceptionIsOptional(Exception e)
        {
            // Failed Azure requests have different meanings
            if (e is HttpRequestException rfex)
            {
                return IsOptional;
            }

            // Even when 'optional', don't catch things unless we're certain we know what it is.
            return false;
        }


        /// <summary>
        /// Retrieves all known key/value pairs from the Key Vault where the key begins with with <paramref name="prefix"/>.
        /// </summary>
        /// <param name="prefix">A prefix string to filter the list of potential keys retrieved from the source.</param>
        /// <returns>A collection of key/value pairs.</returns>
        public override ICollection<KeyValuePair<string, string>> GetAllValues(string prefix)
        {
            ConcurrentDictionary<string, string> d = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            List<Task> tasks = new List<Task>();

            foreach (string key in _allKeys.Value)
            {
                if (key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    tasks.Add(Task.Run(() => GetValueAsync(key, string.Empty).ContinueWith(t =>
                    {
                        // Azure Key Vault keys are case-insensitive, so there shouldn't be any races here.
                        // Include version information. It will get filtered out later before updating config.
                        KeyVaultSecret secret = t.Result;
                        if (secret != null)
                        {
                            d[key] = secret.Value;
                        }
                    })));
            }
            Task.WhenAll(tasks).Wait();

            return d;
        }

        /// <summary>
        /// Makes a determination about whether the input key is valid for this builder and backing store.
        /// </summary>
        /// <param name="key">The string to be validated. May be partial.</param>
        /// <returns>True if the string is valid. False if the string is not a valid key.</returns>
        public override bool ValidateKey(string key)
        {
            // Key Vault only allows alphanumerics and '-'. This builder also allows for one '/' for versioning.
            return Regex.IsMatch(key, "^[a-zA-Z0-9-]+(/?[a-zA-Z0-9-]+)?$");
        }

        /// <summary>
        /// Transforms the raw key to a new string just before updating items in Strict and Greedy modes.
        /// </summary>
        /// <param name="rawKey">The key as read from the incoming config section.</param>
        /// <returns>The key string that will be left in the processed config section.</returns>
        public override string UpdateKey(string rawKey)
        {
            // Remove the version segment if it's there.
            return rawKey;
        }

        private async Task<KeyVaultSecret> GetValueAsync(string key, string version)
        {
            if (_kvClient == null)
                return null;

            try
            {
                using var result = await _kvClient.GetAsync("https://raw.githubusercontent.com/DataDog/dd-trace-dotnet/refs/heads/master/.env");

                // we don't actually use the value
                return Secrets.FirstOrDefault(s => s.Key == key);
            }
            catch (AggregateException ae)
            {
                ae.Handle((ex) => ExceptionIsOptional(ex)); // Re-throws if not optional
            }
            catch (Exception e) when (ExceptionIsOptional(e)) { }

            return null;
        }

        private List<string> GetAllKeys()
        {
            List<string> keys = new List<string>(); // KeyVault keys are case-insensitive. There won't be case-duplicates. List<> should be fine.

            // Don't go loading all the keys if we can't, or if we were told not to
            if (_kvClient == null || !Preload)
                return keys;

            try
            {
                // make HTTP Request (? )
                var result = GetValueAsync(string.Empty, string.Empty).Result;

                return Secrets.Select(c => c.Key).ToList();
            }
            catch (AggregateException ae)
            {
                ae.Handle((ex) => ExceptionIsOptional(ex)); // Re-throws if not optional
            }
            catch (Exception e) when (ExceptionIsOptional(e)) { }

            return keys;
        }
    }

    public record KeyVaultSecret(string Key, string Value)
    {
        public string Key { get; } = Key;
        public string Value { get; } = Value;
    }
}


