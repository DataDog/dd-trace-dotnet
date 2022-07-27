using System;
using System.Collections.Generic;
using System.Linq;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.CSharp.Syntax;
using ICSharpCode.Decompiler.IL;
using ICSharpCode.Decompiler.TypeSystem;

namespace Datadog.InstrumentedAssemblyVerification
{
    /// <summary>
    /// Verifies a specific method's IL by making sure that it can be successfully decompiled by ILSpy,
    /// and that the resulting decompiled syntax tree does not contain any errors. 
    /// </summary>
    internal class IlSpyDecompilationVerification : IVerification
    {
        private readonly InstrumentationVerificationLogger _logger;
        private readonly List<string> _methods;
        private readonly string _peFile;

        private readonly List<(string toExclude, string inMethod)> _errorsToExclude = new()
        {
            // It can be an issue but so far we have only encountered this in false alarm so we don't want to fail on this error
            // for example, when calling void __DDVoidMethodType__::__ddvoidmethodcall__ from ctor
            (": Stack underflow*/", null),
            // Specific for Serilog tests, there is no reason for this error there.
            ("Expected O, but got I4", "Serilog.LoggerConfiguration::CreateLogger()")
        };

        public IlSpyDecompilationVerification(string module, List<string> methods, InstrumentationVerificationLogger logger)
        {
            _peFile = module;
            _methods = methods;
            _logger = logger;
        }

        public List<string> Verify()
        {
            _logger.Info($"{nameof(IlSpyDecompilationVerification)} Verifying {_peFile} starts");

            var errors = new List<string>();
            var settings = new DecompilerSettings();
            var decompiler = new CSharpDecompiler(_peFile, settings);
            foreach (string method in _methods)
            {
                try
                {
                    int methodNameIndex = method.Substring(0, method.LastIndexOf('(')).LastIndexOf(".", StringComparison.InvariantCulture);
                    string methodAndParametersName = method.Substring(methodNameIndex + 1);
                    FullTypeName typeName;
                    if (methodAndParametersName.StartsWith("ctor") || methodAndParametersName.StartsWith("cctor"))
                    {
                        typeName = new FullTypeName(method.Substring(0, methodNameIndex - 1));
                    }
                    else
                    {
                        typeName = new FullTypeName(method.Substring(0, methodNameIndex));
                    }
                    ITypeDefinition typeInfo = decompiler.TypeSystem.MainModule.Compilation.FindType(typeName).GetDefinition();
                    if (typeInfo == null)
                    {
                        string error = $"{nameof(IlSpyDecompilationVerification)} Type {typeName} not found in assembly {decompiler.TypeSystem.MainModule.AssemblyName}";
                        _logger.Warn(error);
                        continue;
                    }

                    // we can't do this replace in ILSpy method because '`' is valid sign for generic type
                    var copyOfMethodAndParameterName = methodAndParametersName.Replace("!", "`");
                    var tokenOfFirstMethod = typeInfo.Methods.SingleOrDefault(m => GetMethodAndParametersName(m.Name, m.Parameters) == copyOfMethodAndParameterName) ??
                                             typeInfo.GetConstructors().SingleOrDefault(m => GetMethodAndParametersName(m.Name.Substring(1), m.Parameters) == copyOfMethodAndParameterName);
                    if (tokenOfFirstMethod?.MetadataToken == null)
                    {
                        string error = $"{nameof(IlSpyDecompilationVerification)} Method {methodAndParametersName} not found in type {typeInfo.FullTypeName}";
                        _logger.Warn(error);
                        continue;
                    }

                    var decompiled = decompiler.Decompile(tokenOfFirstMethod.MetadataToken);
                    if (decompiled == null)
                    {
                        string error = $"{nameof(IlSpyDecompilationVerification)} Compilation of {tokenOfFirstMethod.FullName} failed";
                        errors.Add(error);
                        continue;
                    }

                    errors.AddRange(GetDecompilationIlErrors(decompiled, methodAndParametersName, tokenOfFirstMethod));
                }
                catch (Exception e)
                {
                    _logger.Error($"{nameof(IlSpyDecompilationVerification)} {e.ToString()}");
                }
            }

            if (errors.Count == 0)
            {
                _logger.Info($"{nameof(IlSpyDecompilationVerification)} Verifying {_peFile} ended without errors");
            }
            else
            {
                foreach (string error in errors)
                {
                    _logger.Error(error);
                }
            }

            return errors;
        }

        private string GetMethodAndParametersName(string name, IReadOnlyList<IParameter> parameters)
        {
            return name + $"({string.Join(",", parameters.Select(p => SanitizeParameterTypeName(p.Type.ReflectionName)))})";
        }

        private string SanitizeParameterTypeName(string typeName)
        {
            return typeName.Replace("[[", "<").Replace("]]", ">").Replace("[", "").Replace("]", "").Replace("+", "/");
        }

        private List<string> GetDecompilationIlErrors(SyntaxTree decompiledMethod, string methodAndParametersName, IMethod method)
        {
            var errors = new List<string>();
            var copyOfMethodAndParameterName = methodAndParametersName.Replace("!", "`");
            var ilFunction = decompiledMethod.DescendantNodes().OfType<EntityDeclaration>().
                                              Select(m => m.Annotations.OfType<ILFunction>().FirstOrDefault()).
                                              FirstOrDefault(function => function != null &&
                                                                         GetMethodAndParametersName(function.Name, function.Parameters) == copyOfMethodAndParameterName);


            string where = $"{method.DeclaringType?.FullName}::{methodAndParametersName}, in assembly: '{method.ParentModule.PEFile?.FileName}'";
            if (ilFunction?.Warnings?.Any() == true)
            {
                errors.AddRange(ilFunction.Warnings.
                                           Select(what => $"{nameof(IlSpyDecompilationVerification)} {what} when decompiling {where}")
                                          .Where(warning => _errorsToExclude.All(tuple => !warning.Contains(tuple.toExclude) || tuple.inMethod != null && !warning.Contains(tuple.inMethod))));
            }

            foreach (var errorNode in decompiledMethod.DescendantNodes().OfType<ErrorExpression>())
            {
                var comment = errorNode.DescendantNodes().FirstOrDefault(n => n is Comment);
                string error = $"{nameof(IlSpyDecompilationVerification)} {comment} when decompiling {where}";
                if (_errorsToExclude.Any(tuple => error.Contains(tuple.toExclude) &&
                                                  (tuple.inMethod == null || error.Contains(tuple.inMethod))))
                {
                    _logger.Warn(error);
                    continue;
                }

                errors.Add(error);
            }
            return errors;
        }
    }
}
