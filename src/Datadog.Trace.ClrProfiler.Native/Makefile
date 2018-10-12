CPP_FILES := $(wildcard *.cpp)
H_FILES := $(wildcard *.h)
ROOT_DIR:=$(shell dirname $(realpath $(lastword $(MAKEFILE_LIST))))

.PHONY: all

all: bin/linux-amd64/Datadog.Trace.ClrProfiler.Native.so

Dockerfile:
	docker build -t coreclr .

obj/linux-amd64/Datadog.Trace.ClrProfiler.Native.so: Dockerfile $(CPP_FILES) $(H_FILES)
	docker run -v $(ROOT_DIR):/src coreclr:latest sh -c 'mkdir -p /src/obj/linux-amd64 && cd /src/obj/linux-amd64 && cmake ../.. && make'

bin/linux-amd64/Datadog.Trace.ClrProfiler.Native.so: obj/linux-amd64/Datadog.Trace.ClrProfiler.Native.so
	mkdir -p bin/linux-amd64
	cp obj/linux-amd64/Datadog.Trace.ClrProfiler.Native.so bin/linux-amd64/Datadog.Trace.ClrProfiler.Native.so
