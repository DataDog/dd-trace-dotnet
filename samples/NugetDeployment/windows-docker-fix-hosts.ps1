$hostsFile = "C:\Windows\System32\drivers\etc\hosts"

try {
    $DnsEntries = @("host.docker.internal", "gateway.docker.internal")
    # Tries resolving names for Docker
    foreach ($Entry in $DnsEntries) {
        # If any of the names are not resolved, throws an exception
        Resolve-DnsName -Name $Entry -ErrorAction Stop
    }

    # If it passes, means that DNS is already configured
    Write-Host("DNS settings are already configured.")
} catch {
    # Gets the gateway IP address, that is the Host's IP address in the Docker network
    $ip = (ipconfig | where-object {$_ -match "Default Gateway"} | foreach-object{$_.Split(":")[1]}).Trim()
    # Read the current content from Hosts file
    $src = [System.IO.File]::ReadAllLines($hostsFile)
    # Add the a new line after the content
    $lines = $src += ""
    
    # Check the hosts file and write it in if its not there...
    if((cat $hostsFile | Select-String -Pattern "host.docker.internal") -And (cat $hostsFile | Select-String -Pattern "gateway.docker.internal")) {
        For ($i=0; $i -le $lines.length; $i++) {
            if ($lines[$i].Contains("host.docker.internal"))
            {
                $lines[$i] = ("{0} host.docker.internal" -f $ip)
                $lines[$i+1] = ("{0} gateway.docker.internal" -f $ip)
                break
            }
        }
    } else {
        $lines = $lines += "# Added by Docker for Windows"
        $lines = $lines += ("{0} host.docker.internal" -f $ip)
        $lines = $lines += ("{0} gateway.docker.internal" -f $ip)
        $lines = $lines += "# End of section"
    }
    # Writes the new content to the Hosts file
    [System.IO.File]::WriteAllLines($hostsFile, [string[]]$lines)
    
    Write-Host("New DNS settings written successfully.")
}
