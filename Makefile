ROOT_DIR := $(shell dirname $(realpath $(lastword $(MAKEFILE_LIST))))

.PHONY: all

all: src/Datadog.Trace.ClrProfiler.Native/obj/Debug/x64/Datadog.Trace.ClrProfiler.Native.so \
     src/Datadog.Trace/bin/Debug/netstandard2.0/Datadog.Trace.dll \
     src/Datadog.Trace/bin/Release/netstandard2.0/Datadog.Trace.dll \
     src/Datadog.Trace.ClrProfiler.Managed/bin/Debug/netcoreapp2.0/Datadog.Trace.ClrProfiler.Managed.dll \
     src/Datadog.Trace.ClrProfiler.Managed/bin/Release/netcoreapp2.0/Datadog.Trace.ClrProfiler.Managed.dll \
     samples/Samples.ConsoleCore/bin/Debug/netcoreapp2.0/Samples.ConsoleCore.dll \
     samples/Samples.ConsoleCore/bin/Release/netcoreapp2.0/Samples.ConsoleCore.dll

/tmp/docker-coreclr: Dockerfile
	docker build -t coreclr:latest .
	touch /tmp/docker-coreclr

# Datadog.Trace

DATADOG_TRACE_FILES := $(shell find $(ROOT_DIR)/src/Datadog.Trace -type f -not -path '$(ROOT_DIR)/src/Datadog.Trace/bin*' -not -path '$(ROOT_DIR)/src/Datadog.Trace/obj*')

src/Datadog.Trace/bin/Debug/netstandard2.0/Datadog.Trace.dll: $(DATADOG_TRACE_FILES)
	docker run -v $(ROOT_DIR):/project microsoft/dotnet:2.1-sdk \
		dotnet build --configuration Debug /project/src/Datadog.Trace/Datadog.Trace.csproj

src/Datadog.Trace/bin/Release/netstandard2.0/Datadog.Trace.dll: $(DATADOG_TRACE_FILES)
	docker run -v $(ROOT_DIR):/project microsoft/dotnet:2.1-sdk \
		dotnet build --configuration Release /project/src/Datadog.Trace/Datadog.Trace.csproj

# Datadog.Trace.ClrProfiler.Managed

DATADOG_TRACE_MANAGED_FILES := $(shell find $(ROOT_DIR)/src/Datadog.Trace.ClrProfiler.Managed -type f -not -path '$(ROOT_DIR)/src/Datadog.Trace.ClrProfiler.Managed/bin*' -not -path '$(ROOT_DIR)/src/Datadog.Trace.ClrProfiler.Managed/obj*')

src/Datadog.Trace.ClrProfiler.Managed/bin/Debug/netcoreapp2.0/Datadog.Trace.ClrProfiler.Managed.dll: $(DATADOG_TRACE_MANAGED_FILES)
	docker run -v $(ROOT_DIR):/project microsoft/dotnet:2.1-sdk \
		dotnet build --configuration Debug /project/src/Datadog.Trace.ClrProfiler.Managed/Datadog.Trace.ClrProfiler.Managed.csproj

src/Datadog.Trace.ClrProfiler.Managed/bin/Release/netcoreapp2.0/Datadog.Trace.ClrProfiler.Managed.dll: $(DATADOG_TRACE_MANAGED_FILES)
	docker run -v $(ROOT_DIR):/project microsoft/dotnet:2.1-sdk \
		dotnet build --configuration Release /project/src/Datadog.Trace.ClrProfiler.Managed/Datadog.Trace.ClrProfiler.Managed.csproj

# Datadog.Trace.ClrProfiler.Native

DATADOG_TRACE_NATIVE_FILES := $(shell find $(ROOT_DIR)/src/Datadog.Trace.ClrProfiler.Native -type f -not -path '$(ROOT_DIR)/src/Datadog.Trace.ClrProfiler.Native/bin*' -not -path '$(ROOT_DIR)/src/Datadog.Trace.ClrProfiler.Native/obj*')

src/Datadog.Trace.ClrProfiler.Native/obj/Debug/x64/Datadog.Trace.ClrProfiler.Native.so: /tmp/docker-coreclr $(DATADOG_TRACE_NATIVE_FILES)
	docker run -v $(ROOT_DIR):/project coreclr:latest \
		sh -c 'cd /project/src/Datadog.Trace.ClrProfiler.Native/ && mkdir -p obj/Debug/x64 && cd obj/Debug/x64 && cmake ../../.. && make'

src/Datadog.Trace.ClrProfiler.Native/bin/Debug/x64/Datadog.Trace.ClrProfiler.Native.so: src/Datadog.Trace.ClrProfiler.Native/obj/Debug/x64/Datadog.Trace.ClrProfiler.Native.so
	mkdir -p src/Datadog.Trace.ClrProfiler.Native/bin/Debug/x64/
	cp src/Datadog.Trace.ClrProfiler.Native/obj/Debug/x64/Datadog.Trace.ClrProfiler.Native.so src/Datadog.Trace.ClrProfiler.Native/bin/Debug/x64/Datadog.Trace.ClrProfiler.Native.so

# Samples.ConsoleCore

SAMPLES_CONSOLECORE_FILES := $(shell find $(ROOT_DIR)/samples/Samples.ConsoleCore -type f -not -path '$(ROOT_DIR)/samples/Samples.ConsoleCore/bin*' -not -path '$(ROOT_DIR)/samples/Samples.ConsoleCore/obj*')

samples/Samples.ConsoleCore/bin/Debug/netcoreapp2.0/Samples.ConsoleCore.dll: $(SAMPLES_CONSOLECORE_FILES)
	docker run -v $(ROOT_DIR):/project microsoft/dotnet:2.1-sdk \
		dotnet build --configuration Debug /project/samples/Samples.ConsoleCore/Samples.ConsoleCore.csproj

samples/Samples.ConsoleCore/bin/Release/netcoreapp2.0/Samples.ConsoleCore.dll: $(SAMPLES_CONSOLECORE_FILES)
	docker run -v $(ROOT_DIR):/project microsoft/dotnet:2.1-sdk \
		dotnet build --configuration Release /project/samples/Samples.ConsoleCore/Samples.ConsoleCore.csproj

Samples.ConsoleCore: samples/Samples.ConsoleCore/bin/Release/netcoreapp2.0/Samples.ConsoleCore.dll src/Datadog.Trace.ClrProfiler.Native/obj/Debug/x64/Datadog.Trace.ClrProfiler.Native.so
	docker run -it -v $(ROOT_DIR):/project microsoft/dotnet:2.1-sdk sh -c " \
		(mkdir -p /var/log/datadog && touch /var/log/datadog/dotnet-profiler.log) ; \
		env CORECLR_ENABLE_PROFILING=1 \
		    CORECLR_PROFILER={846F5F1C-F9AE-4B07-969E-05C26BC060D8} \
		    CORECLR_PROFILER_PATH=/project/src/Datadog.Trace.ClrProfiler.Native/obj/Debug/x64/Datadog.Trace.ClrProfiler.Native.so \
		    DD_INTEGRATIONS='/project/integrations.json;/project/test-integrations.json' \
		dotnet /project/samples/Samples.ConsoleCore/bin/Release/netcoreapp2.0/Samples.ConsoleCore.dll ; \
		cat /var/log/datadog/dotnet-profiler.log \
		"

# Datadog.Trace.ClrProfiler.IntegrationTests

CLR_PROFILER_INTEGRATION_TEST_FILES := $(shell find $(ROOT_DIR)/test/Datadog.Trace.ClrProfiler.IntegrationTests -type f -not -path '*/bin*' -not -path '*/obj*')

test/Datadog.Trace.ClrProfiler.IntegrationTests/bin/Release/netcoreapp2.0/publish/Datadog.Trace.ClrProfiler.IntegrationTests.dll: Makefile $(CLR_PROFILER_INTEGRATION_TEST_FILES)
	docker run -v $(ROOT_DIR):/project microsoft/dotnet:2.1-sdk \
		dotnet publish --framework netcoreapp2.0 --configuration Release /project/test/Datadog.Trace.ClrProfiler.IntegrationTests/Datadog.Trace.ClrProfiler.IntegrationTests.csproj

Datadog.Trace.ClrProfiler.IntegrationTests: src/Datadog.Trace.ClrProfiler.Native/bin/Debug/x64/Datadog.Trace.ClrProfiler.Native.so
	docker run -it -v $(ROOT_DIR):/project microsoft/dotnet:2.1-sdk sh -c " \
		(mkdir -p /var/log/datadog && touch /var/log/datadog/dotnet-profiler.log) ; \
		dotnet test /project/test/Datadog.Trace.ClrProfiler.IntegrationTests/Datadog.Trace.ClrProfiler.IntegrationTests.csproj ; \
		cat /var/log/datadog/dotnet-profiler.log \
		"
