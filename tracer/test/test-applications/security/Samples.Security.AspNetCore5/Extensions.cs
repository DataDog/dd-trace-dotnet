using System;
using System.Configuration;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Configuration;

namespace Samples.Security
{
    public static class Extensions
    {
        public static string GetDefaultConnectionString(this IConfiguration configuration)
        {
            return configuration.GetConnectionString("DefaultConnection") ?? $"Data Source={System.IO.Path.Combine("Data", "app.db")}";
        }

        public static bool ShouldUseSqlLite(this IConfiguration configuration)
        {
            // sql lite provider doesn't seem to be working on centos / alpine / arm64 but works in wsl/ubuntu 20... 
            var useSqlLiteDefault = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            return configuration.GetValue("UseSqlite", useSqlLiteDefault);
        }
    }
}
