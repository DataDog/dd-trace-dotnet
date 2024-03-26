// Copyright (c) 2019 Aaron R Robinson

// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:

// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.

// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

#include <cstdio>
#include <cstdint>
#include <PPDBReader.hpp>

template<typename T>
void PrintRow(const T &)
{
    ::printf("Row type needs to be defined");
}

template<>
void PrintRow(const std::vector<uint32_t> &r)
{
    for (auto i : r)
        ::printf("0x%08x ", i);

    ::printf("\n");
}

void PrintGuid(const GUID &g)
{
    ::printf("{%08x-%04x-%04x-%02x%02x-%02x%02x%02x%02x%02x%02x}",
        g.Data1, g.Data2, g.Data3,
        g.Data4[0], g.Data4[1], g.Data4[2], g.Data4[3], g.Data4[4], g.Data4[5], g.Data4[6], g.Data4[7]);
}

template<>
void PrintRow(const PPDB::DocumentTableReader::Row &r)
{
    ::printf("Doc: %s\n", r.Name.c_str());
    ::printf("\tHash Algorithm: ");
    PrintGuid(r.HashAlgorithm);
    ::printf("\n");
    ::printf("\tLanguage: ");
    PrintGuid(r.Language);
    ::printf("\n");
}

template<>
void PrintRow(const PPDB::MethodDebugInformationTableReader::Row &r)
{
    ::printf("Debug: Doc Idx: %zu\n\tSequence Points:\n", r.InitialDocument);

    for (const auto &s : r.Points)
    {
        if (s.StartLine == 0xfeefee || s.EndLine == 0xfeefee)
        {
            ::printf("\t\t%u [Hidden]\n", s.ILOffset);
        }
        else
        {
            ::printf("\t\t%u %u %u %u %u %zu\n", s.ILOffset, s.StartLine, s.StartColumn, s.EndLine, s.EndColumn, s.DocumentIndex);
        }
    }
}

template<>
void PrintRow(const PPDB::ImportScopeTableReader::Row &r)
{
    ::printf("Import: Parent Idx: %zu\n\tImports:\n", r.ParentIndex);

    for (const auto &s : r.Imports)
    {
        switch (s.Kind)
        {
        default:
            continue;
        case PPDB::ImportKind::ImportFromNamespace:
            ::printf("\t\t%s\n", s.TargetNamespace.c_str());
            break;
        case PPDB::ImportKind::ImportFromNamespaceInAssembly:
            ::printf("\t\t%zu %s\n", s.TargetAssembly, s.TargetNamespace.c_str());
            break;
        case PPDB::ImportKind::ImportFromTargetType:
            ::printf("\t\t0x%08x\n", s.TargetType);
            break;
        case PPDB::ImportKind::ImportFromNamespaceWithAlias:
            ::printf("\t\t%s as %s\n", s.Alias.c_str(), s.TargetNamespace.c_str());
            break;
        case PPDB::ImportKind::ImportAssemblyAlias:
            ::printf("\t\t%s as %zu\n", s.Alias.c_str(), s.TargetAssembly);
            break;
        case PPDB::ImportKind::DefineAssemblyAlias:
            ::printf("\t\t%s = %zu\n", s.Alias.c_str(), s.TargetAssembly);
            break;
        case PPDB::ImportKind::DefineNamespaceAlias:
            ::printf("\t\t%s = %s\n", s.Alias.c_str(), s.TargetNamespace.c_str());
            break;
        case PPDB::ImportKind::DefineNamespaceAliasFromAssembly:
            ::printf("\t\t%s = %zu %s\n", s.Alias.c_str(), s.TargetAssembly, s.TargetNamespace.c_str());
            break;
        case PPDB::ImportKind::DefineTargetTypeAlias:
            ::printf("\t\t%s = 0x%08x\n", s.Alias.c_str(), s.TargetType);
            break;
        }
    }
}

template<>
void PrintRow(const PPDB::StateMachineMethodTableReader::Row &r)
{
    ::printf("StateMachine: %zu %zu\n", r.MoveNextMethodIndex, r.KickoffMethodIndex);
}

template<>
void PrintRow(const PPDB::LocalVariableTableReader::Row &r)
{
    ::printf("LocalVar: 0x%04x %u %s\n", (uint32_t)r.Attr, r.SlotIndex, r.Name.c_str());
}

template<>
void PrintRow(const PPDB::LocalConstantTableReader::Row &r)
{
    ::printf("LocalConst: %s\n", r.Name.c_str());
    ::printf("\t%u 0x%08x 0x%08x %zu\n", (uint32_t)r.Signature.Type, r.Signature.TypeToken, r.Signature.CustomModToken, r.Signature.RawValue.size());
}

template<>
void PrintRow(const PPDB::LocalScopeTableReader::Row &r)
{
    ::printf("LocalScope: %zu %zu %zu %zu %u %u\n", r.MethodIndex, r.ImportScopeIndex, r.VariableListIndex, r.ConstantListIndex, r.StartOffset, r.Length);
}

template<>
void PrintRow(const PPDB::ModuleTableReader::Row &r)
{
    ::printf("Mod: Name: %s ", r.Name.c_str());
    ::printf("MVID: ");
    PrintGuid(r.Mvid);
    ::printf("\n");
}

template<>
void PrintRow(const PPDB::CustomDebugInformationTableReader::Row &r)
{
    ::printf("Custom: Parent: %u\n", (uint32_t)r.Parent);
    ::printf("\tKind: ");
    PrintGuid(r.Kind);
    ::printf("\n");
    ::printf("\tData: %zu bytes\n", r.Value.size());
}

template<typename T>
void PrintTable(PPDB::MetadataStreamReader *m)
{
    auto table = m->GetTableReader<T>();
    if (table == nullptr)
    {
        ::printf("empty\n");
    }
    else
    {
        for (size_t i = 1; i <= table->RowCount(); ++i)
        {
            table->SetRow(i);
            std::vector<uint32_t> row;
            table->NextRow(row);
            PrintRow(row);

            table->SetRow(i);
            typename T::Row rowT;
            table->NextRow(rowT);
            PrintRow(rowT);
        }
    }

    ::printf("\n");
}

int main(int ac, char **av)
{
    if (ac != 2)
    {
        ::fprintf(stderr, "Supply Portable PDB to analyze\n");
        return EXIT_FAILURE;
    }

    try
    {
        auto r = PPDB::PortablePdbReader::CreateReader(av[1]);

        auto p = r->GetNamedEntry<PPDB::PdbStreamReader>();
        auto m = r->GetNamedEntry<PPDB::MetadataStreamReader>();
        auto g = r->GetNamedEntry<PPDB::GuidHeapReader>();
        auto s = r->GetNamedEntry<PPDB::StringsHeapReader>();
        auto u = r->GetNamedEntry<PPDB::UserStringsHeapReader>();
        auto b = r->GetNamedEntry<PPDB::BlobHeapReader>();

        PrintTable<PPDB::ModuleTableReader>(m.get());
        PrintTable<PPDB::DocumentTableReader>(m.get());
        PrintTable<PPDB::MethodDebugInformationTableReader>(m.get());
        PrintTable<PPDB::LocalVariableTableReader>(m.get());
        PrintTable<PPDB::LocalConstantTableReader>(m.get());
        PrintTable<PPDB::LocalScopeTableReader>(m.get());
        PrintTable<PPDB::ImportScopeTableReader>(m.get());
        PrintTable<PPDB::StateMachineMethodTableReader>(m.get());
        PrintTable<PPDB::CustomDebugInformationTableReader>(m.get());
    }
    catch (const PPDB::Exception &e)
    {
        ::fprintf(stderr, "Exception: %u (Name '%s', Table '%d')\n", (uint32_t)e.Error, e.Name.c_str(), (int32_t)e.Table);
        return EXIT_FAILURE;
    }

    return EXIT_SUCCESS;
}

