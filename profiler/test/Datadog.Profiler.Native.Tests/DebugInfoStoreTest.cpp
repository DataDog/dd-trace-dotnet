// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "gtest/gtest.h"

#include "DebugInfoStore.h"
#include "ProfilerMockedInterface.h"

#include "shared/src/native-src/dd_filesystem.hpp"
#include "shared/src/native-src/string.h"

using ::testing::Return;
using ::testing::ReturnRef;

#ifdef _WINDOWS

// This test validates that DebugInfoStore::ParseModuleDebugInfo can parse symbols
// from a .NET Framework PDB (Windows PDB format) using DbgHelp.
// .NET Framework (net48) produces Windows PDB files that cannot be parsed as Portable PDBs.
TEST(DebugInfoStoreTest, ParseModuleDebugInfo_NetFramework)
{
    // Try to find the Computer01 net48 PDB file in common build output location
    fs::path pdbPath =
        fs::current_path() / ".." / ".." / ".." / "profiler" / "src" / "Demos" / "Samples.Computer01" / "net48" / "Samples.Computer01.pdb";

    fs::path modulePath;
    bool foundPdb = false;

    std::error_code ec;
    if (fs::exists(pdbPath, ec))
    {
        // The DLL should be in the same directory
        modulePath = pdbPath.parent_path() / "Samples.Computer01.exe";
        if (!fs::exists(modulePath, ec))
        {
            GTEST_SKIP() << "Samples.Computer01.pdb (net48) not found. Build the Samples.Computer01 project for net48 first.";
            return;
        }
    }

    // Parse the PDB using DebugInfoStore
    ModuleDebugInfo moduleInfo;

    // Create a minimal mock configuration
    auto [configuration, mockConfiguration] = CreateConfiguration();
    EXPECT_CALL(mockConfiguration, IsDebugInfoEnabled()).WillRepeatedly(Return(true));

    // Note: We don't need a real ICorProfilerInfo4 for this test since we're using
    // the public ParseModuleDebugInfo method that doesn't use it
    DebugInfoStore debugInfoStore(nullptr, configuration.get());

    // Parse the PDB
    debugInfoStore.ParseModuleDebugInfo(pdbPath.string(), modulePath.string(), moduleInfo);

    // Validate that symbols were loaded
    // For .NET Framework PDB (Windows PDB format), the LoadingState should be Windows
    ASSERT_EQ(moduleInfo.LoadingState, SymbolLoadingState::Windows)
        << "Expected Windows PDB format for .NET Framework compilation (net48)";

    // Validate that debug info was populated
    ASSERT_FALSE(moduleInfo.Files.empty()) << "Expected at least one source file in debug info";
    ASSERT_FALSE(moduleInfo.RvaToDebugInfo.empty()) << "Expected RVA to debug info mapping for Windows PDB";

    // The first entry should be the empty string placeholder
    ASSERT_EQ(moduleInfo.Files[0], DebugInfoStore::NoFileFound);

    // Validate that we have actual source files loaded
    ASSERT_GT(moduleInfo.Files.size(), 1) << "Expected more than just the placeholder entry";

    // At least one file should reference a .cs source file
    bool foundCsFile = false;
    for (const auto& file : moduleInfo.Files)
    {
        if (file.find(".cs") != std::string::npos)
        {
            foundCsFile = true;
            break;
        }
    }
    ASSERT_TRUE(foundCsFile) << "Expected at least one .cs source file in debug info";

    // Validate that we have RVA mappings
    ASSERT_GT(moduleInfo.RvaToDebugInfo.size(), 0) << "Expected RVA to debug info mappings";

    // Validate that at least some RVA entries have valid line numbers
    bool foundValidLineNumber = false;
    for (const auto& [rva, debugInfo] : moduleInfo.RvaToDebugInfo)
    {
        if (debugInfo.StartLine > 0 && debugInfo.File != DebugInfoStore::NoFileFound)
        {
            foundValidLineNumber = true;
            break;
        }
    }
    ASSERT_TRUE(foundValidLineNumber) << "Expected at least one RVA with valid line number and source file";
}

#endif // _WINDOWS


// Additional test to verify that .NET Core/5+ PDBs are Portable format
// This ensures we're testing the right distinction
TEST(DebugInfoStoreTest, ParseModuleDebugInfo_NetCorePortable)
{
    // Try to find the BuggyBits net10.0 PDB file (as an example of Portable PDB)
    fs::path pdbPath = fs::current_path() / ".." / ".." / ".." / "profiler" / "src" / "Demos" / "Samples.BuggyBits" / "net10.0" / "Samples.BuggyBits.pdb";
    fs::path modulePath;
    bool foundPdb = false;

    std::error_code ec;
    if (fs::exists(pdbPath, ec))
    {
        modulePath = pdbPath.parent_path() / "Samples.BuggyBits.exe";
        if (!fs::exists(modulePath, ec))
        {
            GTEST_SKIP() << "Samples.BuggyBits.pdb (net10.0) not found. This is expected if net10.0 is not compiled.";
            return;
        }
    }

    ModuleDebugInfo moduleInfo;

    auto [configuration, mockConfiguration] = CreateConfiguration();
    EXPECT_CALL(mockConfiguration, IsDebugInfoEnabled()).WillRepeatedly(Return(true));

    DebugInfoStore debugInfoStore(nullptr, configuration.get());
    debugInfoStore.ParseModuleDebugInfo(pdbPath.string(), modulePath.string(), moduleInfo);

    // For .NET Core/5+ PDB (Portable PDB format), the LoadingState should be Portable
    ASSERT_EQ(moduleInfo.LoadingState, SymbolLoadingState::Portable)
        << "Expected Portable PDB format for .NET Core/5+ compilation (net10.0)";

    // Portable PDBs use RID-based lookup
    ASSERT_FALSE(moduleInfo.RidToDebugInfo.empty()) << "Expected RID to debug info mapping for Portable PDB";
    ASSERT_FALSE(moduleInfo.Files.empty()) << "Expected source files in debug info";
}
