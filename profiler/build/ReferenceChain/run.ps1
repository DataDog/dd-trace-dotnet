[CmdletBinding(PositionalBinding = $false)]
param(
    [Parameter(Mandatory = $true)]
    [string] $Label,

    [ValidateSet("Debug", "Release")]
    [string] $Configuration = "Release",

    [ValidateSet("x64", "x86")]
    [string] $Architecture = "x64",

    [string] $Framework = "net10.0",

    [ValidateRange(1, 100)]
    [int] $RunCount = 5,

    [ValidateRange(1, 86400)]
    [int] $DurationSeconds = 50,

    [ValidateRange(1, 3600)]
    [int] $SnapshotIntervalSeconds = 10,

    [string] $OutputRoot,

    [string] $ProfilerPath,

    [string] $ApplicationPath,

    [switch] $OpenDashboard,

    [switch] $RemoveLabel,

    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]] $RemainingArguments
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

foreach ($argument in @($RemainingArguments | Where-Object { $null -ne $_ }))
{
    if ($argument -eq "--RemoveLabel")
    {
        $RemoveLabel = $true
    }
    else
    {
        throw "Unknown argument '$argument'."
    }
}

function Get-ManagedMetrics
{
    param([string] $Path)

    $result = @{}
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf))
    {
        return $result
    }

    foreach ($entry in @(Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json))
    {
        foreach ($property in $entry.PSObject.Properties)
        {
            $result[$property.Name] = [double]$property.Value
        }
    }

    return $result
}

function Get-NativeDumpRecords
{
    param([string] $LogDirectory)

    $heapPattern = 'Reference chain benchmark heap: duration_ms=(?<duration>\d+), objects=(?<objects>\d+), bytes=(?<bytes>\d+)'
    $traversalPattern = 'Reference chain benchmark traversal: duration_ms=(?<duration>\d+), roots=(?<roots>\d+), objects=(?<objects>\d+), stack_capacity=(?<stack>\d+), faults=(?<faults>\d+), stop_reason=(?<stop>[^,]+), visited_peak_entries=(?<peak>\d+), visited_bytes=(?<visited>\d+), visited_buckets=(?<buckets>\d+), visited_grows=(?<grows>\d+), edges=(?<edges>\d+), first_visit_refs=(?<firstVisitRefs>\d+), revisit_refs=(?<revisitRefs>\d+), get_class_first_visit=(?<getClassFirst>\d+), get_class_revisit=(?<getClassRevisit>\d+), raw_class_reads=(?<rawClassReads>\d+), tree_nodes=(?<treeNodes>\d+)'
    $sizeCallsPattern = 'Reference chain benchmark GetObjectSize2: root=(?<root>\d+), static_root=(?<staticRoot>\d+), root_scannable=(?<rootScannable>\d+), root_leaf=(?<rootLeaf>\d+), static_root_scannable=(?<staticRootScannable>\d+), static_root_leaf=(?<staticRootLeaf>\d+), first_visit_scannable=(?<firstScannable>\d+), first_visit_leaf=(?<firstLeaf>\d+), revisit=(?<revisit>\d+), failed_or_zero=(?<failed>\d+)'
    $rootsPattern = 'Reference chain benchmark roots: stack=(?<stack>\d+), static=(?<static>\d+), finalizer=(?<finalizer>\d+), handle=(?<handle>\d+), pinning=(?<pinning>\d+), conditional_weak_table=(?<cwt>\d+), com=(?<com>\d+), other=(?<other>\d+), unknown=(?<unknown>\d+)'
    $observedRootsPattern = 'Reference chain benchmark roots observed: stack=(?<stack>\d+), static=(?<static>\d+), finalizer=(?<finalizer>\d+), handle=(?<handle>\d+), pinning=(?<pinning>\d+), conditional_weak_table=(?<cwt>\d+), com=(?<com>\d+), other=(?<other>\d+), unknown=(?<unknown>\d+)'
    $rootDecisionsPattern = 'Reference chain benchmark root decisions: observed=(?<observed>\d+), traversal_calls=(?<traversalCalls>\d+), duplicate_addresses=(?<duplicates>\d+), interior_skipped=(?<interior>\d+), weak_observed=(?<weak>\d+), class_lookup_failed=(?<classFailed>\d+), size_lookup_failed=(?<sizeFailed>\d+), root_get_class_calls=(?<getClass>\d+)'
    $rootWorkPattern = 'Reference chain benchmark root work: category=(?<category>[^,]+), roots=(?<roots>\d+), objects=(?<objects>\d+), edges=(?<edges>\d+), duration_ms=(?<duration>\d+), max_objects=(?<maxObjects>\d+), max_edges=(?<maxEdges>\d+), max_duration_ms=(?<maxDuration>\d+)'

    $nativeLogs = @(Get-ChildItem -LiteralPath $LogDirectory -Filter "DD-DotNet-Profiler-Native-Samples.Computer01-*.log" | Sort-Object LastWriteTime)
    if ($nativeLogs.Count -eq 0)
    {
        throw "No native profiler log found in '$LogDirectory'."
    }

    $lines = @($nativeLogs | ForEach-Object { Get-Content -LiteralPath $_.FullName })

    $heap = @(
        foreach ($line in $lines)
        {
            if ($line -match $heapPattern)
            {
                [pscustomobject]@{
                    DurationMs = [uint64]$Matches.duration
                    Objects = [uint64]$Matches.objects
                    Bytes = [uint64]$Matches.bytes
                }
            }
        }
    )

    $traversal = @(
        foreach ($line in $lines)
        {
            if ($line -match $traversalPattern)
            {
                [pscustomobject]@{
                    DurationMs = [uint64]$Matches.duration
                    Roots = [uint64]$Matches.roots
                    Objects = [uint64]$Matches.objects
                    StackCapacity = [uint64]$Matches.stack
                    Faults = [uint64]$Matches.faults
                    StopReason = $Matches.stop
                    VisitedPeakEntries = [uint64]$Matches.peak
                    VisitedBytes = [uint64]$Matches.visited
                    VisitedBuckets = [uint64]$Matches.buckets
                    VisitedGrows = [uint64]$Matches.grows
                    Edges = [uint64]$Matches.edges
                    FirstVisitReferences = [uint64]$Matches.firstVisitRefs
                    RevisitReferences = [uint64]$Matches.revisitRefs
                    GetClassFromObjectFirstVisitCalls = [uint64]$Matches.getClassFirst
                    GetClassFromObjectRevisitCalls = [uint64]$Matches.getClassRevisit
                    RawMethodTableClassReads = [uint64]$Matches.rawClassReads
                    TreeNodes = [uint64]$Matches.treeNodes
                }
            }
        }
    )

    $sizeCalls = @(
        foreach ($line in $lines)
        {
            if ($line -match $sizeCallsPattern)
            {
                [pscustomobject]@{
                    Root = [uint64]$Matches.root
                    StaticRoot = [uint64]$Matches.staticRoot
                    RootScannable = [uint64]$Matches.rootScannable
                    RootLeaf = [uint64]$Matches.rootLeaf
                    StaticRootScannable = [uint64]$Matches.staticRootScannable
                    StaticRootLeaf = [uint64]$Matches.staticRootLeaf
                    FirstVisitScannable = [uint64]$Matches.firstScannable
                    FirstVisitLeaf = [uint64]$Matches.firstLeaf
                    Revisit = [uint64]$Matches.revisit
                    FailedOrZero = [uint64]$Matches.failed
                }
            }
        }
    )

    $roots = @(
        foreach ($line in $lines)
        {
            if ($line -match $rootsPattern)
            {
                [pscustomobject]@{
                    Stack = [uint64]$Matches.stack
                    Static = [uint64]$Matches.static
                    Finalizer = [uint64]$Matches.finalizer
                    Handle = [uint64]$Matches.handle
                    Pinning = [uint64]$Matches.pinning
                    ConditionalWeakTable = [uint64]$Matches.cwt
                    Com = [uint64]$Matches.com
                    Other = [uint64]$Matches.other
                    Unknown = [uint64]$Matches.unknown
                }
            }
        }
    )

    $observedRoots = @(
        foreach ($line in $lines)
        {
            if ($line -match $observedRootsPattern)
            {
                [pscustomobject]@{
                    Stack = [uint64]$Matches.stack
                    Static = [uint64]$Matches.static
                    Finalizer = [uint64]$Matches.finalizer
                    Handle = [uint64]$Matches.handle
                    Pinning = [uint64]$Matches.pinning
                    ConditionalWeakTable = [uint64]$Matches.cwt
                    Com = [uint64]$Matches.com
                    Other = [uint64]$Matches.other
                    Unknown = [uint64]$Matches.unknown
                }
            }
        }
    )

    $rootDecisions = @(
        foreach ($line in $lines)
        {
            if ($line -match $rootDecisionsPattern)
            {
                [pscustomobject]@{
                    Observed = [uint64]$Matches.observed
                    TraversalCalls = [uint64]$Matches.traversalCalls
                    DuplicateAddresses = [uint64]$Matches.duplicates
                    InteriorSkipped = [uint64]$Matches.interior
                    WeakObserved = [uint64]$Matches.weak
                    ClassLookupFailed = [uint64]$Matches.classFailed
                    SizeLookupFailed = [uint64]$Matches.sizeFailed
                    RootGetClassFromObjectCalls = [uint64]$Matches.getClass
                }
            }
        }
    )

    $rootWork = @(
        foreach ($line in $lines)
        {
            if ($line -match $rootWorkPattern)
            {
                [pscustomobject]@{
                    Category = $Matches.category
                    Roots = [uint64]$Matches.roots
                    Objects = [uint64]$Matches.objects
                    Edges = [uint64]$Matches.edges
                    DurationMs = [uint64]$Matches.duration
                    MaxObjects = [uint64]$Matches.maxObjects
                    MaxEdges = [uint64]$Matches.maxEdges
                    MaxDurationMs = [uint64]$Matches.maxDuration
                }
            }
        }
    )

    $rootCategoryCount = 9
    $recordCounts = @($heap.Count, $traversal.Count, $sizeCalls.Count, $roots.Count, $observedRoots.Count, $rootDecisions.Count)
    if (@($recordCounts | Select-Object -Unique).Count -ne 1 -or $heap.Count -eq 0)
    {
        throw "Incomplete benchmark records: heap=$($heap.Count), traversal=$($traversal.Count), sizeCalls=$($sizeCalls.Count), roots=$($roots.Count), observedRoots=$($observedRoots.Count), rootDecisions=$($rootDecisions.Count)."
    }
    if ($rootWork.Count -ne ($heap.Count * $rootCategoryCount))
    {
        throw "Incomplete root-work records: expected=$($heap.Count * $rootCategoryCount), actual=$($rootWork.Count)."
    }

    $records = @()
    for ($index = 0; $index -lt $heap.Count; $index++)
    {
        $totalSizeCalls = $sizeCalls[$index].Root +
            $sizeCalls[$index].StaticRoot +
            $sizeCalls[$index].FirstVisitScannable +
            $sizeCalls[$index].FirstVisitLeaf +
            $sizeCalls[$index].Revisit

        $workByCategory = [ordered]@{}
        $workStart = $index * $rootCategoryCount
        for ($workIndex = $workStart; $workIndex -lt ($workStart + $rootCategoryCount); $workIndex++)
        {
            $work = $rootWork[$workIndex]
            $workByCategory[$work.Category] = $work
        }

        $records += [pscustomobject][ordered]@{
            Dump = $index + 1
            Phase = $(if ($index -eq 0) { "cold" } else { "warm" })
            HeapDumpDurationMs = $heap[$index].DurationMs
            HeapObjects = $heap[$index].Objects
            HeapBytes = $heap[$index].Bytes
            TraversalDurationMs = $traversal[$index].DurationMs
            TraversalRoots = $traversal[$index].Roots
            ObservedRoots = $rootDecisions[$index].Observed
            TraversalRootCalls = $rootDecisions[$index].TraversalCalls
            TraversalObjects = $traversal[$index].Objects
            TraversalEdges = $traversal[$index].Edges
            TraversalAmplification = $(if ($heap[$index].Objects -eq 0) { 0 } else { [Math]::Round($traversal[$index].Objects / $heap[$index].Objects, 4) })
            ObjectsPerMs = $(if ($traversal[$index].DurationMs -eq 0) { 0 } else { [Math]::Round($traversal[$index].Objects / $traversal[$index].DurationMs, 2) })
            StackCapacity = $traversal[$index].StackCapacity
            Faults = $traversal[$index].Faults
            StopReason = $traversal[$index].StopReason
            TreeNodes = $traversal[$index].TreeNodes
            VisitedPeakEntries = $traversal[$index].VisitedPeakEntries
            VisitedKind = "hash"
            VisitedBytes = $traversal[$index].VisitedBytes
            VisitedBuckets = $traversal[$index].VisitedBuckets
            VisitedGrows = $traversal[$index].VisitedGrows
            FirstVisitReferences = $traversal[$index].FirstVisitReferences
            RevisitReferences = $traversal[$index].RevisitReferences
            GetClassFromObjectFirstVisitCalls = $traversal[$index].GetClassFromObjectFirstVisitCalls
            GetClassFromObjectRevisitCalls = $traversal[$index].GetClassFromObjectRevisitCalls
            RawMethodTableClassReads = $traversal[$index].RawMethodTableClassReads
            TotalSizeCalls = $totalSizeCalls
            RootSizeCalls = $sizeCalls[$index].Root
            StaticRootSizeCalls = $sizeCalls[$index].StaticRoot
            RootScannableSizeCalls = $sizeCalls[$index].RootScannable
            RootLeafSizeCalls = $sizeCalls[$index].RootLeaf
            StaticRootScannableSizeCalls = $sizeCalls[$index].StaticRootScannable
            StaticRootLeafSizeCalls = $sizeCalls[$index].StaticRootLeaf
            FirstVisitScannableSizeCalls = $sizeCalls[$index].FirstVisitScannable
            FirstVisitLeafSizeCalls = $sizeCalls[$index].FirstVisitLeaf
            RevisitSizeCalls = $sizeCalls[$index].Revisit
            FailedOrZeroSizeCalls = $sizeCalls[$index].FailedOrZero
            StackRoots = $roots[$index].Stack
            StaticRoots = $roots[$index].Static
            FinalizerRoots = $roots[$index].Finalizer
            HandleRoots = $roots[$index].Handle
            PinningRoots = $roots[$index].Pinning
            ConditionalWeakTableRoots = $roots[$index].ConditionalWeakTable
            ComRoots = $roots[$index].Com
            OtherRoots = $roots[$index].Other
            UnknownRoots = $roots[$index].Unknown
            ObservedStackRoots = $observedRoots[$index].Stack
            ObservedStaticRoots = $observedRoots[$index].Static
            ObservedFinalizerRoots = $observedRoots[$index].Finalizer
            ObservedHandleRoots = $observedRoots[$index].Handle
            ObservedPinningRoots = $observedRoots[$index].Pinning
            ObservedConditionalWeakTableRoots = $observedRoots[$index].ConditionalWeakTable
            ObservedComRoots = $observedRoots[$index].Com
            ObservedOtherRoots = $observedRoots[$index].Other
            ObservedUnknownRoots = $observedRoots[$index].Unknown
            DuplicateRootAddresses = $rootDecisions[$index].DuplicateAddresses
            InteriorRootsSkipped = $rootDecisions[$index].InteriorSkipped
            WeakRootsObserved = $rootDecisions[$index].WeakObserved
            RootClassLookupFailures = $rootDecisions[$index].ClassLookupFailed
            RootSizeLookupFailures = $rootDecisions[$index].SizeLookupFailed
            RootGetClassFromObjectCalls = $rootDecisions[$index].RootGetClassFromObjectCalls
            RootWork = [pscustomobject]$workByCategory
        }
    }

    return $records
}

function Write-RunDashboard
{
    param(
        [object] $Results,
        [string] $Path
    )

    $json = ($Results | ConvertTo-Json -Depth 12 -Compress).Replace("</", "<\/")
    $template = @'
<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<title>Reference-chain benchmark</title>
<style>
:root{color-scheme:dark;--bg:#10141d;--panel:#181e2a;--line:#2b3547;--text:#eef2f8;--muted:#9aa8bc;--blue:#65a7ff;--purple:#ba8cff;--green:#57d39b;--orange:#ffb454;--red:#ff6b76}
*{box-sizing:border-box}body{margin:0;background:var(--bg);color:var(--text);font:14px/1.45 Segoe UI,Arial,sans-serif}main{max-width:1500px;margin:auto;padding:28px}
h1{font-size:30px;margin:0 0 4px}.subtitle{color:var(--muted);margin-bottom:22px}.cards{display:grid;grid-template-columns:repeat(auto-fit,minmax(190px,1fr));gap:12px}.card,.panel{background:var(--panel);border:1px solid var(--line);border-radius:10px}.card{padding:15px}.card .value{font-size:25px;font-weight:650;margin-top:4px}.card .label{color:var(--muted)}
.grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(480px,1fr));gap:14px;margin-top:14px}.panel{padding:17px;overflow:hidden}.panel h2{font-size:17px;margin:0 0 12px}.chart{min-height:260px}
svg{width:100%;height:auto}.legend{display:flex;gap:18px;flex-wrap:wrap;margin-top:8px;color:var(--muted)}.dot{display:inline-block;width:9px;height:9px;border-radius:50%;margin-right:6px}
table{width:100%;border-collapse:collapse;font-variant-numeric:tabular-nums}th,td{padding:8px 9px;border-bottom:1px solid var(--line);text-align:right;white-space:nowrap}th:first-child,td:first-child{text-align:left}th{color:var(--muted);font-weight:600;position:sticky;top:0;background:var(--panel)}
.table-wrap{overflow:auto;max-height:600px}.ok{color:var(--green)}.bad{color:var(--red)}code{color:#cbd8ea}.footer{color:var(--muted);margin-top:18px}
@media(max-width:700px){main{padding:16px}.grid{grid-template-columns:1fr}}
</style>
</head>
<body><main>
<h1 id="title"></h1><div class="subtitle" id="subtitle"></div>
<div class="cards" id="cards"></div>
<div class="grid">
  <section class="panel"><h2>Process wall time and CPU time</h2><div class="chart" id="processChart"></div></section>
  <section class="panel"><h2>Heap dump and traversal duration</h2><div class="chart" id="durationChart"></div></section>
  <section class="panel"><h2>Surviving and traversed objects</h2><div class="chart" id="objectsChart"></div></section>
  <section class="panel"><h2>Parsed roots</h2><div class="chart" id="rootsChart"></div></section>
</div>
<section class="panel" style="margin-top:14px"><h2>Dump details</h2><div class="table-wrap"><table id="details"></table></div></section>
<div class="footer" id="footer"></div>
</main>
<script>
const data=__DATA__;
const colors=['#65a7ff','#ba8cff','#57d39b','#ffb454','#ff6b76'];
const median=v=>{const a=[...v].filter(Number.isFinite).sort((x,y)=>x-y);if(!a.length)return 0;const m=Math.floor(a.length/2);return a.length%2?a[m]:(a[m-1]+a[m])/2};
const integer=new Intl.NumberFormat();const decimal=new Intl.NumberFormat(undefined,{maximumFractionDigits:2});
const runs=data.Runs.map(r=>({run:r.Run,wallTimeMs:r.WallTimeMs,cpuTimeMs:r.CpuTimeMs,averageCpuPercent:r.AverageCpuPercent,privateBytes:r.PrivateBytes,gcPauseTimeMs:r.GcPauseTimeMs,dumps:r.Dumps.map(d=>({dump:d.Dump,phase:d.Phase,heapDumpDurationMs:d.HeapDumpDurationMs,heapObjects:d.HeapObjects,heapBytes:d.HeapBytes,traversalDurationMs:d.TraversalDurationMs,traversalRoots:d.TraversalRoots,observedRoots:d.ObservedRoots,traversalObjects:d.TraversalObjects,traversalEdges:d.TraversalEdges,traversalAmplification:d.TraversalAmplification,treeNodes:d.TreeNodes,objectsPerMs:d.ObjectsPerMs,stackCapacity:d.StackCapacity,faults:d.Faults,stopReason:d.StopReason,visitedPeakEntries:d.VisitedPeakEntries,visitedBytes:d.VisitedBytes,visitedBuckets:d.VisitedBuckets,visitedGrows:d.VisitedGrows,totalSizeCalls:d.TotalSizeCalls,getClassCalls:d.GetClassFromObjectFirstVisitCalls+d.GetClassFromObjectRevisitCalls,duplicateRoots:d.DuplicateRootAddresses}))}));
const dumps=runs.flatMap(r=>r.dumps.map(d=>({...d,run:r.run,label:`R${r.run}/D${d.dump}`})));
const fmt=(v,d=0)=>new Intl.NumberFormat(undefined,{maximumFractionDigits:d}).format(v);
document.getElementById('title').textContent=`Reference-chain benchmark: ${data.Label}`;
document.getElementById('subtitle').textContent=`${data.Configuration.Configuration}-${data.Configuration.Architecture}, ${data.Configuration.Framework}, Server GC • ${runs.length} process run(s), ${dumps.length} heap dump(s)`;
const cards=[
 ['Median wall time',`${fmt(median(runs.map(x=>x.wallTimeMs))/1000,2)} s`],
 ['Median CPU time',`${fmt(median(runs.map(x=>x.cpuTimeMs))/1000,2)} s`],
 ['Average CPU usage',`${fmt(median(runs.map(x=>x.averageCpuPercent)),1)}%`],
 ['Private memory',`${fmt(median(runs.map(x=>x.privateBytes))/1048576,1)} MiB`],
 ['GC pause time',`${fmt(median(runs.map(x=>x.gcPauseTimeMs))/1000,2)} s`],
 ['Heap dump duration',`${fmt(median(dumps.map(x=>x.heapDumpDurationMs)),0)} ms`],
 ['Traversal duration',`${fmt(median(dumps.map(x=>x.traversalDurationMs)),0)} ms`],
 ['Surviving objects',integer.format(median(dumps.map(x=>x.heapObjects)))],
 ['Parsed roots',integer.format(median(dumps.map(x=>x.traversalRoots)))],
 ['Traversed objects',integer.format(median(dumps.map(x=>x.traversalObjects)))],
 ['Traversal amplification',`${fmt(median(dumps.map(x=>x.traversalAmplification)),2)}x`],
 ['Examined references',integer.format(median(dumps.map(x=>x.traversalEdges)))],
 ['Visited memory',`${fmt(median(dumps.map(x=>x.visitedBytes))/1048576,1)} MiB`],
 ['GetObjectSize2 calls',integer.format(median(dumps.map(x=>x.totalSizeCalls)))],
 ['GetClassFromObject calls',integer.format(median(dumps.map(x=>x.getClassCalls)))],
 ['Duplicate root addresses',integer.format(median(dumps.map(x=>x.duplicateRoots)))]
];
document.getElementById('cards').innerHTML=cards.map(x=>`<div class="card"><div class="label">${x[0]}</div><div class="value">${x[1]}</div></div>`).join('');
function chart(id,labels,series,valueFormat){
 const w=900,h=270,p={l:62,r:18,t:18,b:42};const values=series.flatMap(s=>s.values);const max=Math.max(...values,1)*1.08;const x=i=>p.l+(labels.length===1?(w-p.l-p.r)/2:i*(w-p.l-p.r)/(labels.length-1));const y=v=>h-p.b-v*(h-p.t-p.b)/max;
 let svg=`<svg viewBox="0 0 ${w} ${h}" role="img">`;
 for(let i=0;i<=4;i++){const yy=p.t+i*(h-p.t-p.b)/4;const value=max*(1-i/4);svg+=`<line x1="${p.l}" y1="${yy}" x2="${w-p.r}" y2="${yy}" stroke="#2b3547"/><text x="${p.l-8}" y="${yy+4}" text-anchor="end" fill="#9aa8bc" font-size="11">${valueFormat(value)}</text>`}
 series.forEach((s,si)=>{const points=s.values.map((v,i)=>`${x(i)},${y(v)}`).join(' ');svg+=`<polyline points="${points}" fill="none" stroke="${colors[si]}" stroke-width="2.5"/>`;s.values.forEach((v,i)=>svg+=`<circle cx="${x(i)}" cy="${y(v)}" r="3" fill="${colors[si]}"><title>${labels[i]}: ${valueFormat(v)}</title></circle>`)});
 const step=Math.max(1,Math.ceil(labels.length/12));labels.forEach((l,i)=>{if(i%step===0||i===labels.length-1)svg+=`<text x="${x(i)}" y="${h-15}" text-anchor="middle" fill="#9aa8bc" font-size="11">${l}</text>`});svg+='</svg>';
 svg+=`<div class="legend">${series.map((s,i)=>`<span><i class="dot" style="background:${colors[i]}"></i>${s.name}</span>`).join('')}</div>`;document.getElementById(id).innerHTML=svg;
}
chart('processChart',runs.map(x=>`Run ${x.run}`),[
 {name:'Wall time (s)',values:runs.map(x=>x.wallTimeMs/1000)},
 {name:'CPU time (s)',values:runs.map(x=>x.cpuTimeMs/1000)}
],v=>fmt(v,1));
chart('durationChart',dumps.map(x=>x.label),[
 {name:'Heap dump (ms)',values:dumps.map(x=>x.heapDumpDurationMs)},
 {name:'Traversal (ms)',values:dumps.map(x=>x.traversalDurationMs)}
],v=>fmt(v,0));
chart('objectsChart',dumps.map(x=>x.label),[
 {name:'Surviving objects',values:dumps.map(x=>x.heapObjects)},
 {name:'Traversed objects',values:dumps.map(x=>x.traversalObjects)}
],v=>integer.format(Math.round(v)));
chart('rootsChart',dumps.map(x=>x.label),[
 {name:'Observed roots',values:dumps.map(x=>x.observedRoots)},
 {name:'Traversed roots',values:dumps.map(x=>x.traversalRoots)}
],v=>integer.format(Math.round(v)));
const columns=[
 ['Dump',x=>x.label],['Phase',x=>x.phase],['Heap ms',x=>integer.format(x.heapDumpDurationMs)],['Traversal ms',x=>integer.format(x.traversalDurationMs)],
 ['Survivors',x=>integer.format(x.heapObjects)],['Observed roots',x=>integer.format(x.observedRoots)],['Traversed roots',x=>integer.format(x.traversalRoots)],['Traversed',x=>integer.format(x.traversalObjects)],['Edges',x=>integer.format(x.traversalEdges)],
 ['Visited MiB',x=>fmt(x.visitedBytes/1048576,1)],['Size calls',x=>integer.format(x.totalSizeCalls)],['Class calls',x=>integer.format(x.getClassCalls)],['Faults',x=>integer.format(x.faults)],['Stop',x=>x.stopReason]
];
document.getElementById('details').innerHTML=`<thead><tr>${columns.map(c=>`<th>${c[0]}</th>`).join('')}</tr></thead><tbody>${dumps.map(x=>`<tr>${columns.map((c,i)=>`<td class="${i>8&&(x.faults||x.stopReason!=='none')?'bad':''}">${c[1](x)}</td>`).join('')}</tr>`).join('')}</tbody>`;
document.getElementById('footer').textContent=`Commit ${data.Commit} • Profiler ${data.ProfilerSha256.substring(0,12)} • Generated ${data.CreatedUtc}`;
</script></body></html>
'@

    $template.Replace("__DATA__", $json) | Set-Content -LiteralPath $Path -Encoding UTF8
}

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..\..")).Path
if ([string]::IsNullOrWhiteSpace($OutputRoot))
{
    $OutputRoot = Join-Path $repoRoot "artifacts\reference-chain"
}

$OutputRoot = [IO.Path]::GetFullPath($OutputRoot)
if ([string]::IsNullOrWhiteSpace($Label) -or
    $Label -in @(".", "..") -or
    $Label -ne [IO.Path]::GetFileName($Label))
{
    throw "Label must be a single folder name."
}

$labelDirectory = Join-Path $OutputRoot $Label
if ($RemoveLabel)
{
    if (-not (Test-Path -LiteralPath $labelDirectory -PathType Container))
    {
        throw "Label '$Label' does not exist at '$labelDirectory'."
    }

    Remove-Item -LiteralPath $labelDirectory -Recurse -Force
    Write-Host "Removed label '$Label': $labelDirectory"
    return
}

$configurationPlatform = "$Configuration-$Architecture"
if ([string]::IsNullOrWhiteSpace($ProfilerPath))
{
    $ProfilerPath = Join-Path $repoRoot "artifacts\profiler-build\bin\$configurationPlatform\profiler\src\ProfilerEngine\Datadog.Profiler.Native.Windows\Datadog.Profiler.Native.dll"
}
if ([string]::IsNullOrWhiteSpace($ApplicationPath))
{
    $ApplicationPath = Join-Path $repoRoot "artifacts\profiler-build\bin\$configurationPlatform\profiler\src\Demos\Samples.Computer01\$Framework\Samples.Computer01.exe"
}
if (-not (Test-Path -LiteralPath $ProfilerPath -PathType Leaf))
{
    throw "Profiler not found at '$ProfilerPath'. Build it before running the benchmark."
}
if (-not (Test-Path -LiteralPath $ApplicationPath -PathType Leaf))
{
    throw "Samples.Computer01 not found at '$ApplicationPath'. Build it before running the benchmark."
}

if (Test-Path -LiteralPath $labelDirectory)
{
    throw "Label '$Label' already exists at '$labelDirectory'. Choose a new label."
}
New-Item -ItemType Directory -Path $labelDirectory -Force | Out-Null

$environment = [ordered]@{
    "CORECLR_ENABLE_PROFILING" = "1"
    "CORECLR_PROFILER" = "{BD1A650D-AC5D-4896-B64F-D6FA25D6B26A}"
    "CORECLR_PROFILER_PATH" = $ProfilerPath
    "CORECLR_PROFILER_PATH_64" = $ProfilerPath
    "CORECLR_PROFILER_PATH_32" = $ProfilerPath
    "COR_ENABLE_PROFILING" = "1"
    "COR_PROFILER" = "{BD1A650D-AC5D-4896-B64F-D6FA25D6B26A}"
    "COR_PROFILER_PATH" = $ProfilerPath
    "COR_PROFILER_PATH_64" = $ProfilerPath
    "COR_PROFILER_PATH_32" = $ProfilerPath
    "COMPlus_EnableDiagnostics" = "1"
    "DOTNET_gcServer" = "1"
    "DOTNET_gcConcurrent" = "1"
    "DD_PROFILING_ENABLED" = "1"
    "DD_PROFILING_MANAGED_ACTIVATION_ENABLED" = "0"
    "DD_PROFILING_HEAPSNAPSHOT_ENABLED" = "1"
    "DD_INTERNAL_PROFILING_HEAPSNAPSHOT_SKIP_TRAVERSAL" = "0"
    "DD_INTERNAL_PROFILING_HEAPSNAPSHOT_MEMORY_PRESSURE_THRESHOLD" = "0"
    "DD_INTERNAL_PROFILING_TEST_HEAPSNAPSHOT_INTERVAL" = $SnapshotIntervalSeconds.ToString()
    "DD_INTERNAL_PROFILING_HEAPSNAPSHOT_REFERENCE_TREE_FORMAT" = "1"
    "DD_INTERNAL_PROFILING_HEAPSNAPSHOT_REFERENCE_CHAIN_BENCHMARK_ENABLED" = "1"
    "DD_PROFILING_CPU_ENABLED" = "0"
    "DD_PROFILING_WALLTIME_ENABLED" = "0"
    "DD_PROFILING_EXCEPTION_ENABLED" = "0"
    "DD_PROFILING_ALLOCATION_ENABLED" = "0"
    "DD_PROFILING_LOCK_ENABLED" = "0"
    "DD_PROFILING_GC_ENABLED" = "1"
    "DD_PROFILING_HEAP_ENABLED" = "0"
    "DD_GC_THREADS_CPUTIME_ENABLED" = "0"
    "DD_THREAD_LIFETIME_ENABLED" = "0"
    "DD_TRACE_ENABLED" = "0"
    "DD_TRACE_DEBUG" = "1"
    "DD_INTERNAL_USE_DEVELOPMENT_CONFIGURATION" = "true"
    "DD_PROFILING_UPLOAD_PERIOD" = "5"
    "DD_SERVICE" = "dd-dotnet-reference-chain-benchmark"
}

$dynamicEnvironmentNames = @("DD_TRACE_LOG_DIRECTORY", "DD_INTERNAL_PROFILING_OUTPUT_DIR", "DD_PROFILING_METRICS_FILEPATH")
$previousEnvironment = @{}
foreach ($name in @($environment.Keys) + $dynamicEnvironmentNames)
{
    $previousEnvironment[$name] = [Environment]::GetEnvironmentVariable($name, "Process")
}

$commit = (& git -C $repoRoot rev-parse HEAD).Trim()
$profilerHash = (Get-FileHash -LiteralPath $ProfilerPath -Algorithm SHA256).Hash
$applicationHash = (Get-FileHash -LiteralPath $ApplicationPath -Algorithm SHA256).Hash
$runs = @()

try
{
    foreach ($name in $environment.Keys)
    {
        [Environment]::SetEnvironmentVariable($name, $environment[$name], "Process")
    }

    for ($run = 1; $run -le $RunCount; $run++)
    {
        $runDirectory = Join-Path $labelDirectory ("run-{0:D2}" -f $run)
        $logDirectory = Join-Path $runDirectory "logs"
        $profileDirectory = Join-Path $runDirectory "pprof"
        New-Item -ItemType Directory -Path $logDirectory, $profileDirectory -Force | Out-Null

        $metricsPath = Join-Path $runDirectory "metrics.json"
        [Environment]::SetEnvironmentVariable("DD_TRACE_LOG_DIRECTORY", $logDirectory, "Process")
        [Environment]::SetEnvironmentVariable("DD_INTERNAL_PROFILING_OUTPUT_DIR", $profileDirectory, "Process")
        [Environment]::SetEnvironmentVariable("DD_PROFILING_METRICS_FILEPATH", $metricsPath, "Process")

        Write-Host "[$run/$RunCount] Running Computer01 reference-chain scenario 7..."
        $timer = [Diagnostics.Stopwatch]::StartNew()
        $process = Start-Process `
            -FilePath $ApplicationPath `
            -ArgumentList @("--scenario", "31", "--param", "7", "--timeout", $DurationSeconds.ToString()) `
            -WorkingDirectory (Split-Path -Parent $ApplicationPath) `
            -NoNewWindow `
            -Wait `
            -PassThru `
            -RedirectStandardOutput (Join-Path $runDirectory "stdout.log") `
            -RedirectStandardError (Join-Path $runDirectory "stderr.log")
        $timer.Stop()

        if ($process.ExitCode -ne 0)
        {
            throw "Samples.Computer01 run $run failed with exit code $($process.ExitCode)."
        }

        $managedMetricsPath = Join-Path $runDirectory "Managed_metrics.json"
        $metrics = Get-ManagedMetrics -Path $managedMetricsPath
        $cpuTimeMs = if ($metrics.ContainsKey("metric.runtime.process.processor_time"))
        {
            $metrics["metric.runtime.process.processor_time"]
        }
        else
        {
            try { $process.TotalProcessorTime.TotalMilliseconds } catch { 0 }
        }

        $wallTimeMs = $timer.Elapsed.TotalMilliseconds
        $averageCpuPercent = if ($wallTimeMs -eq 0) { 0 } else { ($cpuTimeMs / $wallTimeMs) * 100 }
        $dumps = @(Get-NativeDumpRecords -LogDirectory $logDirectory)

        $runs += [pscustomobject][ordered]@{
            Run = $run
            WallTimeMs = [Math]::Round($wallTimeMs, 2)
            CpuTimeMs = [Math]::Round($cpuTimeMs, 2)
            AverageCpuPercent = [Math]::Round($averageCpuPercent, 2)
            PrivateBytes = $(if ($metrics.ContainsKey("metric.runtime.process.private_bytes")) { $metrics["metric.runtime.process.private_bytes"] } else { 0 })
            GcPauseTimeMs = $(if ($metrics.ContainsKey("metric.runtime.dotnet.gc.pause_time")) { $metrics["metric.runtime.dotnet.gc.pause_time"] } else { 0 })
            Dumps = $dumps
        }
    }
}
finally
{
    foreach ($name in @($environment.Keys) + $dynamicEnvironmentNames)
    {
        [Environment]::SetEnvironmentVariable($name, $previousEnvironment[$name], "Process")
    }
}

$results = [pscustomobject][ordered]@{
    SchemaVersion = 2
    Label = $Label
    CreatedUtc = [DateTime]::UtcNow.ToString("O")
    Commit = $commit
    ProfilerSha256 = $profilerHash
    ApplicationSha256 = $applicationHash
    Configuration = [pscustomobject][ordered]@{
        Configuration = $Configuration
        Architecture = $Architecture
        Framework = $Framework
        Gc = "Server"
        RunCount = $RunCount
        DurationSeconds = $DurationSeconds
        SnapshotIntervalSeconds = $SnapshotIntervalSeconds
    }
    Runs = $runs
}

$resultsPath = Join-Path $labelDirectory "results.json"
$results | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $resultsPath -Encoding UTF8

$flatRows = foreach ($run in $runs)
{
    foreach ($dump in $run.Dumps)
    {
        [pscustomobject][ordered]@{
            Run = $run.Run
            WallTimeMs = $run.WallTimeMs
            CpuTimeMs = $run.CpuTimeMs
            AverageCpuPercent = $run.AverageCpuPercent
            PrivateBytes = $run.PrivateBytes
            GcPauseTimeMs = $run.GcPauseTimeMs
            Dump = $dump.Dump
            Phase = $dump.Phase
            HeapDumpDurationMs = $dump.HeapDumpDurationMs
            TraversalDurationMs = $dump.TraversalDurationMs
            HeapObjects = $dump.HeapObjects
            HeapBytes = $dump.HeapBytes
            TraversalRoots = $dump.TraversalRoots
            ObservedRoots = $dump.ObservedRoots
            TraversalObjects = $dump.TraversalObjects
            TraversalEdges = $dump.TraversalEdges
            TraversalAmplification = $dump.TraversalAmplification
            TreeNodes = $dump.TreeNodes
            VisitedBytes = $dump.VisitedBytes
            TotalSizeCalls = $dump.TotalSizeCalls
            FirstVisitScannableSizeCalls = $dump.FirstVisitScannableSizeCalls
            FirstVisitLeafSizeCalls = $dump.FirstVisitLeafSizeCalls
            RevisitSizeCalls = $dump.RevisitSizeCalls
            GetClassFromObjectFirstVisitCalls = $dump.GetClassFromObjectFirstVisitCalls
            GetClassFromObjectRevisitCalls = $dump.GetClassFromObjectRevisitCalls
            DuplicateRootAddresses = $dump.DuplicateRootAddresses
            InteriorRootsSkipped = $dump.InteriorRootsSkipped
            WeakRootsObserved = $dump.WeakRootsObserved
            Faults = $dump.Faults
            StopReason = $dump.StopReason
        }
    }
}
$flatRows | Export-Csv -LiteralPath (Join-Path $labelDirectory "results.csv") -NoTypeInformation

$dashboardPath = Join-Path $labelDirectory "dashboard.html"
Write-RunDashboard -Results $results -Path $dashboardPath

Write-Host "Results:   $resultsPath"
Write-Host "Dashboard: $dashboardPath"
if ($OpenDashboard)
{
    Start-Process $dashboardPath
}
