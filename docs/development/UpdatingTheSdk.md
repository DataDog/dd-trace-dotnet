# Updating the .NET SDK Version

This document provides a high-level overview of the steps required when updating the version of the .NET SDK used in the repository, particularly for major version upgrades.

## Overview

When updating the .NET SDK version, there are different levels of changes required depending on the type of update:

- **Minor version updates** (e.g., 9.0.102 → 9.0.203) - Minimal changes, mostly find-and-replace
- **Major version updates** (e.g., .NET 8 → .NET 9) - Extensive changes across multiple areas
- **Pre-release updates** (e.g., stable → RC) - Similar to major updates but with additional considerations

## High-Level Steps for Major Version Updates

### 1. SDK and Runtime References
Update all references to the SDK version across the repository:

- **`global.json`** - Primary SDK version configuration
- **CI/CD Pipeline Files**:
  - `.azure-pipelines/ultimate-pipeline.yml`
  - `.azure-pipelines/noop-pipeline.yml`
  - `.azure-pipelines/steps/install-dotnet.yml`
  - `.github/workflows/*.yml` (all workflow files)
  - `.gitlab-ci.yml`
- **Docker Files**:
  - `docker-compose.yml`
  - `tracer/build/_build/docker/gitlab/gitlab.windows.dockerfile` - the new version should be built locally pushed, and synced to datadog/images, and used in the new build.
  - `tracer/build/_build/docker/alpine.dockerfile`
  - `tracer/build/_build/docker/centos7.dockerfile`
  - `tracer/build/_build/docker/debian.dockerfile`
- **Build Scripts**:
  - `tracer/build_in_docker.ps1`
  - `tracer/build_in_docker.sh`

### 2. Target Framework Updates *(Major versions only)*
Add support for the new .NET version across projects:

- **Build System**:
  - `tracer/build/_build/TargetFramework.cs` - Add new target framework enum
  - `tracer/build/_build/TargetFrameworkExtensions.cs` - Add version comparison logic
- **Project Files**: Update `<TargetFrameworks>` in `.csproj` files across:
  - Test applications in `tracer/test/test-applications/`
  - Integration test projects
  - Sample applications
  - Tools and utilities
  - `Build.Directory.props` files
- **Smoke Tests**:
  - Add additional smoke tests for the new .NET release
  - This may involve pushing new versions of our dedicated smoke test dockerfiles for distros that don't have official Microsoft

### 3. Integration Support Updates *(Major versions only)*
Update automatic instrumentation to support the new runtime:

- **Version Constants**:
  - `tracer/src/Datadog.Trace/AutoInstrumentation/SupportedVersions.cs`
- **Integration Definitions**: Update supported versions for integration targets as required in:
  - `tracer/src/Datadog.Trace/ClrProfiler/*`
- **Native Loader**: 
  - The native loader lists the supported versions for SSI and bails out if outside this range. Need to consider whether that needs updating.

### 4. Package Version Generation *(Major versions only)*
Regenerate package versions for integration tests:

- Run package version generation to create new test matrices, often required for testing "built-in" versions of packages that ship with the framework.

### 5. Runtime-Specific Code Updates *(Major versions only)*
Address breaking changes and new APIs:

- **IAST (Interactive Application Security Testing)**:
  - Add new method overloads in `tracer/src/Datadog.Trace/Iast/Aspects/`
  - Update aspect definitions for new APIs (File, Type, String, StringBuilder)
- **Activity Filtering**: 
  - Exclude new experimental activities to prevent duplicate spans
  - Update `tracer/src/Datadog.Trace/Activity/Handlers/IgnoreActivityHandler.cs`
- **Test Updates**: Fix or skip tests incompatible with new runtime

### 6. Infrastructure Updates
Update development and CI infrastructure:

- **Virtual Machine Scale Sets**: Switch to new VM pools with updated SDK
- **Container Images**: Update base images and build environments
- **Build Tools**: Update MSBuild tools and related components

### 7. Documentation and Configuration
- **Development Documentation**: Update CI guides and local development instructions
- **Container Configurations**: Update devcontainer and development environment configs
- **Update**: Update public documentation

## Pre-Release Version Considerations

When updating to pre-release versions (RC, preview):
- Use pre-release SDK versions in `global.json`
- Enable pre-release rolling forward in runtime configurations
- May require different container image tags or availability
- By default we bail out of preview versions of the SDK in SSI environments in the native loader, need to consider whether that needs changing, or whether we should continue to bail out.

## Files Requiring Manual Review

The following areas typically require manual attention during major upgrades:

1. **Test Snapshots**: May need regeneration due to runtime changes - you may need to add additional scrubbing to account for the differences.
2. **Integration Test Compatibility**: Some packages may not support new runtimes immediately, though typically this won't be a problem.
3. **Native Code Compatibility**: Profiler and native components may need updates - if new `ICorProfiler` interfaces are available, we may need to use them.
4. **Breaking Changes**: Runtime behavior changes requiring code adaptations

## Validation Steps

After completing updates Ensure all builds pass in CI, doing dedicated "full" runs against all target frameworks and all installer steps before merging.

## Post-Merge Tasks

Major version updates typically require follow-up work:
- Rebuild VM scale sets with new SDK versions installed
- Update benchmark environments - we need additional refactoring, as currently these will be broken for the update branch + master after merge, and will need follow up to fix. Ideally this should be done in parallel, but that may not be feasible.
- Monitor for any issues in master or in production

---

*This document provides a high-level overview. Detailed implementation steps and specific file changes should be determined by examining previous version bump commits and current repository state.*