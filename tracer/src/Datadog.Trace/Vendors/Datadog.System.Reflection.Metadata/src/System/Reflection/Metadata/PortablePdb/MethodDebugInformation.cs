// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.MethodDebugInformation
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml


#nullable enable
namespace Datadog.System.Reflection.Metadata
{
  /// <summary>
  /// Debug information associated with a method definition. Stored in debug metadata.
  /// </summary>
  /// <remarks>
  /// See https://github.com/dotnet/runtime/blob/main/src/libraries/System.Reflection.Metadata/specs/PortablePdb-Metadata.md#methoddebuginformation-table-0x31.
  /// </remarks>
  public readonly struct MethodDebugInformation
  {

    #nullable disable
    private readonly MetadataReader _reader;
    private readonly int _rowId;


    #nullable enable
    internal MethodDebugInformation(MetadataReader reader, MethodDebugInformationHandle handle)
    {
      this._reader = reader;
      this._rowId = handle.RowId;
    }

    private MethodDebugInformationHandle Handle => MethodDebugInformationHandle.FromRowId(this._rowId);

    /// <summary>
    /// Returns a blob encoding sequence points, or nil if the method doesn't have sequence points.
    /// Use <see cref="M:System.Reflection.Metadata.MethodDebugInformation.GetSequencePoints" /> to decode the blob.
    /// </summary>
    public BlobHandle SequencePointsBlob => this._reader.MethodDebugInformationTable.GetSequencePoints(this.Handle);

    /// <summary>
    /// Handle of the single document containing all sequence points of the method,
    /// or nil if the method doesn't have sequence points or spans multiple documents.
    /// </summary>
    public DocumentHandle Document => this._reader.MethodDebugInformationTable.GetDocument(this.Handle);

    /// <summary>
    /// Returns local signature handle, or nil if the method doesn't define any local variables.
    /// </summary>
    public StandaloneSignatureHandle LocalSignature => this.SequencePointsBlob.IsNil ? new StandaloneSignatureHandle() : StandaloneSignatureHandle.FromRowId(this._reader.GetBlobReader(this.SequencePointsBlob).ReadCompressedInteger());

    /// <summary>
    /// Returns a collection of sequence points decoded from <see cref="P:System.Reflection.Metadata.MethodDebugInformation.SequencePointsBlob" />.
    /// </summary>
    public SequencePointCollection GetSequencePoints() => new SequencePointCollection(this._reader.BlobHeap.GetMemoryBlock(this.SequencePointsBlob), this.Document);

    /// <summary>
    /// If the method is a MoveNext method of a state machine returns the kickoff method of the state machine, otherwise returns nil handle.
    /// </summary>
    public MethodDefinitionHandle GetStateMachineKickoffMethod() => this._reader.StateMachineMethodTable.FindKickoffMethod(this._rowId);
  }
}
