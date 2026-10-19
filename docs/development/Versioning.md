# Versioning

The SDK version is defined in exactly one committed place: [`version.props`](../../version.props)
at the repository root. Everything else - managed assembly versions, native version headers,
Windows resource scripts, CMake project versions, the MSI, NuGet package versions and the Linux
packages - derives from it at build time.

## The committed input

```xml
<DD_VersionPrefix>3.56.0</DD_VersionPrefix>
<DD_IsPrerelease>false</DD_IsPrerelease>
<DD_LatestReleasedVersion>3.55.0</DD_LatestReleasedVersion>
```

`DD_LatestReleasedVersion` is deliberately behind `DD_VersionPrefix`. It is used only by the sample
projects under `tracer/samples`, which have to reference a package that actually exists on
nuget.org, so they lag the source by one release.

Keep this file free of logic: CMake parses it with a regular expression as well as MSBuild
importing it.

## Build channels

| Channel | When | Version |
|---|---|---|
| `release` | CI building `master`, `main`, `release/*`, `hotfix/*` or a `v*` tag | `3.56.0` |
| `dev` | everything else - PR branches, feature branches, local builds | `3.56.0-dev.g1a2b3c4d` |

The dev token is the first 8 characters of the commit SHA, prefixed with `g`. The prefix is not
decoration: a bare hex SHA can be all digits with a leading zero, which is an invalid semver
prerelease identifier that NuGet rejects.

A release build's output is byte-identical to what the repository produced before this mechanism
existed.

## Version shapes

`build/Version.props` derives these from the prefix, the suffix and the revision. Pick the right one
for the job - the distinctions are load-bearing.

| Property | Release | Dev (CI) | Dev (local) | Use for |
|---|---|---|---|---|
| `DD_ThreePartVersion` | `3.56.0` | `3.56.0` | `3.56.0` | CMake `project(VERSION)`, MSI `InstallerVersion`, native `PROFILER_VERSION` |
| `DD_FourPartVersion` | `3.56.0.0` | `3.56.0.7124` | `3.56.0.0` | `AssemblyVersion`/`FileVersion`, the strong name the loader and the native tracer resolve, Win32 `FILEVERSION` |
| `DD_SemVer` | `3.56.0` | `3.56.0-dev.g1a2b3c4d` | `3.56.0-dev.g1a2b3c4d` | NuGet versions, `InformationalVersion`, Win32 `ProductVersion`, package file names |
| `DD_RpmVersion` | `3.56.0` | `3.56.0~dev.g1a2b3c4d` | `3.56.0~dev.g1a2b3c4d` | deb/rpm only |
| `DD_ReportedVersion` | `3.56.0.0` | `3.56.0-dev.g1a2b3c4d` | `3.56.0-dev.g1a2b3c4d` | the version reported to Datadog |

**The invariant:** anything that is an assembly identity, a Win32 numeric resource field, a CMake
`project()` version or an MSI `ProductVersion` uses a `*ThreePart*` or `*FourPart*` shape and can
never see the suffix. Only `DD_SemVer`, `DD_RpmVersion` and `DD_ReportedVersion` carry it. This is
what keeps the following correct without per-site special casing:

- Windows Installer requires `ProductVersion` to be numeric `major.minor.build`, and `MajorUpgrade`
  needs it to increase monotonically
- CMake `project(VERSION)` is numeric only
- Win32 `FILEVERSION`/`PRODUCTVERSION` are four 16-bit integers
- `AssemblyProperty::WithVersion` takes four `USHORT`s
- `Instrumentation.cs` compares the native and managed versions for an exact string match
- `dd-dotnet` parses the native DLL's `FileVersion` with `Version.TryParse`
- `iast_util`'s `GetDatadogVersion` is parsed into major/minor/build/rev for aspect version ranges

## The assembly version revision

On a **CI** dev build the 4th field is the commit height, so two dev builds have distinct assembly
identities and can, for example, coexist in the GAC. On a release build, and on any local build, it
is `0`.

Commit height rather than a CI build id, because the field is a `USHORT` and Azure DevOps build ids
for this repository are long past 65535, and because GitLab builds the same commit independently of
Azure DevOps, so a per-pipeline number would give one commit two different assembly versions.

### Why local builds keep `.0`

This is deliberate, and the reason matters if you are ever tempted to change it. The native tracer
and the managed loader both resolve `Datadog.Trace` by its exact strong name, and
`ManagedProfilerAssemblyResolver` returns `null` on any mismatch. If the revision moved with `HEAD`
locally, then committing and rebuilding only the managed side - `CompileManagedSrc`, or a plain
`dotnet build` - would leave a monitoring home whose native half looks for `3.56.0.<N>` while the
managed half is `3.56.0.<N+1>`. The tracer would simply fail to load, with nothing but a debug log
to say why.

Keeping it at `0` locally means `dotnet build`, a partial Nuke build and a full Nuke build all agree
on the assembly version, so they can be mixed freely. CI always builds from clean, so it cannot hit
that problem. Either way the semver still carries the commit SHA, which is what actually identifies
the build.

Be clear about what the revision does not buy even on CI: two branches at the same height collide,
and the number carries no commit traceability.

## Changing the version without a commit

```sh
./tracer/build.sh PrintVersion                       # show every shape
./tracer/build.sh GenerateVersionFiles --Version 3.57.0
./tracer/build.sh BuildTracerHome --VersionSuffix rc1 # note: no leading dash, Nuke rejects it
./tracer/build.sh BuildTracerHome --BuildChannel release
```

`--VersionSuffix none` forces an empty suffix. The same values can come from the `Version`,
`VersionSuffix` and `BuildChannel` environment variables, which is how the GitLab job passes them
into its build container.

## How it fits together

1. `DdVersion.cs` (Nuke) resolves the channel and the dev token - it needs git and the CI
   environment, which MSBuild cannot reach at evaluation time, and evaluation time is exactly when
   `$(Version)` has to be known for NuGet packaging and `GenerateAssemblyInfo`.
2. It writes `artifacts/version/Version.g.props` (the resolved prefix, suffix and revision),
   `version.txt` and `version.json`.
3. `build/Version.props` imports `version.props` then that generated file, and derives every shape.
   It is imported by `tracer/`, `profiler/` and `shared/Directory.Build.props`, so every project in
   the repository sees the properties.
4. `build/Version.targets` generates `shared/src/native-src/dd_version.g.h` for the native builds and
   `DatadogVersion.g.cs` for projects that set `DD_GenerateVersionSource`.
5. `build/cmake/DdVersion.cmake` reads `version.props` for the numeric triple, and fails loudly if
   the generated header is missing rather than compiling a stale version in.

## Building without Nuke

`dotnet build` and IDE builds work without Nuke ever having run:

- `build/Version.targets` is reached through the `Directory.Build.targets` chain, so any project
  that compiles generates what it needs. A managed build writes `dd_version.g.h` as well, since the
  target also hooks `CoreCompile`.
- With no `artifacts/version/Version.g.props`, `build/Version.props` falls back to the plain release
  shape - `3.56.0`, assembly version `3.56.0.0` - which is identical to what the repository produced
  before any of this existed, and to what a local Nuke build produces.
- `cmake -B build -S .` on a fresh clone invokes the generator itself rather than failing, so a
  CMake-driven native build does not require Nuke first. (It does still require the managed loader
  to have been built, which was already true.)

The one thing to know: after a Nuke build has written `Version.g.props`, a later `dotnet build`
picks up that file, so it will reuse the suffix resolved at that point rather than re-deriving it
from the current `HEAD`. Run `GenerateVersionFiles` if you want it refreshed. This only affects the
`-dev.<sha>` suffix, never the assembly version, so it cannot break a partial rebuild.

The managed constants are generated by an MSBuild target rather than a source generator because
`Datadog.Trace.csproj` sets `EmitCompilerGeneratedFiles` with `CompilerGeneratedFilesOutputPath`
pointing at the committed `Generated/` folder; a source generator would rewrite a tracked file on
every build.

## Bumping the version

`./tracer/build.sh UpdateVersion --NewVersion 3.57.0 --NewIsPrerelease false` rewrites
`version.props` and the sample projects. A version bump PR touches 13 files: `version.props`,
`docs/CHANGELOG.md` and the 11 samples.

## Checking it

`./tracer/build.sh VerifyVersionConsistency` asserts that the generated files and the version
MSBuild evaluates all agree with what the build resolved, and that the sample literals match
`DD_LatestReleasedVersion`. Run it on every PR - it is what replaced the hand-maintained list of
files the old regex rewriter had to visit.
