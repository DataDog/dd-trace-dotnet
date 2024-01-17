﻿// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.Ecma335.NamespaceCache
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

using System.Collections.Generic;
using Datadog.System.Collections.Immutable;


#nullable enable
namespace Datadog.System.Reflection.Metadata.Ecma335
{
    internal sealed class NamespaceCache
  {

    #nullable disable
    private readonly MetadataReader _metadataReader;
    private readonly object _namespaceTableAndListLock = new object();
    private volatile Dictionary<NamespaceDefinitionHandle, NamespaceData> _namespaceTable;
    private NamespaceData _rootNamespace;
    private uint _virtualNamespaceCounter;


    #nullable enable
    internal NamespaceCache(MetadataReader reader) => this._metadataReader = reader;

    /// <summary>
    /// Returns whether the namespaceTable has been created. If it hasn't, calling a GetXXX method
    /// on this will probably have a very high amount of overhead.
    /// </summary>
    internal bool CacheIsRealized => this._namespaceTable != null;

    internal string GetFullName(NamespaceDefinitionHandle handle) => this.GetNamespaceData(handle).FullName;

    internal NamespaceData GetRootNamespace()
    {
      this.EnsureNamespaceTableIsPopulated();
      return this._rootNamespace;
    }

    internal NamespaceData GetNamespaceData(NamespaceDefinitionHandle handle)
    {
      this.EnsureNamespaceTableIsPopulated();
      NamespaceData namespaceData;
      if (!this._namespaceTable.TryGetValue(handle, out namespaceData))
        Throw.InvalidHandle();
      return namespaceData;
    }

    /// <summary>
    /// This will return a StringHandle for the simple name of a namespace Datadog.name at the given segment index.
    /// If no segment index is passed explicitly or the "segment" index is greater than or equal to the number
    /// of segments, then the last segment is used. "Segment" in this context refers to part of a namespace
    /// name between dots.
    /// 
    /// Example: Given a NamespaceDefinitionHandle to "System.Collections.Generic.Test" called 'handle':
    /// 
    ///   reader.GetString(GetSimpleName(handle)) == "Test"
    ///   reader.GetString(GetSimpleName(handle, 0)) == "System"
    ///   reader.GetString(GetSimpleName(handle, 1)) == "Collections"
    ///   reader.GetString(GetSimpleName(handle, 2)) == "Generic"
    ///   reader.GetString(GetSimpleName(handle, 3)) == "Test"
    ///   reader.GetString(GetSimpleName(handle, 1000)) == "Test"
    /// </summary>
    private StringHandle GetSimpleName(
      NamespaceDefinitionHandle fullNamespaceHandle,
      int segmentIndex = 2147483647)
    {
      fullNamespaceHandle.GetFullName();
      int num1 = fullNamespaceHandle.GetHeapOffset() - 1;
      for (int index = 0; index < segmentIndex; ++index)
      {
        int num2 = this._metadataReader.StringHeap.IndexOfRaw(num1 + 1, '.');
        if (num2 != -1)
          num1 = num2;
        else
          break;
      }
      return StringHandle.FromOffset(num1 + 1).WithDotTermination();
    }

    /// <summary>
    /// Two distinct namespace Datadog.handles represent the same namespace if their full names are the same. This
    /// method merges builders corresponding to such namespace Datadog.handles.
    /// </summary>
    private void PopulateNamespaceTable()
    {
      lock (this._namespaceTableAndListLock)
      {
        if (this._namespaceTable != null)
          return;
        Dictionary<NamespaceDefinitionHandle, NamespaceCache.NamespaceDataBuilder> table = new Dictionary<NamespaceDefinitionHandle, NamespaceCache.NamespaceDataBuilder>();
        NamespaceDefinitionHandle definitionHandle = NamespaceDefinitionHandle.FromFullNameOffset(0);
        table.Add(definitionHandle, new NamespaceCache.NamespaceDataBuilder(definitionHandle, definitionHandle.GetFullName(), string.Empty));
        this.PopulateTableWithTypeDefinitions(table);
        this.PopulateTableWithExportedTypes(table);
        Dictionary<string, NamespaceCache.NamespaceDataBuilder> stringTable;
        NamespaceCache.MergeDuplicateNamespaces(table, out stringTable);
        List<NamespaceCache.NamespaceDataBuilder> virtualNamespaces;
        this.ResolveParentChildRelationships(stringTable, out virtualNamespaces);
        Dictionary<NamespaceDefinitionHandle, NamespaceData> dictionary = new Dictionary<NamespaceDefinitionHandle, NamespaceData>();
        foreach (KeyValuePair<NamespaceDefinitionHandle, NamespaceCache.NamespaceDataBuilder> keyValuePair in table)
          dictionary.Add(keyValuePair.Key, keyValuePair.Value.Freeze());
        if (virtualNamespaces != null)
        {
          foreach (NamespaceCache.NamespaceDataBuilder namespaceDataBuilder in virtualNamespaces)
            dictionary.Add(namespaceDataBuilder.Handle, namespaceDataBuilder.Freeze());
        }
        this._rootNamespace = dictionary[definitionHandle];
        this._namespaceTable = dictionary;
      }
    }


    #nullable disable
    /// <summary>
    /// This will take 'table' and merge all of the NamespaceData instances that point to the same
    /// namespace. It has to create 'stringTable' as an intermediate dictionary, so it will hand it
    /// back to the caller should the caller want to use it.
    /// </summary>
    private static void MergeDuplicateNamespaces(
      Dictionary<NamespaceDefinitionHandle, NamespaceCache.NamespaceDataBuilder> table,
      out Dictionary<string, NamespaceCache.NamespaceDataBuilder> stringTable)
    {
      Dictionary<string, NamespaceCache.NamespaceDataBuilder> dictionary = new Dictionary<string, NamespaceCache.NamespaceDataBuilder>();
      List<KeyValuePair<NamespaceDefinitionHandle, NamespaceCache.NamespaceDataBuilder>> keyValuePairList = (List<KeyValuePair<NamespaceDefinitionHandle, NamespaceCache.NamespaceDataBuilder>>) null;
      foreach (KeyValuePair<NamespaceDefinitionHandle, NamespaceCache.NamespaceDataBuilder> keyValuePair in table)
      {
        NamespaceCache.NamespaceDataBuilder namespaceDataBuilder = keyValuePair.Value;
        NamespaceCache.NamespaceDataBuilder other;
        if (dictionary.TryGetValue(namespaceDataBuilder.FullName, out other))
        {
          namespaceDataBuilder.MergeInto(other);
          if (keyValuePairList == null)
            keyValuePairList = new List<KeyValuePair<NamespaceDefinitionHandle, NamespaceCache.NamespaceDataBuilder>>();
          keyValuePairList.Add(new KeyValuePair<NamespaceDefinitionHandle, NamespaceCache.NamespaceDataBuilder>(keyValuePair.Key, other));
        }
        else
          dictionary.Add(namespaceDataBuilder.FullName, namespaceDataBuilder);
      }
      if (keyValuePairList != null)
      {
        foreach (KeyValuePair<NamespaceDefinitionHandle, NamespaceCache.NamespaceDataBuilder> keyValuePair in keyValuePairList)
          table[keyValuePair.Key] = keyValuePair.Value;
      }
      stringTable = dictionary;
    }

    /// <summary>
    /// Creates a NamespaceDataBuilder instance that contains a synthesized NamespaceDefinitionHandle,
    /// as well as the name provided.
    /// </summary>
    private NamespaceCache.NamespaceDataBuilder SynthesizeNamespaceData(
      string fullName,
      NamespaceDefinitionHandle realChild)
    {
      int segmentIndex = 0;
      foreach (char ch in fullName)
      {
        if (ch == '.')
          ++segmentIndex;
      }
      StringHandle simpleName = this.GetSimpleName(realChild, segmentIndex);
      return new NamespaceCache.NamespaceDataBuilder(NamespaceDefinitionHandle.FromVirtualIndex(++this._virtualNamespaceCounter), simpleName, fullName);
    }

    /// <summary>
    /// Quick convenience method that handles linking together child + parent
    /// </summary>
    private static void LinkChildDataToParentData(
      NamespaceCache.NamespaceDataBuilder child,
      NamespaceCache.NamespaceDataBuilder parent)
    {
      child.Parent = parent.Handle;
      parent.Namespaces.Add(child.Handle);
    }

    /// <summary>
    /// Links a child to its parent namespace. If the parent namespace Datadog.doesn't exist, this will create a
    /// virtual one. This will automatically link any virtual namespaces it creates up to its parents.
    /// </summary>
    private void LinkChildToParentNamespace(
      Dictionary<string, NamespaceCache.NamespaceDataBuilder> existingNamespaces,
      NamespaceCache.NamespaceDataBuilder realChild,
      ref List<NamespaceCache.NamespaceDataBuilder> virtualNamespaces)
    {
      string fullName = realChild.FullName;
      NamespaceCache.NamespaceDataBuilder child = realChild;
      NamespaceCache.NamespaceDataBuilder parent1;
      while (true)
      {
        int length = fullName.LastIndexOf('.');
        string str;
        if (length == -1)
        {
          if (fullName.Length != 0)
            str = string.Empty;
          else
            break;
        }
        else
          str = fullName.Substring(0, length);
        if (!existingNamespaces.TryGetValue(str, out parent1))
        {
          if (virtualNamespaces != null)
          {
            foreach (NamespaceCache.NamespaceDataBuilder parent2 in virtualNamespaces)
            {
              if (parent2.FullName == str)
              {
                NamespaceCache.LinkChildDataToParentData(child, parent2);
                return;
              }
            }
          }
          else
            virtualNamespaces = new List<NamespaceCache.NamespaceDataBuilder>();
          NamespaceCache.NamespaceDataBuilder parent3 = this.SynthesizeNamespaceData(str, realChild.Handle);
          NamespaceCache.LinkChildDataToParentData(child, parent3);
          virtualNamespaces.Add(parent3);
          fullName = parent3.FullName;
          child = parent3;
        }
        else
          goto label_6;
      }
      return;
label_6:
      NamespaceCache.LinkChildDataToParentData(child, parent1);
    }

    /// <summary>
    /// This will link all parents/children in the given namespaces dictionary up to each other.
    /// 
    /// In some cases, we need to synthesize namespaces that do not have any type definitions or forwarders
    /// of their own, but do have child namespaces. These are returned via the virtualNamespaces out
    /// parameter.
    /// </summary>
    private void ResolveParentChildRelationships(
      Dictionary<string, NamespaceCache.NamespaceDataBuilder> namespaces,
      out List<NamespaceCache.NamespaceDataBuilder> virtualNamespaces)
    {
      virtualNamespaces = (List<NamespaceCache.NamespaceDataBuilder>) null;
      foreach (KeyValuePair<string, NamespaceCache.NamespaceDataBuilder> keyValuePair in namespaces)
        this.LinkChildToParentNamespace(namespaces, keyValuePair.Value, ref virtualNamespaces);
    }

    /// <summary>
    /// Loops through all type definitions in metadata, adding them to the given table
    /// </summary>
    private void PopulateTableWithTypeDefinitions(
      Dictionary<NamespaceDefinitionHandle, NamespaceCache.NamespaceDataBuilder> table)
    {
      foreach (TypeDefinitionHandle typeDefinition in this._metadataReader.TypeDefinitions)
      {
        if (!this._metadataReader.GetTypeDefinition(typeDefinition).Attributes.IsNested())
        {
          NamespaceDefinitionHandle namespaceDefinition = this._metadataReader.TypeDefTable.GetNamespaceDefinition(typeDefinition);
          NamespaceCache.NamespaceDataBuilder namespaceDataBuilder;
          if (table.TryGetValue(namespaceDefinition, out namespaceDataBuilder))
          {
            namespaceDataBuilder.TypeDefinitions.Add(typeDefinition);
          }
          else
          {
            StringHandle simpleName = this.GetSimpleName(namespaceDefinition);
            string fullName = this._metadataReader.GetString(namespaceDefinition);
            table.Add(namespaceDefinition, new NamespaceCache.NamespaceDataBuilder(namespaceDefinition, simpleName, fullName)
            {
              TypeDefinitions = {
                typeDefinition
              }
            });
          }
        }
      }
    }

    /// <summary>
    /// Loops through all type forwarders in metadata, adding them to the given table
    /// </summary>
    private void PopulateTableWithExportedTypes(
      Dictionary<NamespaceDefinitionHandle, NamespaceCache.NamespaceDataBuilder> table)
    {
      foreach (ExportedTypeHandle exportedType1 in this._metadataReader.ExportedTypes)
      {
        ExportedType exportedType2 = this._metadataReader.GetExportedType(exportedType1);
        if (exportedType2.Implementation.Kind != HandleKind.ExportedType)
        {
          NamespaceDefinitionHandle namespaceDefinition = exportedType2.NamespaceDefinition;
          NamespaceCache.NamespaceDataBuilder namespaceDataBuilder;
          if (table.TryGetValue(namespaceDefinition, out namespaceDataBuilder))
          {
            namespaceDataBuilder.ExportedTypes.Add(exportedType1);
          }
          else
          {
            StringHandle simpleName = this.GetSimpleName(namespaceDefinition);
            string fullName = this._metadataReader.GetString(namespaceDefinition);
            table.Add(namespaceDefinition, new NamespaceCache.NamespaceDataBuilder(namespaceDefinition, simpleName, fullName)
            {
              ExportedTypes = {
                exportedType1
              }
            });
          }
        }
      }
    }

    /// <summary>If the namespace Datadog.table doesn't exist, populates it!</summary>
    private void EnsureNamespaceTableIsPopulated()
    {
      if (this._namespaceTable != null)
        return;
      this.PopulateNamespaceTable();
    }

    /// <summary>
    /// An intermediate class used to build NamespaceData instances. This was created because we wanted to
    /// use ImmutableArrays in NamespaceData, but having ArrayBuilders and ImmutableArrays that served the
    /// same purpose in NamespaceData got ugly. With the current design of how we create our Namespace
    /// dictionary, this needs to be a class because we have a many-to-one mapping between NamespaceHandles
    /// and NamespaceData. So, the pointer semantics must be preserved.
    /// 
    /// This class assumes that the builders will not be modified in any way after the first call to
    /// Freeze().
    /// </summary>
    private sealed class NamespaceDataBuilder
    {
      public readonly NamespaceDefinitionHandle Handle;
      public readonly StringHandle Name;
      public readonly string FullName;
      public NamespaceDefinitionHandle Parent;
      public ImmutableArray<NamespaceDefinitionHandle>.Builder Namespaces;
      public ImmutableArray<TypeDefinitionHandle>.Builder TypeDefinitions;
      public ImmutableArray<ExportedTypeHandle>.Builder ExportedTypes;
      private NamespaceData _frozen;

      public NamespaceDataBuilder(
        NamespaceDefinitionHandle handle,
        StringHandle name,
        string fullName)
      {
        this.Handle = handle;
        this.Name = name;
        this.FullName = fullName;
        this.Namespaces = ImmutableArray.CreateBuilder<NamespaceDefinitionHandle>();
        this.TypeDefinitions = ImmutableArray.CreateBuilder<TypeDefinitionHandle>();
        this.ExportedTypes = ImmutableArray.CreateBuilder<ExportedTypeHandle>();
      }

      /// <summary>
      /// Returns a NamespaceData that represents this NamespaceDataBuilder instance. After calling
      /// this method, it is an error to use any methods or fields except Freeze() on the target
      /// NamespaceDataBuilder.
      /// </summary>
      public NamespaceData Freeze()
      {
        if (this._frozen == null)
        {
          ImmutableArray<NamespaceDefinitionHandle> immutable1 = this.Namespaces.ToImmutable();
          this.Namespaces = (ImmutableArray<NamespaceDefinitionHandle>.Builder) null;
          ImmutableArray<TypeDefinitionHandle> immutable2 = this.TypeDefinitions.ToImmutable();
          this.TypeDefinitions = (ImmutableArray<TypeDefinitionHandle>.Builder) null;
          ImmutableArray<ExportedTypeHandle> immutable3 = this.ExportedTypes.ToImmutable();
          this.ExportedTypes = (ImmutableArray<ExportedTypeHandle>.Builder) null;
          this._frozen = new NamespaceData(this.Name, this.FullName, this.Parent, immutable1, immutable2, immutable3);
        }
        return this._frozen;
      }

      public void MergeInto(NamespaceCache.NamespaceDataBuilder other)
      {
        this.Parent = new NamespaceDefinitionHandle();
        other.Namespaces.AddRange(this.Namespaces);
        other.TypeDefinitions.AddRange(this.TypeDefinitions);
        other.ExportedTypes.AddRange(this.ExportedTypes);
      }
    }
  }
}
