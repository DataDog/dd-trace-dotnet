#!/bin/bash

base_dir=$1
configuration=$2

for i in $(find $base_dir -name "*.Tests.csproj")
do
	dotnet test -c $configuration $i
done