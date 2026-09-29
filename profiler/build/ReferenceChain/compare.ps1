param(
    [Parameter(Mandatory = $true)]
    [string] $BaselinePath,

    [Parameter(Mandatory = $true)]
    [string] $CandidatePath,

    [string] $OutputPath,

    [switch] $OpenDashboard
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Get-Median
{
    param([object[]] $Values)

    $numericValues = @($Values | Where-Object { $null -ne $_ } | ForEach-Object { [double]$_ })
    if ($numericValues.Count -eq 0)
    {
        return $null
    }

    $sorted = @($numericValues | Sort-Object)
    $middle = [int][Math]::Floor($sorted.Count / 2)
    if (($sorted.Count % 2) -eq 1)
    {
        return $sorted[$middle]
    }

    return ($sorted[$middle - 1] + $sorted[$middle]) / 2
}

function Get-MetricMedian
{
    param(
        [object] $Results,
        [string] $Scope,
        [string] $Property
    )

    if ($Scope -eq "run")
    {
        return Get-Median -Values @(
            $Results.Runs | ForEach-Object {
                $value = $_.PSObject.Properties[$Property]
                if ($null -ne $value) { [double]$value.Value }
            })
    }

    $perRun = @(
        foreach ($run in @($Results.Runs))
        {
            Get-Median -Values @(
                $run.Dumps | ForEach-Object {
                    $value = $_.PSObject.Properties[$Property]
                    if ($null -ne $value) { [double]$value.Value }
                })
        }
    )
    return Get-Median -Values $perRun
}

function Get-PercentChange
{
    param(
        $Baseline,
        $Candidate
    )

    if ($null -eq $Baseline -or $null -eq $Candidate -or $Baseline -eq 0)
    {
        return $null
    }

    return (($Candidate - $Baseline) / $Baseline) * 100
}

function Write-ComparisonDashboard
{
    param(
        [object] $Comparison,
        [string] $Path
    )

    $json = ($Comparison | ConvertTo-Json -Depth 10 -Compress).Replace("</", "<\/")
    $template = @'
<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<title>Reference-chain comparison</title>
<style>
:root{color-scheme:dark;--bg:#10141d;--panel:#181e2a;--line:#2b3547;--text:#eef2f8;--muted:#9aa8bc;--base:#77859a;--candidate:#65a7ff;--green:#57d39b;--red:#ff6b76;--orange:#ffb454}
*{box-sizing:border-box}body{margin:0;background:var(--bg);color:var(--text);font:14px/1.45 Segoe UI,Arial,sans-serif}main{max-width:1350px;margin:auto;padding:28px}
h1{font-size:30px;margin:0 0 5px}.subtitle,.muted{color:var(--muted)}.cards{display:grid;grid-template-columns:repeat(auto-fit,minmax(210px,1fr));gap:12px;margin:20px 0}.card,.panel{background:var(--panel);border:1px solid var(--line);border-radius:10px}.card{padding:15px}.card .value{font-size:25px;font-weight:650;margin-top:4px}.card .label{color:var(--muted)}
.improved{color:var(--green)}.regressed{color:var(--red)}.unchanged,.context{color:var(--muted)}.panel{padding:18px;margin-top:14px}.panel h2{font-size:18px;margin:0 0 14px}
.metric{display:grid;grid-template-columns:230px 1fr 115px;gap:14px;align-items:center;padding:11px 0;border-bottom:1px solid var(--line)}.metric:last-child{border:0}.bars{display:grid;gap:5px}.barrow{display:grid;grid-template-columns:75px 1fr 100px;align-items:center;gap:8px;color:var(--muted)}.track{height:12px;background:#252d3b;border-radius:8px;overflow:hidden}.bar{height:100%;border-radius:8px}.base{background:var(--base)}.candidate{background:var(--candidate)}
.delta{text-align:right;font-weight:650;font-size:16px}.warning{border-left:4px solid var(--orange);padding:12px 15px;background:#2a241a;margin-top:10px}.legend{display:flex;gap:20px;color:var(--muted);margin-bottom:8px}.dot{display:inline-block;width:9px;height:9px;border-radius:50%;margin-right:6px}
table{width:100%;border-collapse:collapse;font-variant-numeric:tabular-nums}th,td{padding:9px;border-bottom:1px solid var(--line);text-align:right}th:first-child,td:first-child{text-align:left}th{color:var(--muted)}.footer{color:var(--muted);margin-top:18px}
@media(max-width:750px){main{padding:16px}.metric{grid-template-columns:1fr}.delta{text-align:left}}
</style>
</head>
<body><main>
<h1 id="title"></h1><div class="subtitle" id="subtitle"></div>
<div class="cards" id="cards"></div>
<section class="panel" id="warningsPanel" style="display:none"><h2>Workload differences</h2><div id="warnings"></div></section>
<section class="panel"><h2>Median comparison</h2><div class="legend"><span><i class="dot" style="background:var(--base)"></i>Baseline</span><span><i class="dot" style="background:var(--candidate)"></i>Candidate</span></div><div id="metrics"></div></section>
<section class="panel"><h2>All metrics</h2><table id="table"></table></section>
<div class="footer" id="footer"></div>
</main>
<script>
const data=__DATA__;const rows=data.Metrics;const fmt=(v,d=2)=>v===null?'n/a':new Intl.NumberFormat(undefined,{maximumFractionDigits:d}).format(v);
document.getElementById('title').textContent=`${data.BaselineLabel} → ${data.CandidateLabel}`;
document.getElementById('subtitle').textContent=`Reference-chain benchmark comparison • medians are calculated per process, then across processes`;
const key=['WallTime','CpuTime','HeapDumpDuration','TraversalDuration'];
document.getElementById('cards').innerHTML=key.map(name=>{const r=rows.find(x=>x.Name===name);const change=r.PercentChange===null?'n/a':`${r.PercentChange>0?'+':''}${fmt(r.PercentChange,1)}%`;return `<div class="card"><div class="label">${r.Label}</div><div class="value ${r.Status}">${change}</div><div class="muted">${fmt(r.Baseline,2)} → ${fmt(r.Candidate,2)} ${r.Unit}</div></div>`}).join('');
if(data.Warnings.length){document.getElementById('warningsPanel').style.display='block';document.getElementById('warnings').innerHTML=data.Warnings.map(x=>`<div class="warning">${x}</div>`).join('')}
document.getElementById('metrics').innerHTML=rows.map(r=>{const values=[r.Baseline,r.Candidate].filter(x=>x!==null);const max=Math.max(...values,0.0001);const bp=r.Baseline===null?0:100*r.Baseline/max,cp=r.Candidate===null?0:100*r.Candidate/max;const delta=r.PercentChange===null?'n/a':`${r.PercentChange>0?'+':''}${fmt(r.PercentChange,1)}%`;return `<div class="metric"><div><strong>${r.Label}</strong><div class="muted">${r.Better==='context'?'workload/context':`${r.Better} is better`}</div></div><div class="bars"><div class="barrow"><span>Baseline</span><div class="track"><div class="bar base" style="width:${bp}%"></div></div><span>${fmt(r.Baseline)} ${r.Unit}</span></div><div class="barrow"><span>Candidate</span><div class="track"><div class="bar candidate" style="width:${cp}%"></div></div><span>${fmt(r.Candidate)} ${r.Unit}</span></div></div><div class="delta ${r.Status}">${delta}</div></div>`}).join('');
document.getElementById('table').innerHTML=`<thead><tr><th>Metric</th><th>Baseline</th><th>Candidate</th><th>Delta</th><th>Change</th></tr></thead><tbody>${rows.map(r=>{const delta=r.Baseline===null||r.Candidate===null?null:r.Candidate-r.Baseline;return `<tr><td>${r.Label}</td><td>${fmt(r.Baseline)} ${r.Unit}</td><td>${fmt(r.Candidate)} ${r.Unit}</td><td>${fmt(delta)} ${r.Unit}</td><td class="${r.Status}">${r.PercentChange===null?'n/a':`${r.PercentChange>0?'+':''}${fmt(r.PercentChange,1)}%`}</td></tr>`}).join('')}</tbody>`;
document.getElementById('footer').textContent=`Baseline: ${data.BaselinePath} • Candidate: ${data.CandidatePath} • Generated ${data.CreatedUtc}`;
</script></body></html>
'@

    $template.Replace("__DATA__", $json) | Set-Content -LiteralPath $Path -Encoding UTF8
}

$baselineDirectory = (Resolve-Path -LiteralPath $BaselinePath).Path
$candidateDirectory = (Resolve-Path -LiteralPath $CandidatePath).Path
$baselineResultsPath = Join-Path $baselineDirectory "results.json"
$candidateResultsPath = Join-Path $candidateDirectory "results.json"
if (-not (Test-Path -LiteralPath $baselineResultsPath -PathType Leaf))
{
    throw "Missing '$baselineResultsPath'. Run run.ps1 for the baseline first."
}
if (-not (Test-Path -LiteralPath $candidateResultsPath -PathType Leaf))
{
    throw "Missing '$candidateResultsPath'. Run run.ps1 for the candidate first."
}

$baseline = Get-Content -LiteralPath $baselineResultsPath -Raw | ConvertFrom-Json
$candidate = Get-Content -LiteralPath $candidateResultsPath -Raw | ConvertFrom-Json

$definitions = @(
    [pscustomobject]@{ Name = "WallTime"; Label = "Process wall time"; Scope = "run"; Property = "WallTimeMs"; Scale = 0.001; Unit = "s"; Better = "lower" }
    [pscustomobject]@{ Name = "CpuTime"; Label = "Process CPU time"; Scope = "run"; Property = "CpuTimeMs"; Scale = 0.001; Unit = "s"; Better = "lower" }
    [pscustomobject]@{ Name = "CpuPercent"; Label = "Average CPU usage"; Scope = "run"; Property = "AverageCpuPercent"; Scale = 1; Unit = "%"; Better = "context" }
    [pscustomobject]@{ Name = "PrivateMemory"; Label = "Private memory"; Scope = "run"; Property = "PrivateBytes"; Scale = (1 / 1MB); Unit = "MiB"; Better = "lower" }
    [pscustomobject]@{ Name = "GcPause"; Label = "GC pause time"; Scope = "run"; Property = "GcPauseTimeMs"; Scale = 0.001; Unit = "s"; Better = "lower" }
    [pscustomobject]@{ Name = "HeapDumpDuration"; Label = "Heap dump duration"; Scope = "dump"; Property = "HeapDumpDurationMs"; Scale = 0.001; Unit = "s"; Better = "lower" }
    [pscustomobject]@{ Name = "TraversalDuration"; Label = "Reference traversal duration"; Scope = "dump"; Property = "TraversalDurationMs"; Scale = 0.001; Unit = "s"; Better = "lower" }
    [pscustomobject]@{ Name = "SurvivingObjects"; Label = "Surviving objects"; Scope = "dump"; Property = "HeapObjects"; Scale = 1; Unit = "objects"; Better = "context" }
    [pscustomobject]@{ Name = "HeapBytes"; Label = "Surviving bytes"; Scope = "dump"; Property = "HeapBytes"; Scale = (1 / 1MB); Unit = "MiB"; Better = "context" }
    [pscustomobject]@{ Name = "ObservedRoots"; Label = "Observed roots"; Scope = "dump"; Property = "ObservedRoots"; Scale = 1; Unit = "roots"; Better = "context" }
    [pscustomobject]@{ Name = "ParsedRoots"; Label = "Parsed roots"; Scope = "dump"; Property = "TraversalRoots"; Scale = 1; Unit = "roots"; Better = "context" }
    [pscustomobject]@{ Name = "TraversedObjects"; Label = "Traversed objects"; Scope = "dump"; Property = "TraversalObjects"; Scale = 1; Unit = "objects"; Better = "context" }
    [pscustomobject]@{ Name = "TraversalEdges"; Label = "Examined references"; Scope = "dump"; Property = "TraversalEdges"; Scale = 1; Unit = "references"; Better = "context" }
    [pscustomobject]@{ Name = "TreeNodes"; Label = "Reference-tree nodes"; Scope = "dump"; Property = "TreeNodes"; Scale = 1; Unit = "nodes"; Better = "context" }
    [pscustomobject]@{ Name = "VisitedMemory"; Label = "Visited-set memory"; Scope = "dump"; Property = "VisitedBytes"; Scale = (1 / 1MB); Unit = "MiB"; Better = "lower" }
    [pscustomobject]@{ Name = "TotalSizeCalls"; Label = "GetObjectSize2 calls"; Scope = "dump"; Property = "TotalSizeCalls"; Scale = 1; Unit = "calls"; Better = "lower" }
    [pscustomobject]@{ Name = "ScannableSizeCalls"; Label = "Scannable first-visit size calls"; Scope = "dump"; Property = "FirstVisitScannableSizeCalls"; Scale = 1; Unit = "calls"; Better = "context" }
    [pscustomobject]@{ Name = "LeafSizeCalls"; Label = "Leaf size calls"; Scope = "dump"; Property = "FirstVisitLeafSizeCalls"; Scale = 1; Unit = "calls"; Better = "lower" }
    [pscustomobject]@{ Name = "RevisitSizeCalls"; Label = "Revisit size calls"; Scope = "dump"; Property = "RevisitSizeCalls"; Scale = 1; Unit = "calls"; Better = "lower" }
    [pscustomobject]@{ Name = "GetClassFirstVisit"; Label = "GetClassFromObject first-visit calls"; Scope = "dump"; Property = "GetClassFromObjectFirstVisitCalls"; Scale = 1; Unit = "calls"; Better = "lower" }
    [pscustomobject]@{ Name = "GetClassRevisit"; Label = "GetClassFromObject revisit calls"; Scope = "dump"; Property = "GetClassFromObjectRevisitCalls"; Scale = 1; Unit = "calls"; Better = "lower" }
    [pscustomobject]@{ Name = "DuplicateRoots"; Label = "Duplicate root addresses"; Scope = "dump"; Property = "DuplicateRootAddresses"; Scale = 1; Unit = "roots"; Better = "context" }
)

$metricRows = @(
    foreach ($definition in $definitions)
    {
        $baselineRaw = Get-MetricMedian -Results $baseline -Scope $definition.Scope -Property $definition.Property
        $candidateRaw = Get-MetricMedian -Results $candidate -Scope $definition.Scope -Property $definition.Property
        $baselineValue = if ($null -eq $baselineRaw) { $null } else { $baselineRaw * $definition.Scale }
        $candidateValue = if ($null -eq $candidateRaw) { $null } else { $candidateRaw * $definition.Scale }
        $percentChange = Get-PercentChange -Baseline $baselineValue -Candidate $candidateValue
        $status = "context"
        if ($definition.Better -ne "context" -and $null -ne $percentChange)
        {
            if ([Math]::Abs($percentChange) -lt 1)
            {
                $status = "unchanged"
            }
            elseif (($definition.Better -eq "lower" -and $percentChange -lt 0) -or
                    ($definition.Better -eq "higher" -and $percentChange -gt 0))
            {
                $status = "improved"
            }
            else
            {
                $status = "regressed"
            }
        }

        [pscustomobject][ordered]@{
            Name = $definition.Name
            Label = $definition.Label
            Unit = $definition.Unit
            Better = $definition.Better
            Baseline = $(if ($null -eq $baselineValue) { $null } else { [Math]::Round($baselineValue, 4) })
            Candidate = $(if ($null -eq $candidateValue) { $null } else { [Math]::Round($candidateValue, 4) })
            PercentChange = $(if ($null -eq $percentChange) { $null } else { [Math]::Round($percentChange, 2) })
            Status = $status
        }
    }
)

$warnings = @()
$rootWorkloadMetric = if ([int]$baseline.SchemaVersion -ge 2 -and [int]$candidate.SchemaVersion -ge 2) { "ObservedRoots" } else { "ParsedRoots" }
foreach ($metricName in @("SurvivingObjects", "HeapBytes", $rootWorkloadMetric, "TraversedObjects"))
{
    $metric = $metricRows | Where-Object Name -eq $metricName
    if ($null -ne $metric.PercentChange -and [Math]::Abs($metric.PercentChange) -gt 5)
    {
        $warnings += "$($metric.Label) changed by $($metric.PercentChange)%. Timing may reflect a different amount of work."
    }
}

if ([string]::IsNullOrWhiteSpace($OutputPath))
{
    $safeBaselineLabel = $baseline.Label -replace '[^a-zA-Z0-9_.-]', '-'
    $OutputPath = Join-Path $candidateDirectory "comparison-vs-$safeBaselineLabel.html"
}
else
{
    $OutputPath = [IO.Path]::GetFullPath($OutputPath)
}

$comparison = [pscustomobject][ordered]@{
    SchemaVersion = 2
    CreatedUtc = [DateTime]::UtcNow.ToString("O")
    BaselineLabel = $baseline.Label
    CandidateLabel = $candidate.Label
    BaselinePath = $baselineDirectory
    CandidatePath = $candidateDirectory
    BaselineRuns = @($baseline.Runs).Count
    CandidateRuns = @($candidate.Runs).Count
    Warnings = $warnings
    Metrics = $metricRows
}

$comparisonJsonPath = [IO.Path]::ChangeExtension($OutputPath, ".json")
$comparison | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $comparisonJsonPath -Encoding UTF8
Write-ComparisonDashboard -Comparison $comparison -Path $OutputPath

Write-Host "Comparison: $comparisonJsonPath"
Write-Host "Dashboard:  $OutputPath"
if ($OpenDashboard)
{
    Start-Process $OutputPath
}
