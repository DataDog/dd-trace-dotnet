#!/bin/bash



ddprof_deploy_folder=$1
commit_sha=$2
commit_author=$3

current_profiler_version="2.14.0"
current_profiler_beta_version="2"
profiler_version=v${current_profiler_version}.${current_profiler_beta_version}_$(date -u +%G%m%d%H%M%S)

## Create master.index.txt file
cat <<- EOF > master.index.txt
master
${commit_sha}
datadog-dotnet-profiler-apm-${profiler_version}-*
${commit_author}
EOF

## Create package
package_name="datadog-dotnet-profiler-apm-${profiler_version}-full"
compressed_package_name="${package_name}.zip"
echo "Creating ${compressed_package_name}..."
mv ${ddprof_deploy_folder} ${package_name}  || (echo "Failed to move ${ddprof_deploy_folder} to ${package_name}." && exit 1)
zip -r ${compressed_package_name} ${package_name} || (echo "Failed to compress ${package_name}." && exit 1)

## Deploy on AWS
echo "Copying ${compressed_package_name} to S3"
aws s3 cp ${compressed_package_name} s3://datadog-reliability-env/dotnet-profiler/${compressed_package_name} || (echo "Failed to copy ${compressed_package_name} to S3." && exit 1)

echo "Copying master.index.txt to S3"
aws s3 cp master.index.txt s3://datadog-reliability-env/dotnet-profiler/master.index.txt || (echo "Failed to copy master.index.txt to S3." && exit 1)
