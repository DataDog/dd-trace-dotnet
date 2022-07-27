using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
#pragma warning disable CS1591

namespace Datadog.InstrumentedAssemblyVerification
{
    internal enum Verification
    {
        ILSpy,
        PrepareMethod,
        PEVerify,
        ILVerify
    }
    
    public class VerificationOutcome
    {
        public VerificationOutcome(bool isValid, string error)
        {
            IsValid = isValid;
            FailureReason = error;
        }
        public bool IsValid { get; }
        public string FailureReason { get; }
    }

    public class VerificationsRunner
    {
        private readonly string _instrumentedModulePath;
        private readonly string _originalModulePath;
        private readonly List<string> _methods;
        private readonly IEnumerable<Verification> _verifications;

        public VerificationsRunner(string instrumentedModulePath, 
                                   string originalModulePath, 
                                   List<string> methods)
        {
            _instrumentedModulePath = instrumentedModulePath;
            _originalModulePath = originalModulePath;
            _methods = methods;
            _verifications = new List<Verification> { Verification.ILSpy, Verification.PrepareMethod, Verification.ILVerify, Verification.PEVerify };
        }

        public VerificationOutcome Run()
        {
            using var logger = new InstrumentationVerificationLogger(_instrumentedModulePath);
            var errors = new List<string>();
            foreach (var verification in _verifications)
            {
                try
                {
                    switch (verification)
                    {
                        case Verification.ILSpy:
                            errors.AddRange(VerifyOriginalAndInstrumentedAndReturnDiff(
                                                new IlSpyDecompilationVerification(_originalModulePath, _methods, logger),
                                                new IlSpyDecompilationVerification(_instrumentedModulePath, _methods, logger)));
                            break;
                        case Verification.PrepareMethod:
                            errors.AddRange(VerifyOriginalAndInstrumentedAndReturnDiff(
                                                new PrepareMethodVerification(_originalModulePath, _methods, logger),
                                                new PrepareMethodVerification(_instrumentedModulePath, _methods, logger)));
                            break;
                        case Verification.PEVerify:
                            if (AssemblyUtils.IsCoreClr())
                            {
                                logger.Info(nameof(PeVerifyVerification) + ": verify .NET Framework assembly only");
                                break;
                            }
                            errors.AddRange(VerifyOriginalAndInstrumentedAndReturnDiff(
                                                new PeVerifyVerification(_originalModulePath, logger),
                                                new PeVerifyVerification(_instrumentedModulePath, logger)));
                            break;
                        case Verification.ILVerify:
                            if (!AssemblyUtils.IsCoreClr())
                            {
                                logger.Warn(nameof(ILVerifyVerification) + ": We can verify .NET Framework assembly but we must run from a .netcore app");
                                break;
                            }
                            errors.AddRange(VerifyOriginalAndInstrumentedAndReturnDiff(
                                                new ILVerifyVerification(_originalModulePath, _methods, logger),
                                                new ILVerifyVerification(_instrumentedModulePath, _methods, logger)));
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }
                catch (Exception e)
                {
                    logger.Error($"Failed to verify '{Path.GetFileName(_instrumentedModulePath)}' with {verification}: {e.Message}");
                }
            }
            return new VerificationOutcome(
                errors.Count == 0,
                string.Join(Environment.NewLine, errors));
        }

        /// <summary>
        /// Some of the verifications tools we use (and especially ILVerify and PEVerify) tend to report many false-positive.
        /// To reduce the noise and confusion this could cause, we run the verifications on the original assemblies as well,
        /// and remove all the errors this yields from the verification results of the instrumented assemblies.
        /// This ensures that the errors we get are only the ones related to our metadata changes and bytecode instrumentation.
        /// </summary>
        private IEnumerable<string> VerifyOriginalAndInstrumentedAndReturnDiff(IVerification original, IVerification instrumented)
        {
            var originalModuleResult = original.Verify();
            var instrumentedModuleResult = instrumented.Verify();
            return instrumentedModuleResult.Except(originalModuleResult);
        }
    }
}
