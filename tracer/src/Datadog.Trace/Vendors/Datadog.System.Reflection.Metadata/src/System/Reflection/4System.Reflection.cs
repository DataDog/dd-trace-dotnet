﻿// Decompiled with JetBrains decompiler
// Type: System.Reflection.AssemblyHashAlgorithm
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

namespace Datadog.System.Reflection
{
  /// <summary>
  /// Specifies all the hash algorithms used for hashing assembly files and for generating the strong name.
  /// </summary>
  public enum AssemblyHashAlgorithm
  {
    /// <summary>
    /// A mask indicating that there is no hash algorithm. If you specify None for a multi-module assembly, the common language runtime defaults to the SHA1 algorithm, since multi-module assemblies need to generate a hash.
    /// </summary>
    None = 0,
    /// <summary>
    /// Retrieves the MD5 message-digest algorithm. MD5 was developed by Rivest in 1991. It is basically MD4 with safety-belts and while it is slightly slower than MD4, it helps provide more security. The algorithm consists of four distinct rounds, which has a slightly different design from that of MD4. Message-digest size, as well as padding requirements, remain the same.
    /// </summary>
    MD5 = 32771, // 0x00008003
    /// <summary>
    /// Retrieves a revision of the Secure Hash Algorithm that corrects an unpublished flaw in SHA.
    /// </summary>
    Sha1 = 32772, // 0x00008004
    /// <summary>
    /// Retrieves a version of the Secure Hash Algorithm with a hash size of 256 bits.
    /// </summary>
    Sha256 = 32780, // 0x0000800C
    /// <summary>
    /// Retrieves a version of the Secure Hash Algorithm with a hash size of 384 bits.
    /// </summary>
    Sha384 = 32781, // 0x0000800D
    /// <summary>
    /// Retrieves a version of the Secure Hash Algorithm with a hash size of 512 bits.
    /// </summary>
    Sha512 = 32782, // 0x0000800E
  }
}
