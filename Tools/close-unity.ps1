Get-Process Unity -ErrorAction SilentlyContinue | ForEach-Object {
    if ($_.MainWindowHandle -ne [IntPtr]::Zero) {
        $_.CloseMainWindow() | Out-Null
    }
}
Start-Sleep -Seconds 5
$remaining = Get-Process Unity -ErrorAction SilentlyContinue
if ($remaining) {
    Write-Host "Unity still running: $($remaining.Id -join ',')"
    exit 1
}
Write-Host "Unity closed"
exit 0
