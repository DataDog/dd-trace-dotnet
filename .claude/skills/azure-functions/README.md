# Azure Functions Skill

A Claude Code skill for working with Azure Functions instrumented with Datadog.AzureFunctions.

## Usage

Invoke the skill with `/azure-functions` followed by an optional command:

```
/azure-functions                    # Interactive mode - guided workflow
/azure-functions build              # Build Datadog.AzureFunctions NuGet package
/azure-functions deploy [app-name]  # Deploy to Azure Function App
/azure-functions test [app-name]    # Trigger and verify function
/azure-functions logs [app-name]    # Download and analyze logs
/azure-functions trace [trace-id]   # Analyze specific trace in Datadog
```

## Features

### Build
- Builds `Datadog.AzureFunctions` NuGet package with your changes
- Clears NuGet cache
- Publishes to bundle folder
- Copies to `D:\temp\nuget`

### Deploy
- Deploys sample app to Azure Function App
- Supports all test apps in resource group `lucas.pimentel`
- Default: `lucasp-premium-linux-isolated-aspnet`
- Reminds to wait for worker restart

### Test
- Triggers HTTP endpoint
- Captures execution timestamp (for log filtering)
- Verifies HTTP 200 response

### Logs
- Downloads logs from Azure
- Extracts to `D:\temp\logs-{timestamp}.zip`
- Provides timestamp-based filtering commands
- Identifies host vs worker processes
- Verifies tracer version
- Analyzes trace context flow

### Trace
- Queries Datadog API for specific trace
- Shows span hierarchy
- Verifies parent-child relationships
- Checks process tags (`host` vs `worker`)

## Files

- **SKILL.md** - Main skill definition with workflow steps
- **log-analysis-guide.md** - Detailed log analysis patterns and grep examples
- **scripts-reference.md** - Reusable bash/PowerShell scripts for common tasks
- **README.md** - This file

## Interactive Mode

When invoked without arguments (`/azure-functions`), the skill guides you through:

1. Understanding your goal (new feature, bug fix, trace verification)
2. Building the NuGet package
3. Selecting target app
4. Deploying to Azure
5. Waiting for worker restart
6. Testing function execution
7. Downloading logs
8. Analyzing results based on your goal
9. Running verification checklist

## Quick Reference

### Test Function Apps

All apps in resource group `lucas.pimentel`, Canada Central:

- **lucasp-premium-linux-isolated-aspnet** (primary) - .NET 8 Isolated + ASP.NET Core
- lucasp-premium-linux-isolated - .NET 8 Isolated (no ASP.NET Core)
- lucasp-premium-linux-inproc - .NET 6 In-Process
- lucasp-premium-windows-isolated-aspnet - Windows + ASP.NET Core
- lucasp-premium-windows-isolated - Windows (no ASP.NET Core)
- lucasp-premium-windows-inproc - Windows In-Process
- lucasp-consumption-windows-isolated - Windows Consumption
- lucasp-flex-consumption-isolated - Flex Consumption

### Sample Applications

- Primary: `D:\source\datadog\serverless-dev-apps\azure\functions\dotnet\isolated-dotnet8-aspnetcore`
- All: `D:\source\datadog\serverless-dev-apps\azure\functions\dotnet`

### Temporary Directories

- NuGet packages: `D:\temp\nuget`
- Log downloads: `D:\temp\logs-*.zip`
- Trace payloads: `D:\temp\trace_payload_*.json`

## Documentation References

- `docs/development/AzureFunctions.md` - Complete Azure Functions integration guide
- `docs/development/for-ai/AzureFunctions-Architecture.md` - Architecture deep dive
- `D:\source\datadog\CLAUDE.md` - Azure Functions testing workflow

## Examples

### Build and deploy to primary app
```
/azure-functions build
/azure-functions deploy
```

### Test specific app and download logs
```
/azure-functions test lucasp-premium-linux-isolated
/azure-functions logs lucasp-premium-linux-isolated
```

### Analyze specific trace
```
/azure-functions trace 68e948220000000047fef7bad8bb854e
```

### Full workflow (interactive)
```
/azure-functions
```

## Verification Checklist

After deployment and testing, the skill helps verify:

- [ ] Function responds successfully (HTTP 200)
- [ ] Worker loaded correct tracer version
- [ ] Host and worker spans share same trace ID
- [ ] Span parent-child relationships are correct
- [ ] Process tags are correct (`aas.function.process:host` or `worker`)
- [ ] No error logs at execution timestamp
- [ ] AsyncLocal context flows correctly (if debugging context issues)

## Common Troubleshooting

The skill provides guidance for:

- Function not responding after deployment
- Wrong tracer version after deployment
- Traces not appearing in Datadog
- Separate traces (span parenting issues)
- Missing debug logs
- AsyncLocal context flow issues

## Tips

- Always use timestamp filtering when analyzing logs
- Verify tracer version before investigating behavior
- Follow trace IDs from host to worker
- Check span parent-child relationships
- Download logs after each test execution
- Enable debug logging for detailed investigation
