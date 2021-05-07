module Datadog.Trace.Tests.Properties.Tests.ClrProfiler.Managed.IntegrationVersionRangeTests

open System
open FsCheck.Xunit
open Datadog.Trace.ClrProfiler

// adapted from /test/Datadog.Trace.ClrProfiler.Managed.Tests/IntegrationVersionRangeTests.cs

type Version =
    { Major: uint16
      Minor: uint16
      Patch: uint16 }

[<Property>]
let ``Minimum version two sets resets defaults for non specified parts`` (oldVer: Version) (newVer: Version) =
    let range = new IntegrationVersionRange()
    range.MinimumVersion <- $"{oldVer.Major}.{oldVer.Minor}.{oldVer.Patch}"
    range.MinimumVersion <- $"{newVer.Major}"
    newVer.Major = range.MinimumMajor &&
    0us = range.MinimumMinor &&
    0us = range.MinimumPatch

[<Property>]
let ``Parses minimum major`` (ver: Version) =
    let range = new IntegrationVersionRange()
    range.MinimumVersion <- $"{ver.Major}.{ver.Minor}"
    ver.Major = range.MinimumMajor

[<Property>]
let ``Parses minimum major and minor`` (ver: Version) =
    let range = new IntegrationVersionRange()
    range.MinimumVersion <- $"{ver.Major}.{ver.Minor}"
    ver.Major = range.MinimumMajor &&
    ver.Minor = range.MinimumMinor

[<Property>]
let ``Parses minimum major and minor and patch`` (ver: Version) =
    let range = new IntegrationVersionRange()
    range.MinimumVersion <- $"{ver.Major}.{ver.Minor}.{ver.Patch}"
    ver.Major = range.MinimumMajor &&
    ver.Minor = range.MinimumMinor &&
    ver.Patch = range.MinimumPatch

[<Property>]
let ``Maximum version two sets resets defaults for non specified parts`` (oldVer: Version) (newVer: Version) =
    let range = new IntegrationVersionRange()
    range.MaximumVersion <- $"{oldVer.Major}.{oldVer.Minor}.{oldVer.Patch}"
    range.MaximumVersion <- $"{newVer.Major}"
    newVer.Major = range.MaximumMajor &&
    UInt16.MaxValue = range.MaximumMinor &&
    UInt16.MaxValue = range.MaximumPatch

[<Property>]
let ``Parses maximum major`` (ver: Version) =
    let range = new IntegrationVersionRange()
    range.MaximumVersion <- $"{ver.Major}"
    ver.Major = range.MaximumMajor

[<Property>]
let ``Parses maximum major and minor`` (ver: Version) =
    let range = new IntegrationVersionRange()
    range.MaximumVersion <- $"{ver.Major}.{ver.Minor}"
    ver.Major = range.MaximumMajor &&
    ver.Minor = range.MaximumMinor

[<Property>]
let ``Parses maximum major and minor and patch`` (ver: Version) =
    let range = new IntegrationVersionRange()
    range.MaximumVersion <- $"{ver.Major}.{ver.Minor}.{ver.Patch}"
    ver.Major = range.MaximumMajor &&
    ver.Minor = range.MaximumMinor &&
    ver.Patch = range.MaximumPatch
