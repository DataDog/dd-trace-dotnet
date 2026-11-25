// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "gtest/gtest.h"

#include <sys/types.h>
#include <sys/wait.h>
#include <unistd.h>

extern char** environ;

// Defined in Datadog.Linux.ApiWrapper (preloaded when running this test suite).
// Returns non-zero when the "application is crashing" flag is raised.
extern "C" unsigned long long dd_inside_wrapped_functions() __attribute__((weak));

namespace CrashFlagTest {

// The .NET runtime handles a fatal signal by fork()ing a child which execve()s createdump
// while the crashing parent waits for it. The wrapper intercepts that execve and raises a
// flag stored in MAP_SHARED memory, so that the profiler stops collecting callstacks in the
// crashing parent (see https://github.com/DataDog/dd-trace-dotnet/pull/7657).
// This must happen even when the call is not redirected to the Datadog crash handler
// (ex: DD_CRASHTRACKING_ENABLED=false, or dd-dotnet not found next to the wrapper).
// Note: the flag is sticky for the remaining lifetime of this test process.
TEST(CrashFlagTest, ExecveOfCreatedumpRaisesTheCrashFlagInTheParent)
{
    if (dd_inside_wrapped_functions == nullptr)
    {
        GTEST_SKIP() << "Datadog.Linux.ApiWrapper is not preloaded";
    }

    ASSERT_EQ(0u, dd_inside_wrapped_functions());

    pid_t pid = fork();
    ASSERT_NE(-1, pid);

    if (pid == 0)
    {
        // Mimic the runtime's crash path: no --name argument is passed when
        // DOTNET_DbgMiniDumpName is not set. The execve itself fails (the path
        // does not exist), but the interception runs before the real execve.
        char* const argv[] = {const_cast<char*>("createdump"), const_cast<char*>("1234"), nullptr};
        execve("/nonexistent_path_for_test/createdump", argv, environ);
        _exit(0);
    }

    int status = 0;
    ASSERT_EQ(pid, waitpid(pid, &status, 0));

    // The child shares the MAP_SHARED flag with this process: it must now be raised.
    ASSERT_NE(0u, dd_inside_wrapped_functions());
}

} // namespace CrashFlagTest
