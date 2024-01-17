﻿// Decompiled with JetBrains decompiler
// Type: System.Reflection.Internal.Hash
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

namespace Datadog.System.Reflection.Internal
{
  internal static class Hash
  {
    /// <summary>
    /// The offset bias value used in the FNV-1a algorithm
    /// See http://en.wikipedia.org/wiki/Fowler%E2%80%93Noll%E2%80%93Vo_hash_function
    /// </summary>
    internal const int FnvOffsetBias = -2128831035;
    /// <summary>
    /// The generative factor used in the FNV-1a algorithm
    /// See http://en.wikipedia.org/wiki/Fowler%E2%80%93Noll%E2%80%93Vo_hash_function
    /// </summary>
    internal const int FnvPrime = 16777619;

    internal static int Combine(int newKey, int currentKey) => currentKey * -1521134295 + newKey;

    internal static int Combine(uint newKey, int currentKey) => currentKey * -1521134295 + (int) newKey;

    internal static int Combine(bool newKeyPart, int currentKey) => Hash.Combine(currentKey, newKeyPart ? 1 : 0);

    internal static int GetFNVHashCode(ReadOnlySpan<byte> data)
    {
      int fnvHashCode = -2128831035;
      for (int index = 0; index < data.Length; ++index)
        fnvHashCode = (fnvHashCode ^ (int) data[index]) * 16777619;
      return fnvHashCode;
    }
  }
}
