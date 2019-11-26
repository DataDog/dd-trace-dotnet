# `dd-trace-dotnet` Release Notes


## Release 1.10.0

### New
- Add ASP.NET Core MVC 3 integration (#555)
- Add `IDbCommand` instrumentation to the ADO.NET integration (#562)

### Fixed
- Fix `DD_LOGS_INJECTION` crash with the ASP.NET integration (#551)
- Fix scope creation failing in ASP.NET MVC integration when URL is empty (#553)
- Fix crash when Profiler and NuGet package versions do not match (#570)
- Add missing tags to GraphQL integration (#547)
- Get the instrumented type instead of using build-time `typeof()` in `HttpMessageHandler` integration (#558)

### Builds and Tests
- Enable `Samples.MySql` test in CI (#548)
- Reduce permutations of minors in package versions tool (#545)
- Refactor "expectations" test framework (#554)
- Enable prerelease version tags (#556)
- Make timing and statistics tests less flaky (#559)
- Clean up `MockTraceAgent`, add event-based API (#501)
- Clean up project settings (#565)

### Other changes
- Add comments to prevent heart attacks (#563)

[All commits](https://github.com/DataDog/dd-trace-dotnet/compare/v1.9.0...v1.10.0)

[Full diff](https://github.com/DataDog/dd-trace-dotnet/compare/v1.9.0..v1.10.0)