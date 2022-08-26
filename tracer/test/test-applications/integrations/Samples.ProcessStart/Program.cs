using System.Diagnostics;
using System.Security;
using System.Threading.Tasks;

namespace Samples.ProcessStart
{
    internal static class Program
    {
        private static async Task Main()
        {
            try
            {
                Process.Start("cmd.exe");
            }
            catch { }

            try
            {
                Process.Start("nonexisting.exe", "arg1");
            }
            catch { }

            try
            {
                Process.Start("nonexisting.exe", "arg1", "user", new SecureString(), "domain");
            }
            catch { }

            try
            {
                Process.Start("nonexisting.exe", "user", new SecureString(), "domain");
            }
            catch { }

            try
            {
                Process process = new Process();
                process.StartInfo = new ProcessStartInfo("nonexisting.exe", "args");
                process.StartInfo.UseShellExecute = false;
                process.Start();
            }
            catch { }
        }
    }
}
