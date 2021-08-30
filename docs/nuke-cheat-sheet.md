Build / Nuke Cheat Sheet
========================

# Starting out

Use one of the bootstap scripts provided at the root of source tree to launch the build:

```
> .\build.cmd [targets] [arguments]
> .\build.ps1 [targets] [arguments]
$ ./build.sh [targets] [arguments]
```

To see all the targets and arguments:

```
build --help
```

To see the dependency graph of all targets:

```
build --plan
```

# Building src

Build all the src folder and ensure everything is ready of testing:

```
build BuildTracerHome
```

or to ensure all is deleted before building:

```
build Clean BuildTracerHome
```

If this succees you'll see the following output:

```
════════════════════════════════════════════════════
Target                          Status      Duration
────────────────────────────────────────────────────
Restore                         Executed        0:16
CreateRequiredDirectories       Executed      < 1sec
CompileManagedSrc               Executed        0:33
PublishManagedProfiler          Executed        0:08
CompileNativeSrcMacOs           Skipped                // EnvironmentInfo.IsOsx
CompileNativeSrcLinux           Skipped                // EnvironmentInfo.IsLinux
CompileNativeSrcWindows         Executed        0:03
PublishNativeProfilerMacOs      Skipped                // EnvironmentInfo.IsOsx
PublishNativeProfilerLinux      Skipped                // EnvironmentInfo.IsLinux
PublishNativeProfilerWindows    Executed      < 1sec
DownloadLibSqreen               Executed      < 1sec
CopyLibSqreen                   Executed      < 1sec
CopyIntegrationsJson            Executed      < 1sec
CreateDdTracerHome              Executed        0:02
────────────────────────────────────────────────────
Total                                           1:04
════════════════════════════════════════════════════

Build succeeded on 29/07/2021 17:28:29. ＼（＾ᴗ＾）／
```

If you're just changing source code from managed languages, you'll generally only need to do `build BuildTracerHome` once per coding session. After you can use this command to build your changes:

```
build CompileManagedSrc
```

# Unit Tests

Build and run all unit tests:

```
build BuildAndRunManagedUnitTests
```

Build and run a specific unit test:

```
build BuildAndRunManagedUnitTests -framework net5.0 -Filter Datadog.Trace.Tests.DiagnosticListeners.AspNetCoreDiagnosticObserverTests.CompleteDiagnosticObserverTest
```

# Integration Tests

```
build BuildWindowsIntegrationTests
```

To run all integration tests:

```
build RunWindowsIntegrationTests
```

### Note: the -framework filter isn't implemented for the next two, I made some attempt to implement it, but it wasn't working, so I undid my changes.

It's unlikely that you'll want to run all integration tests, because they can take a long time to run, and lots of them have external dependencies, which need to be started seperately. It's more likely you'll want to run a specific test:

```
build RunWindowsIntegrationTests -framework net5.0 -Filter Datadog.Trace.Security.IntegrationTests.AspNetCore5.TestSecurity 
```

To compile the integration tests, a specific sample and run them:

```
build CompileIntegrationTests CompileSamples RunWindowsIntegrationTests -framework net5.0 -SampleName Samples.AspNetCore5 -Filter Datadog.Trace.Security.IntegrationTests.AspNetCore5.TestSecurity
```

# Running samples

Builds and runs a sample:

```
build RunDotNetSample -SampleName Samples.AspNetCore5 -framework net5.0
```

Note: this does not build any part of the tracer, so if you're using a sample to test changes to the tracer the quickest way is:

```
build CompileManagedSrc PublishManagedProfiler RunDotNetSample -SampleName Samples.AspNetCore5 -framework net5.0
```

