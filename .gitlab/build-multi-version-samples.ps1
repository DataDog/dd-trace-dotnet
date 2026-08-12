$ErrorActionPreference = "Stop"

$includeMinorPackageVersions = $env:perform_comprehensive_testing -eq "true"
$sampleName = $env:IntegrationTestSampleName
Write-Output "Building multi-version samples for $env:FRAMEWORK (includeMinorPackageVersions=$includeMinorPackageVersions)"

docker run --rm -m 20480M `
  -v "$(Get-Location):c:\mnt" `
  -e CI_JOB_ID=${env:CI_JOB_ID} `
  -e CI=true `
  -e CI_PROJECT_NAME `
  -e CI_JOB_NAME_SLUG `
  -e CI_COMMIT_SHA `
  -e CI_COMMIT_BRANCH `
  -e GITLAB_CI `
  -e "SourceRevisionId=$env:CI_COMMIT_SHA" `
  -e "RepositoryUrl=https://github.com/DataDog/dd-trace-dotnet.git" `
  -e WINDOWS_BUILDER=true `
  -e AWS_NETWORKING=true `
  -e NUGET_CERT_REVOCATION_MODE=offline `
  -e NUGET_ENABLE_EXPERIMENTAL_HTTP_RETRY=true `
  -e "SampleName=$sampleName" `
  -e "IncludeMinorPackageVersions=$includeMinorPackageVersions" `
  $env:WINDOWS_BUILD_IMAGE `
  CreateRequiredDirectories CompileSamples `
  --framework $env:FRAMEWORK `
  --TestAllPackageVersions true `
  --NugetPackageDirectory c:\mnt\packages
if ($LASTEXITCODE -ne 0) {
  throw "Multi-version sample build for $env:FRAMEWORK exited with code $LASTEXITCODE"
}
