// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See the License.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using System.Configuration.Provider;

namespace Microsoft.Configuration.ConfigurationBuilders
{
    // Covariance is not allowed with generic classes. Lets use this trick instead.
    internal interface ISectionHandler
    {
        void InsertOrUpdate(string newKey, string newValue, string oldKey = null, object oldItem = null);
        IEnumerable<Tuple<string, string, object>> KeysValuesAndState();
        string TryGetOriginalCase(string requestedKey);
    }

    /// <summary>
    /// A class to be used by <see cref="KeyValueConfigBuilder"/>s to apply key/value config pairs to .Net configuration sections.
    /// </summary>
    /// <typeparam name="T">The type of <see cref="ConfigurationSection"/> that the implementing class can process.</typeparam>
    public abstract class SectionHandler<T> : ProviderBase, ISectionHandler where T : ConfigurationSection
    {
        /// <summary>
        /// The <see cref="ConfigurationSection"/> instance being processed by this <see cref="SectionHandler{T}"/>.
        /// </summary>
        public T ConfigSection { get; private set; }

        /// <summary>
        /// Obsolete: Please implement <see cref="KeysValuesAndState()" /> to provide enumeration compatible with <see cref="KeyValueMode.Token"/>
        /// </summary>
        /// <returns>An enumerator over pairs consisting of the existing key for a config value in the config section, and an object reference
        /// for the key/value pair to be passed in to <see cref="InsertOrUpdate(string, string, string, object)"/> while processing the config section.</returns>
        [Obsolete]
        public virtual IEnumerator<KeyValuePair<string, object>> GetEnumerator()
        {
            return null;
        }

        /// <summary>
        /// Gets an <see cref="IEnumerable{T}"/> for iterating over the key/value pairs contained in the assigned <see cref="ConfigSection"/>. />
        /// </summary>
        /// <returns>An <see cref="IEnumerable{T}"/> of <see cref="Tuple{T1, T2, T3}"/> for iterating over each key/value pair contained in
        /// the config section. Each Tuple contains the 'key', 'value', and a 'state' object which is passed back to this SectionHandler
        /// when updating records with <see cref="InsertOrUpdate(string, string, string, object)"/>.</returns>
        public virtual IEnumerable<Tuple<string, string, object>> KeysValuesAndState()
        {
#pragma warning disable CS0612 // Type or member is obsolete
            foreach (KeyValuePair<string, object> kvp in this)
#pragma warning restore CS0612 // Type or member is obsolete
                yield return Tuple.Create(kvp.Key, (string)null, kvp.Value);
        }

        /// <summary>
        /// Updates an existing config value in the assigned <see cref="ConfigSection"/> with a new key and a new value. The old config value
        /// can be located using the <paramref name="oldKey"/> or <paramref name="oldItem"/> parameters. If an old config value is not
        /// found, a new config value should be inserted.
        /// </summary>
        /// <param name="newKey">The updated key name for the config item.</param>
        /// <param name="newValue">The updated value for the config item.</param>
        /// <param name="oldKey">The old key name for the config item, or null.</param>
        /// <param name="oldItem">A reference to the old key/value pair obtained by <see cref="GetEnumerator"/>, or null.</param>
        public abstract void InsertOrUpdate(string newKey, string newValue, string oldKey = null, object oldItem = null);

        /// <summary>
        /// Attempt to lookup the original key casing so it can be preserved during greedy updates which would otherwise lose
        /// the original casing in favor of the casing used in the config source.
        /// </summary>
        /// <param name="requestedKey">The key to find original casing for.</param>
        /// <returns>Unless overridden, returns the string passed in.</returns>
        public virtual string TryGetOriginalCase(string requestedKey)
        {
            return requestedKey;
        }


#pragma warning disable IDE0051 // Remove unused private members
        // We call this depending on section type via reflection in SectionHandlerSection.GetSectionHandler()
        private void Initialize(string name, T configSection, NameValueCollection config)
#pragma warning restore IDE0051 // Remove unused private members
        {
            ConfigSection = configSection;
            Initialize(name, config);
        }
    }

    /// <summary>
    /// A class that can be used by <see cref="KeyValueConfigBuilder"/>s to apply key/value config pairs to <see cref="AppSettingsSection"/>.
    /// </summary>
    public class AppSettingsSectionHandler : SectionHandler<AppSettingsSection>
    {
        /// <summary>
        /// Updates an existing app setting in the assigned <see cref="SectionHandler{T}.ConfigSection"/> with a new key and a new value. The old setting
        /// can be located using the <paramref name="oldKey"/> parameter. If an old setting is not found, a new setting should be inserted.
        /// </summary>
        /// <param name="newKey">The updated key name for the app setting.</param>
        /// <param name="newValue">The updated value for the app setting.</param>
        /// <param name="oldKey">The old key name for the app setting, or null.</param>
        /// <param name="oldItem">The old key name for the app setting, or null., or null.</param>
        public override void InsertOrUpdate(string newKey, string newValue, string oldKey = null, object oldItem = null)
        {
            if (newValue != null)
            {
                if (oldKey != null)
                    ConfigSection.Settings.Remove(oldKey);

                ConfigSection.Settings.Remove(newKey);
                ConfigSection.Settings.Add(newKey, newValue);
            }
        }

        /// <summary>
        /// Gets an <see cref="IEnumerable{T}"/> for iterating over the key/value pairs contained in the assigned <see cref="SectionHandler{T}.ConfigSection"/>. />
        /// </summary>
        /// <returns>An enumerator over tuples where the values of the tuple are the existing key for each setting, the old value for the
        /// setting, and the existing key for the setting again as the state which will be returned unmodified when updating.</returns>
        public override IEnumerable<Tuple<string, string, object>> KeysValuesAndState()
        {
            // Grab a copy of the keys array since we are using 'yield' and the Settings collection may change on us.
            ConfigurationElement[] allSettings = new ConfigurationElement[ConfigSection.Settings.Count];
            ConfigSection.Settings.CopyTo(allSettings, 0);
            foreach (KeyValueConfigurationElement setting in allSettings)
                yield return Tuple.Create(setting.Key, setting.Value, (object)setting.Key);
        }

        /// <summary>
        /// Attempt to lookup the original key casing so it can be preserved during greedy updates which would otherwise lose
        /// the original casing in favor of the casing used in the config source.
        /// </summary>
        /// <param name="requestedKey">The key to find original casing for.</param>
        /// <returns>A string containing the key with original casing from the config section, or the key as passed in if no match
        /// can be found.</returns>
        public override string TryGetOriginalCase(string requestedKey)
        {
            if (!String.IsNullOrWhiteSpace(requestedKey))
            {
                var keyval = ConfigSection.Settings[requestedKey];
                if (keyval != null)
                    return keyval.Key;
            }

            return base.TryGetOriginalCase(requestedKey);
        }
    }

    /// <summary>
    /// A class that can be used by <see cref="KeyValueConfigBuilder"/>s to apply key/value config pairs to <see cref="ConnectionStringsSection"/>.
    /// </summary>
    public class ConnectionStringsSectionHandler : SectionHandler<ConnectionStringsSection>
    {
        /// <summary>
        /// Updates an existing connection string in the assigned <see cref="SectionHandler{T}.ConfigSection"/> with a new name and a new value. The old
        /// connection string can be located using the <paramref name="oldItem"/> parameter. If an old connection string is not found, a new connection
        /// string should be inserted.
        /// </summary>
        /// <param name="newKey">The updated key name for the connection string.</param>
        /// <param name="newValue">The updated value for the connection string.</param>
        /// <param name="oldKey">The old key name for the connection string, or null.</param>
        /// <param name="oldItem">A reference to the old <see cref="ConnectionStringSettings"/> object obtained by <see cref="KeysValuesAndState"/>, or null.</param>
        public override void InsertOrUpdate(string newKey, string newValue, string oldKey = null, object oldItem = null)
        {
            if (newValue != null)
            {
                // Preserve the old entry if it exists, as it might have more than just name/connectionString attributes.
                ConnectionStringSettings cs = (oldItem as ConnectionStringSettings) ?? new ConnectionStringSettings();

                // Make sure there are no entries using the old or new name other than this one
                ConfigSection.ConnectionStrings.Remove(oldKey);
                ConfigSection.ConnectionStrings.Remove(newKey);

                // Update values and re-add to the collection
                cs.Name = newKey;
                cs.ConnectionString = newValue;
                ConfigSection.ConnectionStrings.Add(cs);
            }
        }

        /// <summary>
        /// Gets an <see cref="IEnumerable{T}"/> for iterating over the key/value pairs contained in the assigned <see cref="SectionHandler{T}.ConfigSection"/>. />
        /// </summary>
        /// <returns>An enumerator over tuples where the values of the tuple are the existing name for each connection string, the value of
        /// the connection string, and a reference to the <see cref="ConnectionStringSettings"/> object itself which will be returned to
        /// us as a reference state object when updating the config record.</returns>
        public override IEnumerable<Tuple<string, string, object>> KeysValuesAndState()
        {
            // The ConnectionStrings collection may change on us while we enumerate. :/
            ConnectionStringSettings[] connStrs = new ConnectionStringSettings[ConfigSection.ConnectionStrings.Count];
            ConfigSection.ConnectionStrings.CopyTo(connStrs, 0);

            foreach (ConnectionStringSettings cs in connStrs)
                yield return Tuple.Create(cs.Name, cs.ConnectionString, (object)cs);
        }

        /// <summary>
        /// Attempt to lookup the original key casing so it can be preserved during greedy updates which would otherwise lose
        /// the original casing in favor of the casing used in the config source.
        /// </summary>
        /// <param name="requestedKey">The key to find original casing for.</param>
        /// <returns>A string containing the key with original casing from the config section, or the key as passed in if no match
        /// can be found.</returns>
        public override string TryGetOriginalCase(string requestedKey)
        {
            if (!String.IsNullOrWhiteSpace(requestedKey))
            {
                var connStr = ConfigSection.ConnectionStrings[requestedKey];
                if (connStr != null)
                    return connStr.Name;
            }

            return base.TryGetOriginalCase(requestedKey);
        }
    }

    /// <summary>
    /// A class that can be used by <see cref="KeyValueConfigBuilder"/>s to apply key/value config pairs to <see cref="ConnectionStringsSection"/>
    /// with special 'tagging' to allow updating both the 'connectionString' attribute as well as the 'providerName' attribute.
    /// </summary>
    public class ConnectionStringsSectionHandler2 : SectionHandler<ConnectionStringsSection>
    {
        private const string connStrNameTag = ":connectionString";
        private const string providerNameTag = ":providerName";

        private class CSSH2State { public bool UpdateName; public ConnectionStringSettings CS; }

        /// <summary>
        /// Updates an existing connection string attribute in the assigned <see cref="SectionHandler{T}.ConfigSection"/> with a new name and a new value. The old
        /// connection string setting can be located using the <paramref name="oldItem"/> parameter. If an old connection string is not found, a new connection
        /// string should be inserted.
        /// </summary>
        /// <param name="newKey">The updated key name for the connection string. May be post-fixed with attribute tag.</param>
        /// <param name="newValue">The updated value for the connection string.</param>
        /// <param name="oldKey">The old key name for the connection string, or null. May be post-fixed with attribute tag.</param>
        /// <param name="oldItem">A reference to the old <see cref="ConnectionStringSettings"/> object obtained by <see cref="KeysValuesAndState"/>, or null.</param>
        public override void InsertOrUpdate(string newKey, string newValue, string oldKey = null, object oldItem = null)
        {
            string tag;
            (oldKey, tag) = SplitTag(oldKey);
            (newKey, _) = SplitTag(newKey);

            CSSH2State state = oldItem as CSSH2State;
            ConnectionStringSettings cs = state?.CS ?? ConfigSection.ConnectionStrings[oldKey] ?? new ConnectionStringSettings();

            // Make sure there are no entries using the old or new name other than this one
            ConfigSection.ConnectionStrings.Remove(oldKey);
            ConfigSection.ConnectionStrings.Remove(newKey);

            // Update values and re-add to the collection (no state means 'Greedy' mode where we do want to update)
            if (state == null || state.UpdateName)
                cs.Name = newKey;
            if (tag == providerNameTag)
                cs.ProviderName = newValue;
            else
                cs.ConnectionString = newValue;
            ConfigSection.ConnectionStrings.Add(cs);
        }

        /// <summary>
        /// Gets an <see cref="IEnumerable{T}"/> for iterating over the key/value pairs contained in the assigned <see cref="SectionHandler{T}.ConfigSection"/>. />
        /// </summary>
        /// <returns>An enumerator over tuples where the values of the tuple are the existing name for each connection string, the value of
        /// the connection string or the value of the provider name, and a reference to the <see cref="ConnectionStringSettings"/> object itself
        /// which will be returned to us as a reference state object when updating the config record.</returns>
        public override IEnumerable<Tuple<string, string, object>> KeysValuesAndState()
        {
            // The ConnectionStrings collection may change on us while we enumerate. :/
            ConnectionStringSettings[] connStrs = new ConnectionStringSettings[ConfigSection.ConnectionStrings.Count];
            ConfigSection.ConnectionStrings.CopyTo(connStrs, 0);

            foreach (ConnectionStringSettings cs in connStrs)
            {
                // Greedy mode doesn't enumerate here. It just goes direct to 'InsertOrUpdate', which preserves non-tagged
                //  behavior via null-tag awareness.
                // Strict mode will need us to lookup a non-tagged value in addition to tagged values in order to
                //  remain as compatible as possible with the simple old model.
                // Token mode is trickier. See step-by-step notes.

                string originalName = cs.Name;
                string originalCS = cs.ConnectionString;

                // In 'Token' mode, this will replace tokens in 'name' and 'connectionString'.
                yield return Tuple.Create(originalName, originalCS, (object)new CSSH2State() { UpdateName = true, CS = cs }) ;

                // In 'Token' mode, this will re-replace tokens in 'connectionString' only. Conceptually a no-op, except we
                //  don't know which mode we're in so we can't technically skip this re-replacement. We also can't skip this step because
                //  it is required for 'Strict' mode. (It will also re-lookup tokens in 'name', but we are able to skip replacing those
                //  here, since using _this_ tagged 'name' string might not be faithful to the original non-tagged 'name'.)
                // Also, re-lookups for tokens should be cached and free, since the tokens inside the 'name' didn't change when tagged.
                yield return Tuple.Create(originalName + connStrNameTag, originalCS, (object)new CSSH2State() { UpdateName = false, CS = cs });

                // In 'Token' mode, this will replace tokens in 'providerName' only. Same deal with 'name' as the previous step. However,
                //  the tag on the original name is important, as that is the only way we will know to work on 'providerName' instead of 'name'
                //  in 'InsertOrUpdate'.
                yield return Tuple.Create(originalName + providerNameTag, cs.ProviderName, (object)new CSSH2State() { UpdateName = false, CS = cs });
            }
        }

        /// <summary>
        /// Attempt to lookup the original key casing so it can be preserved during greedy updates which would otherwise lose
        /// the original casing in favor of the casing used in the config source.
        /// </summary>
        /// <param name="requestedKey">The key to find original casing for.</param>
        /// <returns>A string containing the key with original casing from the config section, or the key as passed in if no match
        /// can be found.</returns>
        public override string TryGetOriginalCase(string requestedKey)
        {
            if (!String.IsNullOrWhiteSpace(requestedKey))
            {
                var connStr = ConfigSection.ConnectionStrings[requestedKey];
                if (connStr != null)
                    return connStr.Name;
            }

            return base.TryGetOriginalCase(requestedKey);
        }

        private (string, string) SplitTag(string key)
        {
            if (key != null)
            {
                if (key.EndsWith(connStrNameTag))
                    return (key.Remove(key.Length - connStrNameTag.Length), connStrNameTag);
                else if (key.EndsWith(providerNameTag))
                    return (key.Remove(key.Length - providerNameTag.Length), providerNameTag);
            }

            return (key, null);
        }
    }
}