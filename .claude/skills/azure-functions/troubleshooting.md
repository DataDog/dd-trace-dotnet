# Common Troubleshooting

**General guidance**: For environment variable configuration issues, see [environment-variables.md](environment-variables.md) for complete reference on required, recommended, and debugging variables.

## Function Not Responding

**First, check if the app is running**:
```powershell
$envCheck = ./.claude/skills/azure-functions/Test-EnvVars.ps1 -AppName "<app-name>" -ResourceGroup "<resource-group>"
if ($envCheck.State -ne "Running") {
    Write-Host "App is '$($envCheck.State)' — starting it..."
    az functionapp start --name <app-name> --resource-group <resource-group>
}
```

If the app is running but not responding:
```bash
# Restart function app
az functionapp restart --name <app-name> --resource-group <resource-group>
```

## Deployment Fails

If `az functionapp deployment source config-zip` fails:
```bash
# Check recent deployment status
az functionapp deployment list \
  --name <app-name> \
  --resource-group <resource-group> \
  --query "[0].{status:status, message:message, startTime:startTime}" -o table
```

Common causes:
- **Auth expired**: Run `az login` and retry
- **App not running**: Start it first with `az functionapp start --name <app-name> --resource-group <resource-group>`
- **Zip structure wrong**: Verify `host.json` exists at root of publish output (check the temp publish dir on failure)
- **Build errors**: Check `dotnet publish` output

## Wrong Tracer Version After Deployment

```bash
# Check all worker initializations
grep "Assembly metadata" LogFiles/datadog/dotnet-tracer-managed-dotnet-*.log
```

If old version appears:
1. Rebuild the tracer: `dotnet build tracer/src/Datadog.Trace -c Release -f net6.0`
2. Re-run `Deploy-AzureFunction.ps1` (or use `-SkipTracerBuild` if you just built it manually)

## Traces Not Appearing in Datadog

**Verify all required environment variables** (including DD_API_KEY, profiler paths, etc.):
```powershell
./.claude/skills/azure-functions/Test-EnvVars.ps1 -AppName "<app-name>" -ResourceGroup "<resource-group>" -IncludeRecommended
```

If all env vars pass, check worker initialization in logs:
```bash
grep "Datadog Tracer initialized" LogFiles/datadog/dotnet-tracer-managed-dotnet-*.log
```

**Complete reference**: See [environment-variables.md](environment-variables.md) for all available variables.

## Separate Traces (Parenting Issue)
1. Get trace ID from host logs at execution timestamp
2. Search worker logs for same trace ID
3. If not found → worker created separate trace
4. Look for worker spans with `p_id: null` instead of parent IDs matching host spans
5. Enable debug logging: `az functionapp config appsettings set --name <app> --resource-group <resource-group> --settings DD_TRACE_DEBUG=1`
6. Re-test and analyze debug messages about AsyncLocal context flow
