# Datadog.FleetInstaller

Windows SSI fleet-installer command line tool for managing tracer installations.

## Purpose

Manages tracer installation and configuration on Windows systems:
- Install/uninstall tracer versions
- Enable/remove IIS instrumentation
- Manage GAC installation of tracer assemblies
- Configure registry settings for instrumentation

## Key Functionality

- **Version management**: Install and switch between tracer versions
- **IIS integration**: Enable/disable instrumentation for IIS application pools
- **GAC operations**: Install assemblies to Global Assembly Cache
- **Registry configuration**: Set up environment variables and profiler CLSIDs

## Dependencies

None - standalone tool.

## Dependents

None - distributed separately for Windows installations.

## Artifacts

### Windows Executable
- **Name**: `Datadog.FleetInstaller.exe`
- **Platform**: win-x64
- **Target Framework**: net461
