if (-not (Test-Path env:SKIP_TRACER_INSTALL)) { 

  $this_dir=Get-Location
  $pre_built_msi_folder="C:\msi\"

  $msi_to_run="datadog-apm.msi"

  Write-Host "[install-tracer.ps1] Installing pre-built tracer msi"
  $pre_built_msi=Get-ChildItem -Path $pre_built_msi_folder\*-x64.msi
  Write-Host "[install-tracer.ps1] Found msi at $pre_built_msi"
  $msi_to_run=$pre_built_msi
  
  Write-Host "[install-tracer.ps1] Installing Datadog APM"
  Start-Process -Wait msiexec -ArgumentList '/qn /i $msi_to_run'
  
  exit
}

# Skip tracer install
Write-Host "[install-tracer.ps1] Skipping install of tracer"