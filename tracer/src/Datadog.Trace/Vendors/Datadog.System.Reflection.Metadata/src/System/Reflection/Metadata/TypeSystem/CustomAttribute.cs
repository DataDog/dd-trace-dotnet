﻿// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.CustomAttribute
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

using System;
using Datadog.System.Reflection.Metadata.Ecma335;


#nullable enable
namespace Datadog.System.Reflection.Metadata
{
  public readonly struct CustomAttribute
  {

    #nullable disable
    private readonly MetadataReader _reader;
    private readonly uint _treatmentAndRowId;


    #nullable enable
    internal CustomAttribute(MetadataReader reader, uint treatmentAndRowId)
    {
      this._reader = reader;
      this._treatmentAndRowId = treatmentAndRowId;
    }

    private int RowId => (int) this._treatmentAndRowId & 16777215;

    private CustomAttributeHandle Handle => CustomAttributeHandle.FromRowId(this.RowId);

    private MethodDefTreatment Treatment => (MethodDefTreatment) (this._treatmentAndRowId >> 24);

    /// <summary>
    /// The constructor (<see cref="T:System.Reflection.Metadata.MethodDefinitionHandle" /> or <see cref="T:System.Reflection.Metadata.MemberReferenceHandle" />) of the custom attribute type.
    /// </summary>
    /// <remarks>
    /// Corresponds to Type field of CustomAttribute table in ECMA-335 Standard.
    /// </remarks>
    public EntityHandle Constructor => this._reader.CustomAttributeTable.GetConstructor(this.Handle);

    /// <summary>
    /// The handle of the metadata entity the attribute is applied to.
    /// </summary>
    /// <remarks>
    /// Corresponds to Parent field of CustomAttribute table in ECMA-335 Standard.
    /// </remarks>
    public EntityHandle Parent => this._reader.CustomAttributeTable.GetParent(this.Handle);

    /// <summary>The value of the attribute.</summary>
    /// <remarks>
    /// Corresponds to Value field of CustomAttribute table in ECMA-335 Standard.
    /// </remarks>
    public BlobHandle Value => this.Treatment == MethodDefTreatment.None ? this._reader.CustomAttributeTable.GetValue(this.Handle) : this.GetProjectedValue();

    /// <summary>Decodes the arguments encoded in the value blob.</summary>
    public CustomAttributeValue<TType> DecodeValue<TType>(
      ICustomAttributeTypeProvider<TType> provider)
    {
      return new CustomAttributeDecoder<TType>(provider, this._reader).DecodeValue(this.Constructor, this.Value);
    }

    private BlobHandle GetProjectedValue()
    {
      CustomAttributeValueTreatment attributeValueTreatment = this._reader.CalculateCustomAttributeValueTreatment(this.Handle);
      return attributeValueTreatment == CustomAttributeValueTreatment.None ? this._reader.CustomAttributeTable.GetValue(this.Handle) : this.GetProjectedValue(attributeValueTreatment);
    }

    private BlobHandle GetProjectedValue(CustomAttributeValueTreatment treatment)
    {
      BlobHandle.VirtualIndex virtualIndex;
      bool flag;
      switch (treatment)
      {
        case CustomAttributeValueTreatment.AttributeUsageAllowSingle:
          virtualIndex = BlobHandle.VirtualIndex.AttributeUsage_AllowSingle;
          flag = false;
          break;
        case CustomAttributeValueTreatment.AttributeUsageAllowMultiple:
          virtualIndex = BlobHandle.VirtualIndex.AttributeUsage_AllowMultiple;
          flag = false;
          break;
        case CustomAttributeValueTreatment.AttributeUsageVersionAttribute:
        case CustomAttributeValueTreatment.AttributeUsageDeprecatedAttribute:
          virtualIndex = BlobHandle.VirtualIndex.AttributeUsage_AllowMultiple;
          flag = true;
          break;
        default:
          return new BlobHandle();
      }
      BlobHandle handle = this._reader.CustomAttributeTable.GetValue(this.Handle);
      BlobReader blobReader = this._reader.GetBlobReader(handle);
      if (blobReader.Length != 8 || blobReader.ReadInt16() != (short) 1)
        return handle;
      AttributeTargets virtualValue = CustomAttribute.ProjectAttributeTargetValue(blobReader.ReadUInt32());
      if (flag)
        virtualValue |= AttributeTargets.Constructor | AttributeTargets.Property;
      return BlobHandle.FromVirtualIndex(virtualIndex, (ushort) virtualValue);
    }

    private static AttributeTargets ProjectAttributeTargetValue(uint rawValue)
    {
      if (rawValue == uint.MaxValue)
        return AttributeTargets.All;
      AttributeTargets attributeTargets = (AttributeTargets) 0;
      if (((int) rawValue & 1) != 0)
        attributeTargets |= AttributeTargets.Delegate;
      if (((int) rawValue & 2) != 0)
        attributeTargets |= AttributeTargets.Enum;
      if (((int) rawValue & 4) != 0)
        attributeTargets |= AttributeTargets.Event;
      if (((int) rawValue & 8) != 0)
        attributeTargets |= AttributeTargets.Field;
      if (((int) rawValue & 16) != 0)
        attributeTargets |= AttributeTargets.Interface;
      if (((int) rawValue & 64) != 0)
        attributeTargets |= AttributeTargets.Method;
      if (((int) rawValue & 128) != 0)
        attributeTargets |= AttributeTargets.Parameter;
      if (((int) rawValue & 256) != 0)
        attributeTargets |= AttributeTargets.Property;
      if (((int) rawValue & 512) != 0)
        attributeTargets |= AttributeTargets.Class;
      if (((int) rawValue & 1024) != 0)
        attributeTargets |= AttributeTargets.Struct;
      return attributeTargets;
    }
  }
}
