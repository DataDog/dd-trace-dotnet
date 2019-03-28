namespace Datadog.Trace.Configuration
{
    /// <summary>
    /// A source of configuration settings, identifiable by a string key.
    /// </summary>
    public interface IConfigurationSource
    {
        /// <summary>
        /// Gets the <see cref="string"/> value of
        /// the setting with the specified key.
        /// </summary>
        /// <param name="key">The key that identifies the setting.</param>
        /// <returns>The value of the setting, or <c>null</c> if not found.</returns>
        string GetString(string key);

        /// <summary>
        /// Gets the <see cref="int"/> value of
        /// the setting with the specified key.
        /// </summary>
        /// <param name="key">The key that identifies the setting.</param>
        /// <returns>The value of the setting, or <c>null</c> if not found.</returns>
        int? GetInt32(string key);

        /// <summary>
        /// Gets the <see cref="double"/> value of
        /// the setting with the specified key.
        /// </summary>
        /// <param name="key">The key that identifies the setting.</param>
        /// <returns>The value of the setting, or <c>null</c> if not found.</returns>
        double? GetDouble(string key);

        /// <summary>
        /// Gets the <see cref="bool"/> value of
        /// the setting with the specified key.
        /// </summary>
        /// <param name="key">The key that identifies the setting.</param>
        /// <returns>The value of the setting, or <c>null</c> if not found.</returns>
        bool? GetBool(string key);
    }
}
