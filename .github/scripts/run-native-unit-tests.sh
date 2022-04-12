#!/bin/bash


base_dir=$1
configuration=$2
platform=$3

for i in $(find $base_dir -name "*.Tests.vcxproj")
do
    project_name=$(basename $i ".vcxproj")
    ./profiler/_build/bin/${configuration}-${platform}/profiler/test/${project_name}/${project_name}.exe
done
