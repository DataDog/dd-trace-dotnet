using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Datadog.InstrumentedAssemblyVerification
{
    internal class PeVerifyVerification : IVerification
    {
        private readonly InstrumentationVerificationLogger _logger;
        private readonly string _assemblyLocation;

        private readonly List<(string excludeString, Func<string, string, bool> excludeFunction)> _errorsToExclude = new()
        {
            // TODO: consider using /ignore=@ responseFile
            ("Method does not exist.", (error, excludeString) => error.EndsWith(excludeString)),
            // Safe to ignore
            ("(Exception from HRESULT: 0x80131040)", (error, excludeString) => error.Contains(excludeString)),
            ("because the parent does not exist. Unable to resolve token.", (error, excludeString) => error.EndsWith(excludeString)),
            // todo: added because in CI we don't have the full message like the above message, we should find away to resolve it or move to ILVerify in NET Framework as well
            ("] Unable to resolve token.", (error, excludeString) => error.EndsWith(excludeString)),
            ("The located assembly's manifest definition does not match the assembly reference.", (error, excludeString) => error.EndsWith(excludeString)),
            ("[HRESULT 0x80070002] - The system cannot find the file specified.", (error, excludeString) => error.EndsWith(excludeString)),
            // We don't care much about it, maybe e will fix it later
            ("Method is not visible.", (error, excludeString) => error.EndsWith(excludeString)),
            // Sometimes method resolution pick a wrong overload and this lead to unexpected type (e.g. string instead of object)
            ("Unexpected type on the stack.", (error, excludeString) => error.EndsWith(excludeString)),
            // It's not a good practice to do that but we ain't to fail the verification because this. (happens for example in "Microsoft.Extensions.Logging.LoggerFactory::.ctor")
            ("Uninitialized this on entering a try block.", (error, excludeString) => error.EndsWith(excludeString)),
            // This error exist even if everything looks valid
            ("] Type load failed.", (error, excludeString) => error.EndsWith(excludeString)),
        };

        public PeVerifyVerification(string assemblyLocation, InstrumentationVerificationLogger logger)
        {
            _logger = logger;
            _assemblyLocation = assemblyLocation;
        }

        public List<string> Verify()
        {
            _logger.Info($"{nameof(PeVerifyVerification)} Verifying {_assemblyLocation} starts");

            var errors = new List<string>();
            var peVerifyLocation = GetPEVerifyLocation();

            if (!File.Exists(peVerifyLocation))
            {
                errors.Add($"{nameof(PeVerifyVerification)} PEVerify.exe could not be found. Skipping verifier.");
                return errors;
            }

            _logger.Info($"{nameof(PeVerifyVerification)} PeVerify.exe path is: {peVerifyLocation}");

            try
            {
                Process process = CreatePEVerifyProcess(_assemblyLocation, peVerifyLocation);
                process.Start();
                string processOutput = process.StandardOutput.ReadToEnd();
                string processErrorOutput = process.StandardError.ReadToEnd();
                var isExited = process.WaitForExit(10 * 1000);
                if (!isExited)
                {
                    _logger.Error($"{nameof(PeVerifyVerification)} The process did not end after 10 seconds");
                }

                string result = $"{nameof(PeVerifyVerification)} PEVerify Exit Code: {process.ExitCode}";

                if (process.ExitCode == 0)
                {
                    _logger.Info($"{nameof(PeVerifyVerification)} Verifying {_assemblyLocation} ended without errors");
                }
                else
                {
                    const string mdToken = "[mdToken=0x6000001]";
                    const string offset = "[offset 0x00000007]";
                    string allOutput = processOutput + Environment.NewLine + processErrorOutput;
                    // We want to get errors agnostics to the assembly path and tokens number so we do the follow:
                    // Take only errors
                    // Replace full path with only file name
                    // Write the error to log
                    // Remove tokens and offsets

                    if (!string.IsNullOrEmpty(allOutput))
                    {
                        _logger.Debug("PEVerify all errors:" + Environment.NewLine + allOutput);
                    }

                    var allErrors = allOutput.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries)
                                             .Where(line => line.StartsWith("[IL]: Error:"))
                                             .Select(line => line.Replace("[IL]: Error:", $"{nameof(PeVerifyVerification)} [IL]:"))
                                             .Select(line => line.Replace(_assemblyLocation, Path.GetFileName(_assemblyLocation)))
                                             .Select(line => RemoveWordIfExist(line, "[mdToken=0x", mdToken.Length))
                                             .Select(line => RemoveWordIfExist(line, "[offset 0x", offset.Length));

                    var errorsToReport = RemoveExcludedErrors(allErrors).ToList();
                    if (errorsToReport.Any())
                    {
                        errors.AddRange(errorsToReport);
                        _logger.Error($"{nameof(PeVerifyVerification)} {result}");
                        foreach (var error in errors)
                        {
                            _logger.Error(error);
                        }
                        _logger.Error($"{nameof(PeVerifyVerification)} Verifying {_assemblyLocation} ended with errors");
                    }
                    else
                    {
                        _logger.Info($"{nameof(PeVerifyVerification)} Verifying {_assemblyLocation} ended without errors");
                    }
                }
            }
            catch (Exception e)
            {
                _logger.Error($"{nameof(PeVerifyVerification)} Verifying {_assemblyLocation} ended with errors. {e.ToString()}");
                errors.Add(e.ToString());
            }

            return errors;
        }

        private static string RemoveWordIfExist(string line, string wordOrPartOf, int wordLength)
        {
            var indexOfToken = line.IndexOf(wordOrPartOf, StringComparison.InvariantCultureIgnoreCase);
            if (indexOfToken > -1)
            {
                return line.Remove(indexOfToken, wordLength);
            }
            else
            {
                return line;
            }
        }

        private IEnumerable<string> RemoveExcludedErrors(IEnumerable<string> errors)
        {
            return errors.Where(error => _errorsToExclude.All(tuple => !tuple.excludeFunction(error, tuple.excludeString)));
        }

        private static Process CreatePEVerifyProcess(string assemblyLocation, string peVerifyLocation)
        {
            var process = new Process();
            process.StartInfo.FileName = peVerifyLocation;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory;
            process.StartInfo.Arguments = "\"" + assemblyLocation + "\" /verbose /nologo /il";
            process.StartInfo.CreateNoWindow = true;
            return process;
        }

        private static string GetPEVerifyLocation()
        {
            string peVerifyLocation = string.Empty;
            foreach (string dir in PossibleWindowsSdkLocations.Values)
            {
                peVerifyLocation = Path.Combine(dir, "peverify.exe");

                if (File.Exists(peVerifyLocation))
                {
                    break;
                }
            }
            return peVerifyLocation;
        }

        private static readonly Dictionary<string, string> PossibleWindowsSdkLocations = new()
        {
            // Order: from newest ro oldest
            ["sdk_win10_48"] = @"C:\Program Files (x86)\Microsoft SDKs\Windows\v10.0A\bin\NETFX 4.8 Tools\x64",
            ["sdk_win10_472"] = @"C:\Program Files (x86)\Microsoft SDKs\Windows\v10.0A\bin\NETFX 4.7.2 Tools\x64",
            ["sdk_win10_471"] = @"C:\Program Files (x86)\Microsoft SDKs\Windows\v10.0A\bin\NETFX 4.7.1 Tools\x64",
            ["sdk_win10_47"] = @"C:\Program Files (x86)\Microsoft SDKs\Windows\v10.0A\bin\NETFX 4.7 Tools\x64",
            ["sdk_win10_462"] = @"C:\Program Files (x86)\Microsoft SDKs\Windows\v10.0A\bin\NETFX 4.6.2 Tools\x64",
            ["sdk_win10_461"] = @"C:\Program Files (x86)\Microsoft SDKs\Windows\v10.0A\bin\NETFX 4.6.1 Tools\x64",
            ["sdk_win10_46"] = @"C:\Program Files (x86)\Microsoft SDKs\Windows\v10.0A\bin\NETFX 4.6 Tools\x64",
        };

        private string CopyPeVerifyExeToDllDirectory(string peVerifyLocation)
        {
            try
            {
                _logger.Info($"{nameof(PeVerifyVerification)} PeVerify.exe path is: {peVerifyLocation}");
                var copyTo = Path.Combine(new FileInfo(_assemblyLocation).DirectoryName, Path.GetFileName(peVerifyLocation));
                if (!File.Exists(copyTo))
                {
                    File.Copy(peVerifyLocation, copyTo);
                }

                return copyTo;
            }
            catch (Exception e)
            {
                _logger.Error(e.ToString());
                return null;
            }
        }
    }
}
