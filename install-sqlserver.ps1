if (Test-Path "C:\Program Files\Microsoft SQL Server\MSSQL14.SQLEXPRESS") {
    Write-Output "SQL Server Express is already installed"
    Exit 0;
}

if (-not (Test-Path "C:\Temp\sqlexpress.exe")) {
    Write-Output "downloading SQL Server Express"
    (New-Object Net.WebClient).DownloadFile(
        'https://download.microsoft.com/download/5/E/9/5E9B18CC-8FD5-467E-B5BF-BADE39C51F73/SQLServer2017-SSEI-Expr.exe',
        "C:\Temp\sqlexpress.exe")
}

Write-Output "installing SQL Server Express"
C:\Temp\sqlexpress.exe /ACTION=install /ENU /IAcceptSQLServerLicenseTerms /Quiet /HideProgressBar
Exit $LastExitCode
