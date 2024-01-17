﻿// Decompiled with JetBrains decompiler
// Type: System.Reflection.PortableExecutable.PEStreamOptions
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

using System;

namespace Datadog.System.Reflection.PortableExecutable
{
  [Flags]
  public enum PEStreamOptions
  {
    /// <summary>
    /// By default the stream is disposed when <see cref="T:System.Reflection.PortableExecutable.PEReader" /> is disposed and sections of the PE image are read lazily.
    /// </summary>
    Default = 0,
    /// <summary>
    /// Keep the stream open when the <see cref="T:System.Reflection.PortableExecutable.PEReader" /> is disposed.
    /// </summary>
    LeaveOpen = 1,
    /// <summary>Reads metadata section into memory right away.</summary>
    /// <remarks>
    /// Reading from other sections of the file is not allowed (<see cref="T:System.InvalidOperationException" /> is thrown by the <see cref="T:System.Reflection.PortableExecutable.PEReader" />).
    /// The underlying file may be closed and even deleted after <see cref="T:System.Reflection.PortableExecutable.PEReader" /> is constructed.
    /// 
    /// <see cref="T:System.Reflection.PortableExecutable.PEReader" /> closes the stream automatically by the time the constructor returns unless <see cref="F:System.Reflection.PortableExecutable.PEStreamOptions.LeaveOpen" /> is specified.
    /// </remarks>
    PrefetchMetadata = 2,
    /// <summary>Reads the entire image into memory right away.</summary>
    /// <remarks>
    /// <see cref="T:System.Reflection.PortableExecutable.PEReader" /> closes the stream automatically by the time the constructor returns unless <see cref="F:System.Reflection.PortableExecutable.PEStreamOptions.LeaveOpen" /> is specified.
    /// </remarks>
    PrefetchEntireImage = 4,
    /// <summary>
    /// Indicates that the underlying PE image has been loaded into memory by the OS loader.
    /// </summary>
    IsLoadedImage = 8,
  }
}
