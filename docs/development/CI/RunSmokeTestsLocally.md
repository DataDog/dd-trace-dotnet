To run the smoke tests locally, you can do the following.

1. Choose a `SmokeTestCategory` and `SmokeTestScenario` to run.
2. Build (or download) the required artifact for the test category you wish to run. Consult `ultimate-pipeline.yml` for the appropriate artifact.
3. Move the artifact to `./artifacts`.
4. Ensure Docker Desktop is running (in `Linux` mode for Linux categories and `Windows` mode for Windows categories).
5. Run the following command, passing in your chosen  `SmokeTestCategory` and `SmokeTestScenario`. The examples below use `LinuxX64Installer` and `ubuntu_10_0-noble` as the category and scenario:

**Windows (PowerShell):**
```powershell
tracer\build.cmd RunArtifactSmokeTests CheckSmokeTestsForErrors ExtractMetricsFromLogs -SmokeTestCategory 'LinuxX64Installer' -SmokeTestScenario 'ubuntu_10_0-noble' --Artifacts './artifacts'
```

**macOS/Linux:**
```bash
./tracer/build.sh RunArtifactSmokeTests CheckSmokeTestsForErrors ExtractMetricsFromLogs -SmokeTestCategory 'LinuxArm64Installer' -SmokeTestScenario 'ubuntu_10_0-noble' --Artifacts './artifacts'
```

This will build the smoke test image, pull the test-agent (if not already available), set up a session, and run snapshot tests. Any failures should be shown in the logs