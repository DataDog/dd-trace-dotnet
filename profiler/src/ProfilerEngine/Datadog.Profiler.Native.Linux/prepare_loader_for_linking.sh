#!/bin/bash

pushd ../../../../_build/bin/$1-AnyCPU/shared/src/managed-lib/ManagedLoader/netcoreapp2.0/

echo "Create obj file Datadog.AutoInstrumentation.ManagedLoader.dll.o"
ld -r -b binary Datadog.AutoInstrumentation.ManagedLoader.dll -o Datadog.AutoInstrumentation.ManagedLoader.dll.o

echo "Create obj file Datadog.AutoInstrumentation.ManagedLoader.pdb.o"
ld -r -b binary Datadog.AutoInstrumentation.ManagedLoader.pdb -o Datadog.AutoInstrumentation.ManagedLoader.pdb.o