// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.EventDefinition
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

using System.Reflection;
using Datadog.System.Collections.Immutable;


#nullable enable
namespace Datadog.System.Reflection.Metadata
{
  public readonly struct EventDefinition
  {

    #nullable disable
    private readonly MetadataReader _reader;
    private readonly int _rowId;


    #nullable enable
    internal EventDefinition(MetadataReader reader, EventDefinitionHandle handle)
    {
      this._reader = reader;
      this._rowId = handle.RowId;
    }

    private EventDefinitionHandle Handle => EventDefinitionHandle.FromRowId(this._rowId);

    public StringHandle Name => this._reader.EventTable.GetName(this.Handle);

    public EventAttributes Attributes => this._reader.EventTable.GetFlags(this.Handle);

    public EntityHandle Type => this._reader.EventTable.GetEventType(this.Handle);

    public CustomAttributeHandleCollection GetCustomAttributes() => new CustomAttributeHandleCollection(this._reader, (EntityHandle) this.Handle);

    public EventAccessors GetAccessors()
    {
      int adderRowId = 0;
      int removerRowId = 0;
      int raiserRowId = 0;
      ImmutableArray<MethodDefinitionHandle>.Builder builder = (ImmutableArray<MethodDefinitionHandle>.Builder) null;
      ushort methodCount;
      int semanticMethodsForEvent = this._reader.MethodSemanticsTable.FindSemanticMethodsForEvent(this.Handle, out methodCount);
      for (ushort index = 0; (int) index < (int) methodCount; ++index)
      {
        int rowId = semanticMethodsForEvent + (int) index;
        switch (this._reader.MethodSemanticsTable.GetSemantics(rowId))
        {
          case MethodSemanticsAttributes.Other:
            if (builder == null)
              builder = ImmutableArray.CreateBuilder<MethodDefinitionHandle>();
            builder.Add(this._reader.MethodSemanticsTable.GetMethod(rowId));
            break;
          case MethodSemanticsAttributes.Adder:
            adderRowId = this._reader.MethodSemanticsTable.GetMethod(rowId).RowId;
            break;
          case MethodSemanticsAttributes.Remover:
            removerRowId = this._reader.MethodSemanticsTable.GetMethod(rowId).RowId;
            break;
          case MethodSemanticsAttributes.Raiser:
            raiserRowId = this._reader.MethodSemanticsTable.GetMethod(rowId).RowId;
            break;
        }
      }
      ImmutableArray<MethodDefinitionHandle> others = builder != null ? builder.ToImmutable() : ImmutableArray<MethodDefinitionHandle>.Empty;
      return new EventAccessors(adderRowId, removerRowId, raiserRowId, others);
    }
  }
}
