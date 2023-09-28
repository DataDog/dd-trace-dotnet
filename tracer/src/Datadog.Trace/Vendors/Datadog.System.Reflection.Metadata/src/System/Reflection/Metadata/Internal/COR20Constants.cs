// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.Ecma335.COR20Constants
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml


#nullable enable
namespace Datadog.System.Reflection.Metadata.Ecma335
{
  internal static class COR20Constants
  {
    internal const int SizeOfCorHeader = 72;
    internal const uint COR20MetadataSignature = 1112167234;
    internal const int MinimumSizeofMetadataHeader = 16;
    internal const int SizeofStorageHeader = 4;
    internal const int MinimumSizeofStreamHeader = 8;
    internal const string StringStreamName = "#Strings";
    internal const string BlobStreamName = "#Blob";
    internal const string GUIDStreamName = "#GUID";
    internal const string UserStringStreamName = "#US";
    internal const string CompressedMetadataTableStreamName = "#~";
    internal const string UncompressedMetadataTableStreamName = "#-";
    internal const string MinimalDeltaMetadataTableStreamName = "#JTD";
    internal const string StandalonePdbStreamName = "#Pdb";
    internal const int LargeStreamHeapSize = 4096;
  }
}
