// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See the License.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using System.Security;
using System.Text.RegularExpressions;

namespace Microsoft.Configuration.ConfigurationBuilders
{
    /// <summary>
    /// Base class for a set of ConfigurationBuilders that follow a basic key/value pair substitution model. This base
    /// class handles substitution modes and most prefix concerns, so implementing classes only need to be a basic
    /// source of key/value pairs through the <see cref="GetValue(string)"/> and <see cref="GetAllValues(string)"/> methods.
    /// </summary>
    public abstract class KeyValueConfigBuilder : ConfigurationBuilder
    {
        #pragma warning disable CS1591 // No xml comments for tag literals.
        public const string modeTag = "mode";
        public const string prefixTag = "prefix";
        public const string stripPrefixTag = "stripPrefix";
        public const string tokenPatternTag = "tokenPattern";
        public const string optionalTag = "optional";
        public const string enabledTag = "enabled";
        public const string escapeTag = "escapeExpandedValues";
        public const string charMapTag = "charMap";
        public const string recursionGuardTag = "recur";
        #pragma warning restore CS1591 // No xml comments for tag literals.

        private NameValueCollection _config = null;
        private IDictionary<string, string> _cachedValues;
        private bool _lazyInitializeStarted = false;
        private bool _lazyInitialized = false;
        private bool _greedyInitialized = false;

        /// <summary>
        /// Gets or sets the substitution pattern to be used by the KeyValueConfigBuilder.
        /// </summary>
        public KeyValueMode Mode { get { EnsureInitialized(); return _mode; } }
        private KeyValueMode _mode = KeyValueMode.Strict;

        /// <summary>
        /// Gets or sets a prefix string that must be matched by keys to be considered for value substitution.
        /// </summary>
        public string KeyPrefix { get { EnsureInitialized(); return _keyPrefix; } }
        private string _keyPrefix = "";

        private bool StripPrefix { get { EnsureInitialized(); return _stripPrefix; } }
        private bool _stripPrefix = false;  // Prefix-stripping is all handled in this base class; this is private so it doesn't confuse sub-classes.

        /// <summary>
        /// Specifies whether the config builder should cause errors if the backing source cannot be found.
        /// </summary>
        [Obsolete("Please use the 'Enabled' flag instead to specify optional builders.")]
        public bool Optional { get { return Enabled != KeyValueEnabled.Enabled; } protected set { _enabled = value ? KeyValueEnabled.Optional : KeyValueEnabled.Enabled; } }
        /// <summary>
        /// Specifies whether the config builder should cause errors if the backing source cannot be found.
        /// </summary>
        public bool IsOptional { get { return Enabled != KeyValueEnabled.Enabled; } }

        /// <summary>
        /// Specifies whether the config builder should cause errors if the backing source cannot be found, or even run at all.
        /// </summary>
        public KeyValueEnabled Enabled { get { EnsureInitialized(); return _enabled; } protected set { _enabled = value; } }
        private KeyValueEnabled _enabled = KeyValueEnabled.Optional;

        /// <summary>
        /// Specifies whether the config builder should cause errors if the backing source cannot be found.
        /// </summary>
        public bool EscapeValues { get { EnsureInitialized(); return _escapeValues; } protected set { _escapeValues = value; } }
        private bool _escapeValues = false;

        /// <summary>
        /// Gets or sets a regular expression used for matching tokens during 'Token' substitution.
        /// </summary>
        public string TokenPattern { get { EnsureInitialized(); return _tokenPattern; } protected set { _tokenPattern = value; } }
        //private string _tokenPattern = @"\$\{(\w+)\}";
        private string _tokenPattern = @"\$\{(\w[\w-_$@#+,.:~]*)\}";    // Updated to be more reasonable for V2
        //private string _tokenPattern = @"\$\{(\w[\w-_$@#+,.~]*)(?:\:([^}]*))?\}";    // Something like this to allow default values

        /// <summary>
        /// Gets or sets the behavior to use when recursion is detected.
        /// </summary>
        public RecursionGuardValues Recursion { get { return _recur; } private set { _recur = value; } }
        private RecursionGuardValues _recur = RecursionGuardValues.Throw;

        /// <summary>
        /// Gets or sets a string-represented mapping of characters to apply when mapping keys. Escape with doubles. Ex "@=a,$=S" or "a-z=a,,z,0-9=0,,9"
        /// </summary>
        public Dictionary<string, string> CharacterMap { get { EnsureInitialized(); return _characterMap; } protected set { _characterMap = value; } }
        private Dictionary<string, string> _characterMap = new Dictionary<string, string>();

        /// <summary>
        /// Gets the ConfigurationSection object that is currently being processed by this builder.
        /// </summary>
        protected ConfigurationSection CurrentSection { get { return _currentSection; } }
        private ConfigurationSection _currentSection = null;

        /// <summary>
        /// Looks up a single 'value' for the given 'key.'
        /// </summary>
        /// <param name="key">The 'key' to look up in the config source. (Prefix handling is not needed here.)</param>
        /// <returns>The value corresponding to the given 'key' or null if no value is found.</returns>
        public abstract string GetValue(string key);

        /// <summary>
        /// Retrieves all known key/value pairs for the configuration source where the key begins with with <paramref name="prefix"/>.
        /// </summary>
        /// <param name="prefix">A prefix string to filter the list of potential keys retrieved from the source.</param>
        /// <returns>A collection of key/value pairs.</returns>
        public abstract ICollection<KeyValuePair<string, string>> GetAllValues(string prefix);

        /// <summary>
        /// Transform the given key to an intermediate format that will be used to look up values in backing store.
        /// </summary>
        /// <param name="key">The string to be mapped.</param>
        /// <returns>The key string to be used while looking up config values..</returns>
        public virtual string MapKey(string key)
        {
            if (String.IsNullOrEmpty(key))
                return key;

            foreach (var mapping in CharacterMap)
                key = key.Replace(mapping.Key, mapping.Value);

            return key;
        }

        /// <summary>
        /// Makes a determination about whether the input key is valid for this builder and backing store.
        /// </summary>
        /// <param name="key">The string to be validated. May be partial.</param>
        /// <returns>True if the string is valid. False if the string is not a valid key.</returns>
        public virtual bool ValidateKey(string key) { return true; }

        /// <summary>
        /// Transforms the raw key to a new string just before updating items in Strict and Greedy modes.
        /// </summary>
        /// <param name="rawKey">The key as read from the incoming config section.</param>
        /// <returns>The key string that will be left in the processed config section.</returns>
        public virtual string UpdateKey(string rawKey) { return rawKey; }

        /// <summary>
        /// Initializes the configuration builder.
        /// </summary>
        /// <param name="name">The friendly name of the provider.</param>
        /// <param name="config">A collection of the name/value pairs representing builder-specific attributes specified in the configuration for this provider.</param>
        public override void Initialize(string name, NameValueCollection config)
        {
            base.Initialize(name, config);
            _config = config ?? new NameValueCollection();

            if (_config[recursionGuardTag] != null)
            {
                // We want an exception here if 'recursionCheck' is specified but unrecognized.
                Recursion = (RecursionGuardValues)Enum.Parse(typeof(RecursionGuardValues), config[recursionGuardTag], true);
            }
        }

        /// <summary>
        /// Initializes the configuration builder lazily.
        /// </summary>
        /// <param name="name">The friendly name of the provider.</param>
        /// <param name="config">A collection of the name/value pairs representing builder-specific attributes specified in the configuration for this provider.</param>
        protected virtual void LazyInitialize(string name, NameValueCollection config)
        {
            // We need this first so we can look for tokens to replace with AppSettings
            _tokenPattern = config[tokenPatternTag] ?? _tokenPattern;

            // Next, check 'enabled' to see if we even need to do anything.
            // 'optional' is obsolete, but we'll still honor it only if it is set explicitly and does not conflict
            // with an explicit 'enabled' attribute.
            _enabled = (UpdateConfigSettingWithAppSettings(enabledTag) != null) ? (KeyValueEnabled)Enum.Parse(typeof(KeyValueEnabled), config[enabledTag], true) : _enabled;
            if (config[enabledTag] == null)
            {
                // There was no explicit 'enabled' attribute, but we have our default. Only change if we find an explicit 'optional'.
                if (UpdateConfigSettingWithAppSettings(optionalTag) != null)
                    _enabled = Boolean.Parse(config[optionalTag]) ? KeyValueEnabled.Optional : KeyValueEnabled.Enabled;
            }

            // At this point, we have our 'Enabled' choice. If we are disabled, we can stop right here.
            if (_enabled == KeyValueEnabled.Disabled) return;

            // Use pre-assigned defaults if not specified. Non-freeform options should throw on unrecognized values.
            _mode = (UpdateConfigSettingWithAppSettings(modeTag) != null) ? (KeyValueMode)Enum.Parse(typeof(KeyValueMode), config[modeTag], true) : _mode;
            _keyPrefix = UpdateConfigSettingWithAppSettings(prefixTag) ?? _keyPrefix;
            _stripPrefix = (UpdateConfigSettingWithAppSettings(stripPrefixTag) != null) ? Boolean.Parse(config[stripPrefixTag]) : _stripPrefix;
            _escapeValues = (UpdateConfigSettingWithAppSettings(escapeTag) != null) ? Boolean.Parse(config[escapeTag]) : _escapeValues;
            _characterMap = (UpdateConfigSettingWithAppSettings(charMapTag) != null) ? ParseCharacterMap(config[charMapTag]) : _characterMap;

            _cachedValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Perform token substitution on a config parameter passed through builder initialization using token values from appSettings.
        /// </summary>
        /// <param name="configName">The name of the parameter to be retrieved.</param>
        /// <returns>The updated parameter value if it exists. Null otherwise.</returns>
        protected string UpdateConfigSettingWithAppSettings(string configName)
        {
            string configValue = _config[configName];

            if (!_lazyInitializeStarted || String.IsNullOrWhiteSpace(configValue))
                return configValue;

            configValue = Regex.Replace(configValue, _tokenPattern, (m) =>
            {
                string settingName = m.Groups[1].Value;
                string defaultValue = (m.Groups[2].Success) ? m.Groups[2].Value : m.Groups[0].Value;

                // If we are processing appSettings in ProcessConfigurationSection(), then we can use that. Other config builders in
                // the chain before us have already finished, so this is a relatively consistent and logical state to draw from.
                if (CurrentSection is AppSettingsSection appSettings && CurrentSection.SectionInformation?.SectionName == "appSettings")
                    return (appSettings.Settings[settingName]?.Value ?? defaultValue);

                // Try to use CurrentConfiguration before falling back to ConfigurationManager. Otherwise OpenConfiguration()
                // scenarios won't work because we're looking in the wrong processes AppSettings.
                else if (CurrentSection?.CurrentConfiguration?.AppSettings is AppSettingsSection currentAppSettings)
                    return (currentAppSettings.Settings[settingName]?.Value ?? defaultValue);

                // All other config sections can just go through ConfigurationManager to get app settings though. :)
                return (ConfigurationManager.AppSettings[settingName] ?? defaultValue);
            });

            _config[configName] = configValue;
            return configValue;
        }

        /// <summary>
        /// Use <see cref="GetAllValues(string)" /> to populate a cache of possible key/value pairs and avoid
        /// querying the config source multiple times. Always called by base in 'Greedy' mode. May also be called by
        /// individual builders in some other cases.
        /// </summary>
        protected void EnsureGreedyInitialized()
        {
            try
            {
                // In Greedy mode, we need to know all the key/value pairs from this config source. So we
                // can't 'cache' them as we go along. Slurp them all up now. But only once. ;)
                if (!_greedyInitialized)
                {
                    string prefix = MapKey(KeyPrefix);  // Do this outside the lock. It ensures _cachedValues is initialized.
                    lock (_cachedValues)
                    {
                        if (!_greedyInitialized && (String.IsNullOrEmpty(prefix) || ValidateKey(prefix)))
                        {
                            foreach (KeyValuePair<string, string> kvp in GetAllValues(prefix))
                            {
                                _cachedValues.Add(kvp.Key, kvp.Value);
                            }
                            _greedyInitialized = true;
                        }
                    }
                }
            }
            catch (Exception ex) when (!KeyValueExceptionHelper.IsKeyValueConfigException(ex))
            {
                throw KeyValueExceptionHelper.CreateKVCException("GetAllValues() Error", ex, this);
            }
        }

        //=========================================================================================================================
        #region "Private" stuff
        // Sub-classes need not worry about this stuff, even though some of it is "public" because it comes from the framework.

        /// <summary>
        ///  (Warning: Overriding may interfere with recursion detection.)
        /// </summary>
        public override ConfigurationSection ProcessConfigurationSection(ConfigurationSection configSection)
        {
            _currentSection = configSection;

            using (var rg = new RecursionGuard(this, configSection.SectionInformation?.Name, Recursion))
            {
                // Don't do anything more if we are disabled or getting caught in recursion.
                if (rg.ShouldStop || Enabled == KeyValueEnabled.Disabled)
                    return configSection;

                // See if we know how to process this section
                ISectionHandler handler = SectionHandlersSection.GetSectionHandler(configSection);
                if (handler == null)
                    return configSection;


                // Strict Mode. Only replace existing key/values.
                if (Mode == KeyValueMode.Strict)
                {
                    foreach (var configItem in handler.KeysValuesAndState())
                    {
                        // Presumably, UpdateKey will preserve casing appropriately, so newKey is cased as expected.
                        string newKey = UpdateKey(configItem.Item1);
                        string newValue = GetValueInternal(configItem.Item1);

                        if (newValue != null)
                            handler.InsertOrUpdate(newKey, newValue, configItem.Item1, configItem.Item3);
                    }
                }

                // Token Mode. Replace tokens in existing key/values.
                else if (Mode == KeyValueMode.Token)
                {
                    foreach (var configItem in handler.KeysValuesAndState())
                    {
                        string newKey = ExpandTokens(configItem.Item1);
                        string newValue = ExpandTokens(configItem.Item2);

                        if (newValue != null)
                            handler.InsertOrUpdate(newKey, newValue, configItem.Item1, configItem.Item3);
                    }
                }

                // Greedy Mode. Insert all key/values.
                else if (Mode == KeyValueMode.Greedy)
                {
                    EnsureGreedyInitialized();

                    // Cached keys have already been 'mapped', but the prefix property we're about to use for trimming them
                    // hasn't. Do that here so we are sure to correctly trim prefixes according to the way they are mapped.
                    string prefix = MapKey(KeyPrefix);
                    foreach (KeyValuePair<string, string> kvp in _cachedValues)
                    {
                        if (kvp.Value != null)
                        {
                            // Here, kvp.Key is not from the config file, so it might not be correctly cased. Get the correct casing for UpdateKey.
                            string oldKey = TrimPrefix(handler.TryGetOriginalCase(kvp.Key), prefix);
                            string newKey = UpdateKey(oldKey);
                            handler.InsertOrUpdate(newKey, kvp.Value, oldKey);
                        }
                    }
                }
            }

            _currentSection = null;
            return configSection;
        }

        private void EnsureInitialized()
        {
            if (!_lazyInitialized)
            {
                lock (this)
                {
                    if (!_lazyInitialized && !_lazyInitializeStarted)
                    {
                        try
                        {
                            _lazyInitializeStarted = true;
                            LazyInitialize(Name, _config);
                            _lazyInitialized = true;
                        }
                        catch (Exception ex) when (!KeyValueExceptionHelper.IsKeyValueConfigException(ex))
                        {
                            throw KeyValueExceptionHelper.CreateKVCException("Initialization Error", ex, this);
                        }
                    }
                }
            }
        }

        private string ExpandTokens(string rawString)
        {
            string updatedString = Regex.Replace(rawString, TokenPattern, (m) =>
                {
                    string key = m.Groups[1].Value;
                    string defaultValue = (m.Groups[2].Success) ? m.Groups[2].Value : m.Groups[0].Value;

                    // Same prefix-handling rules apply in token mode as in strict mode.
                    // Since the key is being completely replaced by the value, we don't need to call UpdateKey().
                    return EscapeValue(GetValueInternal(key)) ?? defaultValue;
                });

            return updatedString;
        }

        private string GetValueInternal(string key)
        {
            if (String.IsNullOrEmpty(key))
                return null;

            try
            {
                // Make sure the key we are looking up begins with the correct prefix... if we are not stripping prefixes.
                if (!StripPrefix && !key.StartsWith(KeyPrefix, StringComparison.OrdinalIgnoreCase))
                    return null;

                // Stripping Prefix in strict mode means from the source key. The static config file will have a prefix-less key to match.
                // ie <add key="MySetting" /> should only match the key/value (KeyPrefix + "MySetting") from the source.
                string sourceKey = MapKey((StripPrefix) ? KeyPrefix + key : key);

                if (!ValidateKey(sourceKey))
                    return null;

                return (_cachedValues.ContainsKey(sourceKey)) ? _cachedValues[sourceKey] : _cachedValues[sourceKey] = GetValue(sourceKey);
            }
            catch (Exception ex) when (!KeyValueExceptionHelper.IsKeyValueConfigException(ex))
            {
                throw KeyValueExceptionHelper.CreateKVCException("GetValue() Error", ex, this);
            }
        }

        private string TrimPrefix(string fullString, string prefix = null)
        {
            prefix = prefix ?? KeyPrefix;

            if (!StripPrefix || !fullString.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return fullString;

            return fullString.Substring(prefix.Length);
        }

        // Maybe this could be virtual? Simple xml escaping should be enough for most folks.
        private string EscapeValue(string original)
        {
            return (_escapeValues && original != null) ? SecurityElement.Escape(original) : original;
        }

        private Dictionary<string, string> ParseCharacterMap(string stringMap)
        {
            // The format here is string=string,string=string.
            // To use separators in your maps, escape them by doubling.
            Dictionary<string, string> charmap = new Dictionary<string, string>();
            char[] coupler = { '=' };
            char[] delimiter = { ',' };

            if (String.IsNullOrWhiteSpace(stringMap))
                return charmap;

            try
            {
                // Break the string into pairs - Account for escaped ','s
                var pairs = stringMap.Replace(",,", "\x30").Split(delimiter, StringSplitOptions.RemoveEmptyEntries);

                foreach (string pairing in pairs)
                {
                    // Remember to un-escape any ','s first, and do then escape escaped '='
                    var mapping = pairing.Replace("\x30", ",").Replace("==", "\x30").Split(coupler, 2, StringSplitOptions.RemoveEmptyEntries);

                    // If we have a 'mapping' that does not have two parts, this is an error
                    if (mapping.Length < 2)
                        throw new ArgumentException("Mapping should be a ',' delimited list of strings paired with '='. Use double characters to escape ',' and '='.", charMapTag);

                    // Remember to un-escape any '='s first
                    mapping[0] = mapping[0].Replace("\x30", "=");
                    mapping[1] = mapping[1].Replace("\x30", "=");

                    charmap.Add(mapping[0], mapping[1]);
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Error in Configuration Builder '{Name}' while parsing '{charMapTag}'", ex);
            }

            return charmap;
        }

        #endregion
        //=========================================================================================================================
    }
}