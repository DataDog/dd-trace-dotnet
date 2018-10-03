using System;
using System.Linq;

namespace Datadog.Trace.ClrProfiler
{
    /// <summary>
    /// Static class for initializing the module.
    /// </summary>
    public static class ModuleInitializer
    {
        /// <summary>
        /// Initializes the module.
        /// </summary>
        public static void Initialize()
        {
            var ad = AppDomain.CurrentDomain;
            foreach (var asm in ad.GetAssemblies())
            {
                IntegrationLoader.ProcessAssembly(asm);
            }

            ad.AssemblyLoad += Ad_AssemblyLoad;
            NativeMethods.AddIntegrations(@"
[
  {
    ""name"": ""StackExchangeRedis"",
    ""method_replacements"": [
      {
        ""caller"": {
            ""assembly"": ""StackExchange.Redis""
        },
        ""target"": {
          ""assembly"": ""StackExchange.Redis"",
          ""type"": ""StackExchange.Redis.ConnectionMultiplexer"",
          ""method"": ""ExecuteSyncImpl""
        },
        ""wrapper"": {
          ""assembly"": ""Datadog.Trace.ClrProfiler.Managed, Version=0.3.2.0, Culture=neutral, PublicKeyToken=def86d061d0d2eeb"",
          ""type"": ""Datadog.Trace.ClrProfiler.Integrations.StackExchange.Redis.ConnectionMultiplexer"",
          ""method"": ""ExecuteSyncImpl"",
          ""signature"": ""10 01 04 1E 00 1C 1C 1C 1C""
        }
            },
      {
                ""caller"": {
                    ""assembly"": ""StackExchange.Redis""
                },
        ""target"": {
                    ""assembly"": ""StackExchange.Redis"",
          ""type"": ""StackExchange.Redis.ConnectionMultiplexer"",
          ""method"": ""ExecuteAsyncImpl""
        },
        ""wrapper"": {
                    ""assembly"": ""Datadog.Trace.ClrProfiler.Managed, Version=0.3.2.0, Culture=neutral, PublicKeyToken=def86d061d0d2eeb"",
          ""type"": ""Datadog.Trace.ClrProfiler.Integrations.StackExchange.Redis.ConnectionMultiplexer"",
          ""method"": ""ExecuteAsyncImpl"",
          ""signature"": ""10 01 05 1C 1C 1C 1C 1C 1C""
        }
      },
      {
        ""caller"": {
          ""assembly"": ""StackExchange.Redis""
        },
        ""target"": {
          ""assembly"": ""StackExchange.Redis"",
          ""type"": ""StackExchange.Redis.RedisBase"",
          ""method"": ""ExecuteAsync""
        },
        ""wrapper"": {
          ""assembly"": ""Datadog.Trace.ClrProfiler.Managed, Version=0.3.2.0, Culture=neutral, PublicKeyToken=def86d061d0d2eeb"",
          ""type"": ""Datadog.Trace.ClrProfiler.Integrations.StackExchange.Redis.RedisBatch"",
          ""method"": ""ExecuteAsync"",
          ""signature"": ""10 01 04 1C 1C 1C 1C 1C""
        }
      }
    ]
  }
]
            ");
        }

        private static void Ad_AssemblyLoad(object sender, AssemblyLoadEventArgs args)
        {
            try
            {
                IntegrationLoader.ProcessAssembly(args.LoadedAssembly);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{ex.Message}");
            }
        }
    }
}
