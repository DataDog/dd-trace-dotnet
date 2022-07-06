$sleep = $env:SLEEP_LENGTH
$timeout = $env:TIMEOUT_LENGTH
if (!$sleep)
{
    $sleep = 2
}
if (!$timeout)
{
    $timeout = 300
}

echo "Checking $( $args.count ) hosts with Sleep $sleep and timeout $timeout..."

for ($i = 0; $i -lt $args.count; $i++) {
    $splitArg = $args[$i].Split(":")
    $address = $splitArg[0]
    $port = $splitArg[1]
    echo "Waiting for $address to listen on $port..."

    $success = $false;
    $timer = [Diagnostics.Stopwatch]::StartNew()
    while ($timer.Elapsed.TotalSeconds -lt $Timeout) {
        try {
            $connection = New-Object System.Net.Sockets.TcpClient($address, $port)
            if ($connection.Connected) {
                echo "Connected!"
                $success = $true;
                break;
            }
        }
        catch {}

        if ($timer.Elapsed.TotalSeconds -lt $timeout) {
            echo "sleeping"
            sleep -Seconds $sleep
        }
    }

    $timer.Stop()
    if (!$success) {
        echo "Service $( $address ):$( $port ) did not start within $timeout seconds. Aborting..."
        exit 1
    }
}