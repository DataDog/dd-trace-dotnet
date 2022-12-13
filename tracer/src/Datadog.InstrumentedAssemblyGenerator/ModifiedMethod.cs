using System;
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
    /// <summary>
    /// Create a new <see cref="ModifiedMethod"/>
    /// </summary>
    /// <param name="methodDef">Method definition after instrumentation</param>
    /// <param name="modulePath">Full path of the modified method module</param>
    /// <remarks>We use it names instead of <see cref="MethodDef"/> or <see cref="MDToken"/> because we have hear two type systems
    /// 1. To generate the method and the assembly we use dnlib
    /// 2. To decompile the code we use ICsharpCode.Decompiler
    /// So although the method has token and theoretically we can resole it directly, we can't do that when the method has a generic context
    /// in this case we must have the context owner which is the method or type that we just search for</remarks>
    internal ModifiedMethod(MethodDef methodDef, string modulePath)
    {
        try
        {
            ModulePath = modulePath;
            Instructions = string.Join(Environment.NewLine, methodDef.Body.Instructions);
            MethodAndArgumentsName = $"{methodDef.Name}({string.Join(",", methodDef.Parameters.Where(p => !p.IsHiddenThisParameter).Select(p => p.Type.FullName))})";
            TypeFullName = methodDef.DeclaringType.FullName;
            DecompiledCode = new Lazy<string>(Decompile);
            IsValid = true;
        }
        catch (Exception e)
        {
            IsValid = false;
            Console.WriteLine(e);
            ModulePath = null;
            Instructions = null;
        }
    }

    /// <summary>
    /// 
    /// </summary>
    public string MethodAndArgumentsName { get; }

    /// <summary>
    /// 
    /// </summary>
    public string TypeFullName { get; }

    /// <summary>
    /// If the current instance is a valid ModifiedMethod object
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// The module of the method
    /// </summary>
    public string ModulePath { get; }

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
            ITypeDefinition typeDefinition = ILSpyHelper.FindType(decompiler, TypeFullName);
            if (typeDefinition == null)
            {
                Console.WriteLine($"{nameof(Decompile)}: Can't find type {TypeFullName}");
                return null;
            }

            var ilSpyMethod = ILSpyHelper.FindMethod(typeDefinition, MethodAndArgumentsName);
            if (ilSpyMethod?.MetadataToken == null)
            {
                Console.WriteLine($"Can't find method token for {MethodAndArgumentsName}");
                return null;
            }

            return decompiler.DecompileAsString(ilSpyMethod.MetadataToken);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return null;
        }
    }

}
