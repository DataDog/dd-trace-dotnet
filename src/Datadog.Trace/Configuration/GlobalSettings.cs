using System.IO;
using Datadog.Trace.Logging;
using Datadog.Trace.Vendors.Serilog.Events;

namespace Datadog.Trace.Configuration
{
    /// <summary>
    /// Contains global datadog settings.
    /// </summary>
    public class GlobalSettings
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="GlobalSettings"/> class with default values.
        /// </summary>
        internal GlobalSettings()
            : this(null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="GlobalSettings"/> class
        /// using the specified <see cref="IConfigurationSource"/> to initialize values.
        /// </summary>
        /// <param name="source">The <see cref="IConfigurationSource"/> to use when retrieving configuration values.</param>
        internal GlobalSettings(IConfigurationSource source)
        {
            DebugEnabled = source?.GetBool(ConfigurationKeys.DebugEnabled) ??
                           // default value
                           false;
        }

        /// <summary>
        /// Gets a value indicating whether debug mode is enabled.
        /// Default is <c>false</c>.
        /// Set in code via <see cref="SetDebugEnabled"/>
        /// </summary>
        /// <seealso cref="ConfigurationKeys.DebugEnabled"/>
        public bool DebugEnabled { get; private set; }

        /// <summary>
        /// Gets or sets the global settings instance.
        /// </summary>
        internal static GlobalSettings Source { get; set; } = FromDefaultSources();

        /// <summary>
        /// Set whether debug mode is enabled.
        /// Affects the level of logs written to file.
        /// </summary>
        /// <param name="enabled">Whether debug is enabled.</param>
        public static void SetDebugEnabled(bool enabled)
        {
            Source.DebugEnabled = enabled;

            if (enabled)
            {
                DatadogLogging.SetLogLevel(LogEventLevel.Verbose);
            }
            else
            {
                DatadogLogging.UseDefaultLevel();
            }
        }

        /// <summary>
        /// Used to refresh global settings when environment variables or config sources change.
        /// This is not necessary if changes are set via code, only environment.
        /// </summary>
        public static void Reload()
        {
            Source = FromDefaultSources();
        }

        /// <summary>
        /// Create a <see cref="GlobalSettings"/> populated from the default sources
        /// returned by <see cref="CreateDefaultConfigurationSource"/>.
        /// </summary>
        /// <returns>A <see cref="TracerSettings"/> populated from the default sources.</returns>
        public static GlobalSettings FromDefaultSources()
        {
            var source = CreateDefaultConfigurationSource();
            return new GlobalSettings(source);
        }

        /// <summary>
        /// Creates a <see cref="IConfigurationSource"/> by combining environment variables,
        /// AppSettings where available, and a local datadog.json file, if present.
        /// </summary>
        /// <returns>A new <see cref="IConfigurationSource"/> instance.</returns>
        internal static CompositeConfigurationSource CreateDefaultConfigurationSource()
        {
            // env > AppSettings > datadog.json
            var configurationSource = new CompositeConfigurationSource
            {
                new EnvironmentConfigurationSource(),

#if !NETSTANDARD2_0
                // on .NET Framework only, also read from app.config/web.config
                new NameValueConfigurationSource(System.Configuration.ConfigurationManager.AppSettings)
#endif
            };

            string currentDirectory = System.Environment.CurrentDirectory;

#if !NETSTANDARD2_0
            // on .NET Framework only, use application's root folder
            // as default path when looking for datadog.json
            if (System.Web.Hosting.HostingEnvironment.IsHosted)
            {
                currentDirectory = System.Web.Hosting.HostingEnvironment.MapPath("~");
            }
#endif

            // if environment variable is not set, look for default file name in the current directory
            var configurationFileName = configurationSource.GetString(ConfigurationKeys.ConfigurationFileName) ??
                                        Path.Combine(currentDirectory, "datadog.json");

            if (Path.GetExtension(configurationFileName).ToUpperInvariant() == ".JSON" &&
                File.Exists(configurationFileName))
            {
                configurationSource.Add(JsonConfigurationSource.FromFile(configurationFileName));
            }

            return configurationSource;
        }
    }
}
