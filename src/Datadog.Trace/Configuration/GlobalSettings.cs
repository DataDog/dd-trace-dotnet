using System;
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

            DiagnosticSourceEnabled = source?.GetBool(ConfigurationKeys.DiagnosticSourceEnabled) ??
                                      // default value
                                      true;
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
        /// Gets a value indicating whether the use
        /// of System.Diagnostics.DiagnosticSource is enabled.
        /// This value can only be set with environment variables
        /// or a configuration file, not through code.
        /// </summary>
        internal bool DiagnosticSourceEnabled { get; }

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
                DatadogLogging.SetLogLevel(LogEventLevel.Debug);
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
            DatadogLogging.Reset();
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

#if NETFRAMEWORK
                // on .NET Framework only, also read from app.config/web.config
                new NameValueConfigurationSource(System.Configuration.ConfigurationManager.AppSettings)
#endif
            };

            if (TryLoadJsonConfigurationFile(configurationSource, out var jsonConfigurationSource))
            {
                configurationSource.Add(jsonConfigurationSource);
            }

            return configurationSource;
        }

        private static bool TryLoadJsonConfigurationFile(IConfigurationSource configurationSource, out IConfigurationSource jsonConfigurationSource)
        {
            try
            {
                // if environment variable is not set, look for default file name in the current directory
                var configurationFileName = configurationSource.GetString(ConfigurationKeys.ConfigurationFileName) ??
                                            configurationSource.GetString("DD_DOTNET_TRACER_CONFIG_FILE") ??
                                            Path.Combine(GetCurrentDirectory(), "datadog.json");

                if (string.Equals(Path.GetExtension(configurationFileName), ".JSON", StringComparison.OrdinalIgnoreCase) &&
                    File.Exists(configurationFileName))
                {
                    jsonConfigurationSource = JsonConfigurationSource.FromFile(configurationFileName);
                    return true;
                }
            }
            catch (Exception)
            {
                // Unable to load the JSON file from disk
                // The configuration manager should not depend on a logger being bootstrapped yet
                // so do not do anything
            }

            jsonConfigurationSource = default;
            return false;
        }

        private static string GetCurrentDirectory()
        {
            try
            {
                // Entering TryLoadHostingEnvironmentPath and accessing System.Web.dll
                // will immediately throw an exception in partial trust scenarios,
                // so surround this call by a try/catch block
                if (TryLoadHostingEnvironmentPath(out var hostingPath))
                {
                    return hostingPath;
                }
            }
            catch (Exception)
            {
                // The configuration manager should not depend on a logger being bootstrapped yet
                // so do not do anything
            }

            return System.Environment.CurrentDirectory;
        }

        private static bool TryLoadHostingEnvironmentPath(out string hostingPath)
        {
#if NETFRAMEWORK
            // on .NET Framework only, use application's root folder
            // as default path when looking for datadog.json
            if (System.Web.Hosting.HostingEnvironment.IsHosted)
            {
                hostingPath = System.Web.Hosting.HostingEnvironment.MapPath("~");
                return true;
            }

#endif
            hostingPath = default;
            return false;
        }
    }
}
