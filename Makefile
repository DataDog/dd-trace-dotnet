ROOT_DIR := $(shell dirname $(realpath $(lastword $(MAKEFILE_LIST))))

CPP_FILES := $(wildcard $(NATIVE_DIR)/*.cpp)
H_FILES := $(wildcard $(NATIVE_DIR)/*.h)

.PHONY: all Samples.ConsoleCore

all: $(ROOT_DIR)/src/Datadog.Trace.ClrProfiler.Native/bin/Release/linux-x64/Datadog.Trace.ClrProfiler.Native.so \
     $(ROOT_DIR)/src/Datadog.Trace/bin/Debug/netstandard2.0/Datadog.Trace.dll \
	 $(ROOT_DIR)/src/Datadog.Trace/bin/Release/netstandard2.0/Datadog.Trace.dll \
	 $(ROOT_DIR)/src/Datadog.Trace.ClrProfiler.Managed/bin/Debug/netcoreapp2.0/Datadog.Trace.ClrProfiler.Managed.dll \
	 $(ROOT_DIR)/src/Datadog.Trace.ClrProfiler.Managed/bin/Release/netcoreapp2.0/Datadog.Trace.ClrProfiler.Managed.dll \
	$(ROOT_DIR)/samples/Samples.ConsoleCore/bin/Debug/netcoreapp2.0/Samples.ConsoleCore.dll \
	$(ROOT_DIR)/samples/Samples.ConsoleCore/bin/Release/netcoreapp2.0/Samples.ConsoleCore.dll

Dockerfile:
	docker build -t coreclr .

# Datadog.Trace

DATADOG_TRACE_FILES := $(shell find $(ROOT_DIR)/src/Datadog.Trace -type f -not -path '$(ROOT_DIR)/src/Datadog.Trace/bin*' -not -path '$(ROOT_DIR)/src/Datadog.Trace/obj*')

$(ROOT_DIR)/src/Datadog.Trace/bin/Debug/netstandard2.0/Datadog.Trace.dll: $(DATADOG_TRACE_FILES)
	docker run -v $(ROOT_DIR):/project microsoft/dotnet:2.1-sdk dotnet build --configuration Debug /project/src/Datadog.Trace/Datadog.Trace.csproj

$(ROOT_DIR)/src/Datadog.Trace/bin/Release/netstandard2.0/Datadog.Trace.dll: $(DATADOG_TRACE_FILES)
	docker run -v $(ROOT_DIR):/project microsoft/dotnet:2.1-sdk dotnet build --configuration Release /project/src/Datadog.Trace/Datadog.Trace.csproj

# Datadog.Trace.ClrProfiler.Managed

DATADOG_TRACE_MANAGED_FILES := $(shell find $(ROOT_DIR)/src/Datadog.Trace.ClrProfiler.Managed -type f -not -path '$(ROOT_DIR)/src/Datadog.Trace.ClrProfiler.Managed/bin*' -not -path '$(ROOT_DIR)/src/Datadog.Trace.ClrProfiler.Managed/obj*')

$(ROOT_DIR)/src/Datadog.Trace.ClrProfiler.Managed/bin/Debug/netcoreapp2.0/Datadog.Trace.ClrProfiler.Managed.dll: $(DATADOG_TRACE_MANAGED_FILES)
	docker run -v $(ROOT_DIR):/project microsoft/dotnet:2.1-sdk dotnet build --configuration Debug /project/src/Datadog.Trace.ClrProfiler.Managed/Datadog.Trace.ClrProfiler.Managed.csproj

$(ROOT_DIR)/src/Datadog.Trace.ClrProfiler.Managed/bin/Release/netcoreapp2.0/Datadog.Trace.ClrProfiler.Managed.dll: $(DATADOG_TRACE_MANAGED_FILES)
	docker run -v $(ROOT_DIR):/project microsoft/dotnet:2.1-sdk dotnet build --configuration Release /project/src/Datadog.Trace.ClrProfiler.Managed/Datadog.Trace.ClrProfiler.Managed.csproj

# Datadog.Trace.ClrProfiler.Native

$(ROOT_DIR)/src/Datadog.Trace.ClrProfiler.Native/obj/linux-x64/Datadog.Trace.ClrProfiler.Native.so: Dockerfile $(CPP_FILES) $(H_FILES)
	docker run -v $(ROOT_DIR)/src/Datadog.Trace.ClrProfiler.Native:/src coreclr:latest sh -c 'mkdir -p /src/obj/linux-x64 && cd /src/obj/linux-x64 && cmake ../.. && make'

$(ROOT_DIR)/src/Datadog.Trace.ClrProfiler.Native/bin/Release/linux-x64/Datadog.Trace.ClrProfiler.Native.so: $(ROOT_DIR)/src/Datadog.Trace.ClrProfiler.Native/obj/linux-x64/Datadog.Trace.ClrProfiler.Native.so
	mkdir -p $(ROOT_DIR)/src/Datadog.Trace.ClrProfiler.Native/bin/Release/linux-x64
	cp $(ROOT_DIR)/src/Datadog.Trace.ClrProfiler.Native/obj/linux-x64/Datadog.Trace.ClrProfiler.Native.so $(ROOT_DIR)/src/Datadog.Trace.ClrProfiler.Native/bin/Release/linux-x64/Datadog.Trace.ClrProfiler.Native.so

# Samples.ConsoleCore

DATADOG_TRACE_MANAGED_FILES := $(shell find $(ROOT_DIR)/samples/Samples.ConsoleCore -type f -not -path '$(ROOT_DIR)/samples/Samples.ConsoleCore/bin*' -not -path '$(ROOT_DIR)/samples/Samples.ConsoleCore/obj*')

$(ROOT_DIR)/samples/Samples.ConsoleCore/bin/Debug/netcoreapp2.0/Samples.ConsoleCore.dll: $(DATADOG_TRACE_MANAGED_FILES)
	docker run -v $(ROOT_DIR):/project microsoft/dotnet:2.1-sdk dotnet build --configuration Debug /project/samples/Samples.ConsocaleCore/Samples.ConsoleCore.csproj

$(ROOT_DIR)/samples/Samples.ConsoleCore/bin/Release/netcoreapp2.0/Samples.ConsoleCore.dll: $(DATADOG_TRACE_MANAGED_FILES)
	docker run -v $(ROOT_DIR):/project microsoft/dotnet:2.1-sdk dotnet build --configuration Release /project/samples/Samples.ConsoleCore/Samples.ConsoleCore.csproj

Samples.ConsoleCore: $(ROOT_DIR)/samples/Samples.ConsoleCore/bin/Release/netcoreapp2.0/Samples.ConsoleCore.dll
	docker run -it -v $(ROOT_DIR):/project microsoft/dotnet:2.1-sdk \
		env CORECLR_ENABLE_PROFILING=1 \
		    CORECLR_PROFILER={846F5F1C-F9AE-4B07-969E-05C26BC060D8} \
		    CORECLR_PROFILER_PATH=/project/src/Datadog.Trace.ClrProfiler.Native/bin/Release/linux-x64/Datadog.Trace.ClrProfiler.Native.so \
		dotnet run --framework netcoreapp2.0 --project /project/samples/Samples.ConsoleCore/Samples.ConsoleCore.csproj
