// <copyright file="SymmetricAlgorithmAspect.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#if NETFRAMEWORK

#nullable enable

using System;
using System.Security.Cryptography;
using Datadog.Trace.ClrProfiler;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.CryptographyAlgorithm;
using Datadog.Trace.Iast.Dataflow;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Iast.Aspects;

/// <summary> SymmetricAlgorithm class aspects </summary>
[AspectClass("mscorlib")]
[global::System.ComponentModel.Browsable(false)]
[global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
public class SymmetricAlgorithmAspect
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(SymmetricAlgorithmAspect));

    private static void ProcessCipherClassCreation(SymmetricAlgorithm target)
    {
        try
        {
            var scope = SymmetricAlgorithmIntegrationCommon.CreateScope(target);
            scope?.Dispose();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in SymmetricAlgorithmAspect.");
        }
    }

    /// <summary>
    /// DESCryptoServiceProvider constructor
    /// </summary>
    /// <returns>main DESCryptoServiceProvider instance</returns>
    [AspectCtorReplace("System.Security.Cryptography.DESCryptoServiceProvider::.ctor()")]
    public static DESCryptoServiceProvider InitDES()
    {
        var target = new DESCryptoServiceProvider();
        try
        {
            ProcessCipherClassCreation(target);
        }
        catch (Exception ex)
        {
            IastModule.Log.Error(ex, $"Error invoking {nameof(SymmetricAlgorithmAspect)}.{nameof(InitDES)}");
        }

        return target;
    }

    /// <summary>
    /// RC2CryptoServiceProvider constructor
    /// </summary>
    /// <returns>main RC2CryptoServiceProvider instance</returns>
    [AspectCtorReplace("System.Security.Cryptography.RC2CryptoServiceProvider::.ctor()")]
    public static RC2CryptoServiceProvider InitRC2()
    {
        var target = new RC2CryptoServiceProvider();
        try
        {
            ProcessCipherClassCreation(target);
        }
        catch (Exception ex)
        {
            IastModule.Log.Error(ex, $"Error invoking {nameof(SymmetricAlgorithmAspect)}.{nameof(InitRC2)}");
        }

        return target;
    }

    /// <summary>
    /// TripleDESCryptoServiceProvider constructor
    /// </summary>
    /// <returns>main TripleDESCryptoServiceProvider instance</returns>
    [AspectCtorReplace("System.Security.Cryptography.TripleDESCryptoServiceProvider::.ctor()")]
    public static TripleDESCryptoServiceProvider InitTripleDES()
    {
        var target = new TripleDESCryptoServiceProvider();
        try
        {
            ProcessCipherClassCreation(target);
        }
        catch (Exception ex)
        {
            IastModule.Log.Error(ex, $"Error invoking {nameof(SymmetricAlgorithmAspect)}.{nameof(InitTripleDES)}");
        }

        return target;
    }

    /// <summary>
    /// RijndaelManaged constructor
    /// </summary>
    /// <returns>main RijndaelManaged instance</returns>
    [AspectCtorReplace("System.Security.Cryptography.RijndaelManaged::.ctor()")]
    public static RijndaelManaged InitRijndaelManaged()
    {
        var target = new RijndaelManaged();
        try
        {
            ProcessCipherClassCreation(target);
        }
        catch (Exception ex)
        {
            IastModule.Log.Error(ex, $"Error invoking {nameof(SymmetricAlgorithmAspect)}.{nameof(InitRijndaelManaged)}");
        }

        return target;
    }

    /// <summary>
    /// AesCryptoServiceProvider constructor
    /// </summary>
    /// <returns>main AesCryptoServiceProvider instance</returns>
    [AspectCtorReplace("System.Security.Cryptography.AesCryptoServiceProvider::.ctor()")]
    public static AesCryptoServiceProvider InitAesCryptoServiceProvider()
    {
        var target = new AesCryptoServiceProvider();
        try
        {
            ProcessCipherClassCreation(target);
        }
        catch (Exception ex)
        {
            IastModule.Log.Error(ex, $"Error invoking {nameof(SymmetricAlgorithmAspect)}.{nameof(InitAesCryptoServiceProvider)}");
        }

        return target;
    }

    /// <summary>
    /// Instrument the creation of all the SymmetricAlgorithm classes defined for .net framework https://learn.microsoft.com/es-es/dotnet/api/system.security.cryptography.symmetricalgorithm?view=netframework-4.8.1
    /// </summary>
    /// <param name="target">SymmetricAlgorithm instance</param>
    /// <returns>main SymmetricAlgorithm instance</returns>
    [AspectMethodInsertAfter($"System.Security.Cryptography.DES::Create()")]
    [AspectMethodInsertAfter($"System.Security.Cryptography.DES::Create({ClrNames.String})")]
    [AspectMethodInsertAfter($"System.Security.Cryptography.RC2::Create()")]
    [AspectMethodInsertAfter($"System.Security.Cryptography.RC2::Create({ClrNames.String})")]
    [AspectMethodInsertAfter($"System.Security.Cryptography.TripleDES::Create()")]
    [AspectMethodInsertAfter($"System.Security.Cryptography.TripleDES::Create({ClrNames.String})")]
    [AspectMethodInsertAfter($"System.Security.Cryptography.Rijndael::Create()")]
    [AspectMethodInsertAfter($"System.Security.Cryptography.Rijndael::Create({ClrNames.String})")]
    [AspectMethodInsertAfter($"System.Security.Cryptography.Aes::Create()")]
    [AspectMethodInsertAfter($"System.Security.Cryptography.Aes::Create({ClrNames.String})")]
    public static SymmetricAlgorithm Create(SymmetricAlgorithm target)
    {
        try
        {
            ProcessCipherClassCreation(target);
            return target;
        }
        catch (global::System.Exception ex)
        {
            IastModule.Log.Error(ex, $"Error invoking {nameof(SymmetricAlgorithmAspect)}.{nameof(Create)}");
            return target;
        }
    }
}
#endif
