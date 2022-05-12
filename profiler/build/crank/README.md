Crank
=====

The crank jobs are primarily designed to run as part of the CI, but it can be useful to run them from your local machine
if you're trying to optimize something.

The scripts `run.sh` and `run-appsec.sh` are design to be used at part of the CI.

The script `./run-adhoc.sh` should be run from a Linux dd-trace-dotnet development environment (WSL will do nicely). It can be used
to run a throughput test with the tracer and / or profiler attached.

Before running the script, the tracer and profiler (if desired) source must be built. Use the following commands:

```
./tracer/build.sh BuildTracerHome
# if the profiler is desired
./tracer/build.sh BuildProfilerHome
./tracer/build.sh CompileNativeLoader
./tracer/build.sh PublishNativeLoader
```

Once the source is built the script can used with the following command line:

```
./tracer/build/crank/run-adhoc.sh appsec_baseline linux_adhoc true
```

or

```
./tracer/build/crank/run-adhoc.sh appsec_baseline linux_profiler_adhoc true
```

1st parameter - name of the test to run
2nd parameter - name of the OS profile to use
3rd parameter - true use the appsec tests definitions file, false for tracer