using System.IO;

namespace Datadog.Trace.Configuration
{
    /// <summary>
    /// Contains logic to create instances of <see cref="IConfigurationSource"/>.
    /// </summary>
    public static class ConfigurationSource
    {
        /// <summary>
        /// Creates a <see cref="IConfigurationSource"/> by combining environment variables,
        /// AppSettings where available, and a local datadog.json file, if present.
        /// </summary>
        /// <returns>A new <see cref="IConfigurationSource"/> instance.</returns>
        public static IConfigurationSource CreateDefaultSource()
        {
            // env > AppSettings > datadog.json
            var compositeSource = new CompositeConfigurationSource
                                  {
                                      new EnvironmentConfigurationSource()
                                  };

            var fallbacksSource = new FallbacksConfigurationSource(compositeSource);

#if !NETSTANDARD2_0
            try
            {
                // on .NET Framework only, also read from app.config/web.config
                compositeSource.Add(new NameValueConfigurationSource(System.Configuration.ConfigurationManager.AppSettings));
            }
            catch
            {
                // TODO: can we log yet?
            }
#endif

            string currentDirectory = null;

#if !NETSTANDARD2_0
            try
            {
                // on .NET Framework only, use application's root folder
                // as default path when looking for datadog.json
                if (System.Web.Hosting.HostingEnvironment.IsHosted)
                {
                    currentDirectory = System.Web.Hosting.HostingEnvironment.MapPath("~") ?? string.Empty;
                }
            }
            catch
            {
                // TODO: can we log yet?
            }
#endif

            try
            {
                currentDirectory ??= System.Environment.CurrentDirectory;

                var configurationFileName = fallbacksSource.GetString(ConfigurationKeys.ConfigurationFileName) ??
                                            // if path to config file is not set, look in default path
                                            Path.Combine(currentDirectory, "datadog.json");

                if (File.Exists(configurationFileName))
                {
                    compositeSource.Add(JsonConfigurationSource.FromFile(configurationFileName));
                }
            }
            catch
            {
                // TODO: can we log yet?
            }

            return fallbacksSource;
        }
    }
}
