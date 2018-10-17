ROOT_DIR := $(shell dirname $(realpath $(lastword $(MAKEFILE_LIST))))

/tmp/docker-coreclr: Dockerfile
	docker build -t coreclr:latest .
	touch /tmp/docker-coreclr

# Datadog.Trace.ClrProfiler.Managed

SAMPLE_FILES := $(shell find ./samples -type f -not -path '*/bin*' -not -path '*/obj*')
SRC_FILES := $(shell find ./src -type f -not -path '*/bin*' -not -path '*/obj*')
TEST_FILES := $(shell find ./test -type f -not -path '*/bin*' -not -path '*/obj*')

build: $(SAMPLE_FILES) $(SRC_FILES) $(TEST_FILES)
	docker run -v $(ROOT_DIR):/project microsoft/dotnet:2.1-sdk sh -c 'cd /project && ./Makefile-build.sh'

# Datadog.Trace.ClrProfiler.Native

NATIVE_FILES := $(shell find $(ROOT_DIR)/src/Datadog.Trace.ClrProfiler.Native -type f -not -path '$(ROOT_DIR)/src/Datadog.Trace.ClrProfiler.Native/bin*' -not -path '$(ROOT_DIR)/src/Datadog.Trace.ClrProfiler.Native/obj*')

native: /tmp/docker-coreclr $(NATIVE_FILES)
	docker run -v $(ROOT_DIR):/project coreclr:latest \
		sh -c 'cd /project/src/Datadog.Trace.ClrProfiler.Native/ && mkdir -p obj/Debug/x64 && cd obj/Debug/x64 && cmake ../../.. && make'
	mkdir -p src/Datadog.Trace.ClrProfiler.Native/bin/Debug/x64/
	cp src/Datadog.Trace.ClrProfiler.Native/obj/Debug/x64/Datadog.Trace.ClrProfiler.Native.so src/Datadog.Trace.ClrProfiler.Native/bin/Debug/x64/Datadog.Trace.ClrProfiler.Native.so

# Samples.ConsoleCore

runsample/%:
	docker run -it -v $(ROOT_DIR):/project microsoft/dotnet:2.1-sdk sh -c " \
		(mkdir -p /var/log/datadog && touch /var/log/datadog/dotnet-profiler.log) ; \
		env CORECLR_ENABLE_PROFILING=1 \
		    CORECLR_PROFILER={846F5F1C-F9AE-4B07-969E-05C26BC060D8} \
		    CORECLR_PROFILER_PATH=/project/src/Datadog.Trace.ClrProfiler.Native/obj/Debug/x64/Datadog.Trace.ClrProfiler.Native.so \
		    DD_INTEGRATIONS='/project/integrations.json;/project/test-integrations.json' \
		dotnet /project/samples/Samples.$*/bin/Release/netcoreapp2.0/Samples.$*.dll ; \
		cat /var/log/datadog/dotnet-profiler.log \
		"

Samples.ConsoleCore: runsample/Samples.ConsoleCore

# Datadog.Trace.ClrProfiler.IntegrationTests

runtest/%:
	docker run -it -v $(ROOT_DIR):/project microsoft/dotnet:2.1-sdk sh -c " \
		(mkdir -p /var/log/datadog && touch /var/log/datadog/dotnet-profiler.log) ; \
		dotnet test /project/test/$*/$*.csproj ; \
		cat /var/log/datadog/dotnet-profiler.log \
		"

Datadog.Trace.ClrProfiler.IntegrationTests: runtest/Datadog.Trace.ClrProfiler.IntegrationTests
