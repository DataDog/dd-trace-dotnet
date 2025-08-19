#include "pch.h"

#include "../../../shared/src/native-src/string.h"

using namespace shared;

#define ASSERT_EQ_TOSTR(x) EXPECT_EQ(ToString(WStr(x)), x);
#define ASSERT_EQ_TOWSTR(x) EXPECT_EQ(ToWSTRING(x), WStr(x));

TEST(StringTests, ToString)
{
    ASSERT_EQ_TOSTR("Hello World");
    ASSERT_EQ_TOSTR("C:\\MyPath\\_path1\\a.txt");
    ASSERT_EQ_TOSTR("/MyPath/_path1/a.txt");
    ASSERT_EQ_TOSTR("/my_path/_path1/a.txt");
    ASSERT_EQ_TOSTR("~/my_path/_path1/a.txt");
    ASSERT_EQ_TOSTR("Asterix comics swearing $%&/#@|)(!\"'[]{}");
    ASSERT_EQ_TOSTR("2025-08-19 12:57:49.297 +02:00 [DBG] Logger retrieved for: Datadog.Trace.RemoteConfigurationManagement.RcmSubscriptionManager, Datadog.Trace, Version=3.24.0.0, Culture=neutral, PublicKeyToken=def86d061d0d2eeb  { MachineName: \".\", Process: \"[6520 Samples.Security.AspNetCore5]\", AppDomain: \"[1 Samples.Security.AspNetCore5]\", AssemblyLoadContext: \"\"\" Datadog.Trace.ClrProfiler.Managed.Loader.ManagedProfilerAssemblyLoadContext #2\", TracerVersion: \"3.24.0.0\"}");
    EXPECT_EQ(ToString(WStr("C:\\Users\\用户\\文档\\我的文件.txt")), "C:\\Users\\\xE7\x94\xA8\xE6\x88\xB7\\\xE6\x96\x87\xE6\xA1\xA3\\\xE6\x88\x91\xE7\x9A\x84\xE6\x96\x87\xE4\xBB\xB6.txt");
    EXPECT_EQ(ToString(WStr("~/my_path/_path1/a.txt🙈")), "~/my_path/_path1/a.txt\xF0\x9F\x99\x88");
}

TEST(StringTests, ToWString)
{
    ASSERT_EQ_TOWSTR("Hello World");
    ASSERT_EQ_TOWSTR("C:\\MyPath\\_path1\\a.txt");
    ASSERT_EQ_TOWSTR("/MyPath/_path1/a.txt");
    ASSERT_EQ_TOWSTR("/my_path/_path1/a.txt");
    ASSERT_EQ_TOWSTR("~/my_path/_path1/a.txt");
    ASSERT_EQ_TOWSTR("Asterix comics swearing $%&/#@|)(!\"'[]{}");
    ASSERT_EQ_TOWSTR("2025-08-19 12:57:49.297 +02:00 [DBG] Logger retrieved for: Datadog.Trace.RemoteConfigurationManagement.RcmSubscriptionManager, Datadog.Trace, Version=3.24.0.0, Culture=neutral, PublicKeyToken=def86d061d0d2eeb  { MachineName: \".\", Process: \"[6520 Samples.Security.AspNetCore5]\", AppDomain: \"[1 Samples.Security.AspNetCore5]\", AssemblyLoadContext: \"\"\" Datadog.Trace.ClrProfiler.Managed.Loader.ManagedProfilerAssemblyLoadContext #2\", TracerVersion: \"3.24.0.0\"}");
    EXPECT_EQ(ToWSTRING("C:\\Users\\\xE7\x94\xA8\xE6\x88\xB7\\\xE6\x96\x87\xE6\xA1\xA3\\\xE6\x88\x91\xE7\x9A\x84\xE6\x96\x87\xE4\xBB\xB6.txt"), WStr("C:\\Users\\用户\\文档\\我的文件.txt"));
    EXPECT_EQ(ToWSTRING("~/my_path/_path1/a.txt\xF0\x9F\x99\x88"), WStr("~/my_path/_path1/a.txt🙈"));
}