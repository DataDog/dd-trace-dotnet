#include "pch.h"

#include <filesystem>

#include "../../src/Datadog.Trace.ClrProfiler.Native/util.h"

using namespace trace;

TEST(UtilTest, AppendToPathAddsMissingDirectorySeparator) {
  auto directoryPath = std::filesystem::temp_directory_path() / "Directory";
  auto filename = "testfile"_W;

  auto expected = directoryPath / filename;
  auto result = AppendToPath(directoryPath.wstring(), filename);
  EXPECT_STREQ(expected.c_str(), result.c_str());
}

TEST(UtilTest, AppendToPathDoesNotAddDuplicateDirectorySeparator) {
  auto directoryPath = std::filesystem::temp_directory_path() / "Directory" / "";
  auto filename = "testfile"_W;

  auto expected = directoryPath / filename;
  auto result = AppendToPath(directoryPath.wstring(), filename);
  EXPECT_STREQ(expected.c_str(), result.c_str());
}