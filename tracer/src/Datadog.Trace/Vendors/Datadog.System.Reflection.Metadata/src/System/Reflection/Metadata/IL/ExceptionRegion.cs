// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.ExceptionRegion
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

namespace Datadog.System.Reflection.Metadata
{
  public readonly struct ExceptionRegion
  {
    private readonly ExceptionRegionKind _kind;
    private readonly int _tryOffset;
    private readonly int _tryLength;
    private readonly int _handlerOffset;
    private readonly int _handlerLength;
    private readonly int _classTokenOrFilterOffset;

    internal ExceptionRegion(
      ExceptionRegionKind kind,
      int tryOffset,
      int tryLength,
      int handlerOffset,
      int handlerLength,
      int classTokenOrFilterOffset)
    {
      this._kind = kind;
      this._tryOffset = tryOffset;
      this._tryLength = tryLength;
      this._handlerOffset = handlerOffset;
      this._handlerLength = handlerLength;
      this._classTokenOrFilterOffset = classTokenOrFilterOffset;
    }

    public ExceptionRegionKind Kind => this._kind;

    /// <summary>Start IL offset of the try block.</summary>
    public int TryOffset => this._tryOffset;

    /// <summary>Length in bytes of try block.</summary>
    public int TryLength => this._tryLength;

    /// <summary>Start IL offset of the exception handler.</summary>
    public int HandlerOffset => this._handlerOffset;

    /// <summary>Length in bytes of the exception handler.</summary>
    public int HandlerLength => this._handlerLength;

    /// <summary>
    /// IL offset of the start of the filter block, or -1 if the region is not a filter.
    /// </summary>
    public int FilterOffset => this.Kind != ExceptionRegionKind.Filter ? -1 : this._classTokenOrFilterOffset;

    /// <summary>
    /// Returns a TypeRef, TypeDef, or TypeSpec handle if the region represents a catch, nil token otherwise.
    /// </summary>
    public EntityHandle CatchType => this.Kind != ExceptionRegionKind.Catch ? new EntityHandle() : new EntityHandle((uint) this._classTokenOrFilterOffset);
  }
}
