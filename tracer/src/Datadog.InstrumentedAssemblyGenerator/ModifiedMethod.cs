using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using dnlib.DotNet;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.TypeSystem;

namespace Datadog.InstrumentedAssemblyGenerator;

/// <summary>
/// Represent a modified method i.e. method that was replaced by a new instrumented method
/// </summary>
public class ModifiedMethod
{
    private readonly int _methodNameIndex;

    /// <summary>
    /// Create a new <see cref="ModifiedMethod"/>
    /// </summary>
    /// <param name="instrumentedMethod">Method that was instrumented</param>
    /// <param name="methodDef">Method definition after instrumentation</param>
    /// <param name="context">Module context</param>
    /// <param name="module">The module of the method</param>
    /// <param name="instrumentedModulesFolder">Output folder of instrumented modules</param>
    internal ModifiedMethod(InstrumentedMethod instrumentedMethod, MethodDef methodDef, InstrumentedAssemblyGeneratorContext context, ModuleDefMD module, string instrumentedModulesFolder)
    {
        try
        {
            ModulePath = Path.Combine(instrumentedModulesFolder, module.FullName);
            Instructions = string.Join(Environment.NewLine, methodDef.Body.Instructions);
            Arguments = instrumentedMethod.ArgumentsNames.Select(n => n.GetTypeSig(module, context).FullName).ToList();
            FullName = $"{instrumentedMethod}({string.Join(",", Arguments)})";
            _methodNameIndex = FullName.Substring(0, FullName.LastIndexOf('(')).LastIndexOf(".", StringComparison.InvariantCulture);
            DecompiledCode = new Lazy<string>(Decompile);
            IsValid = true;
        }
        catch (Exception e)
        {
            IsValid = false;
            Console.WriteLine(e);
            ModulePath = null;
            Instructions = null;
            Arguments = null;
            FullName = null;
            _methodNameIndex = -1;
        }
    }

    /// <summary>
    /// If the current instance is a valid ModifiedMethod object
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// The module of the method
    /// </summary>
    public string ModulePath { get; }

    /// <summary>
    /// the full name of the method
    /// </summary>
    public string FullName { get; }

    /// <summary>
    /// The name of the method parent type
    /// </summary>
    public string TypeName
    {
        get
        {
            if (!IsValid)
            {
                return null;
            }

            if (MethodName.StartsWith("ctor") || MethodName.StartsWith("cctor"))
            {
                return FullName.Substring(0, _methodNameIndex - 1);
            }
            else
            {
                return FullName.Substring(0, _methodNameIndex);
            }
        }
    }

    /// <summary>
    /// The name of the method including method arguments
    /// </summary>
    public string MethodName
    {
        get
        {
            if (!IsValid)
            {
                return null;
            }

            return FullName.Substring(_methodNameIndex + 1);
        }
    }

    /// <summary>
    /// The full name arguments of the method
    /// </summary>
    public List<string> Arguments { get; }

    /// <summary>
    /// The IL instructions of the method
    /// </summary>
    public string Instructions { get; }

    /// <summary>
    /// Get the decompiled code of the method
    /// </summary>
    public Lazy<string> DecompiledCode { get; }

    private string Decompile()
    {
        if (!IsValid)
        {
            return null;
        }

        try
        {
            var settings = new DecompilerSettings();
            var decompiler = new CSharpDecompiler(ModulePath, settings);
            var typeName = new FullTypeName(TypeName);
            ITypeDefinition typeInfo = decompiler.TypeSystem.MainModule.Compilation.FindType(typeName).GetDefinition();
            if (typeInfo == null)
            {
                Console.WriteLine($"{nameof(ModifiedMethod.Decompile)}: Can't find type {TypeName}");
                return null;
            }

            // we can't do this replace in ILSpy method because '`' is valid sign for generic type
            var copyOfMethodAndParameterName = MethodName.Replace("!", "`");
            var tokenOfFirstMethod = typeInfo.Methods.SingleOrDefault(m => ILSpyHelper.GetMethodAndParametersName(m.Name, m.Parameters) == copyOfMethodAndParameterName) ??
                                     typeInfo.GetConstructors().SingleOrDefault(m => ILSpyHelper.GetMethodAndParametersName(m.Name.Substring(1), m.Parameters) == copyOfMethodAndParameterName);
            if (tokenOfFirstMethod?.MetadataToken == null)
            {
                Console.WriteLine($"Can't find method token for {MethodName}");
                return null;
            }

            return decompiler.DecompileAsString(tokenOfFirstMethod.MetadataToken);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return null;
        }
    }

}
