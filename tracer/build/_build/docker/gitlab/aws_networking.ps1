$ErrorActionPreference = 'Stop'

$defgw = Get-NetRoute -DestinationPrefix "0.0.0.0/0" | Select-Object -ExpandProperty "NextHop"
Set-NetRoute -DestinationPrefix "169.254.169.254/32" -NextHop $defgw -ErrorAction 'silentlycontinue'
if(! $?){
    $defif = Get-NetRoute -DestinationPrefix "0.0.0.0/0" | Select-Object -ExpandProperty "InterfaceIndex"
    New-NetRoute -DestinationPrefix "169.254.169.254/32" -NextHop $defgw -InterfaceIndex $defif -ErrorAction 'silentlycontinue'
    if(! $?){
        Write-Host -ForegroundColor Yellow "New-NetRoute returned an error; checking..."
        $route = Get-NetRoute -DestinationPrefix "169.254.169.254/32"
        if($route -and $route.NextHop -eq $defgw){
            Write-Host -ForegroundColor Green "AWS Endpoint properly configured."
            $inst = (iwr -ErrorAction 'silentlycontinue' -UseBasicParsing http://169.254.169.254/latest/meta-data/instance-id).content
            if($inst){
                Write-Host -ForegroundColor Green "AWS metadata endpoint accessible.  Instance ID $inst"
            } else {
                Write-Host -ForegroundColor Red "Metadata endpoint not available"
            }
        } else {
            Write-Host -ForegroundColor Red "Route not properly configured, AWS metadata not available"
        }
    }
}