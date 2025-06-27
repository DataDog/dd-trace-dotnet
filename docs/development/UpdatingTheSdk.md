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
  - `.github/workflows/*.yml` (all workflow files)
  - `.gitlab-ci.yml`
- **Docker Files**:
  - `docker-compose.yml`
  - `tracer/build/_build/docker/gitlab/gitlab.windows.dockerfile`
  - All smoke test dockerfiles in `tracer/build/_build/docker/`
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

### 3. Integration Support Updates *(Major versions only)*
Update automatic instrumentation to support the new runtime:

- **Version Constants**:
  - `tracer/src/Datadog.Trace/AutoInstrumentation/SupportedVersions.cs`
- **Integration Definitions**: Update minimum supported versions in:
  - `tracer/src/Datadog.Trace/Generated/InstrumentationDefinitions.g.cs`
  - Integration-specific definition files

### 4. Package Version Generation *(Major versions only)*
Regenerate package versions for integration tests:

- Run package version generation to create new test matrices
- Update generated files:
  - `tracer/build/PackageVersionsLatestMajors.g.props`
  - `tracer/build/PackageVersionsLatestMinors.g.props`
  - `tracer/build/PackageVersionsLatestSpecific.g.props`

### 5. Runtime-Specific Code Updates *(Major versions only)*
Address breaking changes and new APIs:

- **IAST (Interactive Application Security Testing)**:
  - Add new method overloads in `tracer/src/Datadog.Trace/Iast/Aspects/`
  - Update aspect definitions for new APIs (File, Type, String, StringBuilder)
- **Activity Filtering**: 
  - Exclude new experimental activities to prevent duplicate spans
  - Update `tracer/src/Datadog.Trace/Activity/Handlers/IgnoreActivityHandler.cs`
- **Duck Typing**: Address reflection changes for internal APIs
- **Test Updates**: Fix or skip tests incompatible with new runtime

### 6. Infrastructure Updates
Update development and CI infrastructure:

- **Virtual Machine Scale Sets**: Switch to new VM pools with updated SDK
- **Container Images**: Update base images and build environments
- **Build Tools**: Update MSBuild tools and related components

### 7. Documentation and Configuration
- **Development Documentation**: Update CI guides and local development instructions
- **Container Configurations**: Update devcontainer and development environment configs

## Pre-Release Version Considerations

When updating to pre-release versions (RC, preview):
- Use pre-release SDK versions in `global.json`
- Enable pre-release rolling forward in runtime configurations
- May require different container image tags or availability
- Consider stability for production CI environments

## Stable vs Pre-Release Differences

**Stable Versions:**
- Full container image availability across all variants
- Complete package ecosystem support
- Recommended for production CI

**Pre-Release Versions:**
- Limited container image variants
- May require manual SDK installation
- Package versions may be incomplete
- Useful for early testing and preparation

## Files Requiring Manual Review

The following areas typically require manual attention during major upgrades:

1. **Test Snapshots**: May need regeneration due to runtime changes
2. **Integration Test Compatibility**: Some packages may not support new runtimes immediately  
3. **Native Code Compatibility**: Profiler and native components may need updates
4. **Breaking Changes**: Runtime behavior changes requiring code adaptations

## Validation Steps

After completing updates:
1. Ensure all builds pass in CI
2. Run integration test suites
3. Execute smoke tests across all supported platforms
4. Validate profiler integration tests
5. Check for any skipped tests that can be re-enabled

## Post-Merge Tasks

Major version updates typically require follow-up work:
- Update VM scale sets with new SDK versions
- Update GitLab build images
- Re-enable temporarily skipped tests
- Update benchmark environments
- Monitor for any runtime-specific issues in production

---

*This document provides a high-level overview. Detailed implementation steps and specific file changes should be determined by examining previous version bump commits and current repository state.*