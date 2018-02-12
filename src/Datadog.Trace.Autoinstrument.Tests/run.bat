SET COR_ENABLE_PROFILING=1
SET COR_PROFILER={cf0d821e-299b-5307-a3d8-b283c03916dd}
SET COR_PROFILER_PATH=C:\Users\bertr\github\ddprofiler\build64\Debug\ddprofiler.dll

SET CORECLR_ENABLE_PROFILING=1
SET CORECLR_PROFILER={cf0d821e-299b-5307-a3d8-b283c03916dd}
SET CORECLR_PROFILER_PATH=C:\Users\bertr\github\ddprofiler\build64\Debug\ddprofiler.dll

@rem dotnet test --framework net452
@rem dotnet test --framework net461
dotnet test --framework netcoreapp2.0
