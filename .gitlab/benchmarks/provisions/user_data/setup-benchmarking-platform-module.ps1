<powershell>
@'
function Generate-GitHubInstallationAccessToken {
    param(
        # GitHub app (bp-repo-fetcher) authentication parameters
        # See https://github.com/organizations/DataDog/settings/apps/bp-repo-fetcher
        [Parameter(Mandatory = $true)][string]$privateKeySSMParameterName,
        [Parameter(Mandatory = $true)][string]$clientId
    )

    $installationAccessTokenScript = @"
    #!/usr/bin/env python3

import sys
import time
import jwt
import requests


def generate_github_installation_access_token(private_key_path, client_id):
    with open(private_key_path, "rb") as f:
        private_key = f.read()

    jwt_token = jwt.encode(
        {"iat": int(time.time()), "exp": int(time.time()) + 600, "iss": client_id},
        private_key,
        algorithm="RS256",
    )

    headers = {
        "Accept": "application/vnd.github+json",
        "Authorization": f"Bearer {jwt_token}",
        "X-GitHub-Api-Version": "2022-11-28",
    }

    installation_id = requests.get(
        "https://api.github.com/app/installations", headers=headers
    ).json()[0]["id"]

    return requests.post(
        f"https://api.github.com/app/installations/{installation_id}/access_tokens",
        headers=headers,
    ).json()["token"]


if __name__ == "__main__":
    print(generate_github_installation_access_token(sys.argv[1], sys.argv[2]))
"@

    $tempDir = [System.IO.Path]::GetTempPath()
    $installationAccessTokenScriptPath = "$tempDir\generate-installation-access-token.py"

    Set-Content -Path "$installationAccessTokenScriptPath" -Value $installationAccessTokenScript

    $venvDir = "$tempDir\.venv"
    python -m venv $venvDir

    & $venvDir\Scripts\activate
    pip install --disable-pip-version-check -q pyjwt[crypto] requests 2>$null

    $ssmParam = Get-SSMParameter -Name $privateKeySSMParameterName -WithDecryption $true
    $privateKeyPath = "$tempDir\bp-repo-fetcher-key.pem"
    Set-Content -Path $privateKeyPath -Value $ssmParam.Value

    $installationAccessToken = python "$installationAccessTokenScriptPath" $privateKeyPath $clientId
    & $venvDir\Scripts\deactivate

    Remove-Item -Path $installationAccessTokenScriptPath -Force
    Remove-Item -Path $venvDir -Recurse -Force

    Write-Output $installationAccessToken
}

Export-ModuleMember -Function Generate-GitHubInstallationAccessToken
'@ | Out-File -FilePath "C:\BenchmarkingPlatformModule.psm1" -Encoding utf8
</powershell>
<persist>true</persist>