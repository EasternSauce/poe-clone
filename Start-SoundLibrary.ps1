$ErrorActionPreference = 'Stop'
$libraryPort = if ($env:SOUND_LIBRARY_PORT) { [int]$env:SOUND_LIBRARY_PORT } else { 8101 }
$libraryUrl = "http://127.0.0.1:$libraryPort"
try {
    $libraryExisting = Invoke-RestMethod "$libraryUrl/api/library" -TimeoutSec 2
    if ($libraryExisting.app -ne 'sound-library') { throw 'Port is occupied by another application.' }
} catch {
    if (Get-NetTCPConnection -LocalPort $libraryPort -State Listen -ErrorAction SilentlyContinue) {
        throw "Port $libraryPort is already in use. Set SOUND_LIBRARY_PORT to another port."
    }
    Start-Process -FilePath (Get-Command node).Source -ArgumentList 'tools/sound-library/server.js' -WorkingDirectory $PSScriptRoot -WindowStyle Hidden
    $libraryReady = $false
    for ($libraryAttempt = 0; $libraryAttempt -lt 30; $libraryAttempt++) {
        Start-Sleep -Milliseconds 200
        try {
            $libraryCheck = Invoke-RestMethod "$libraryUrl/api/library" -TimeoutSec 1
            if ($libraryCheck.app -eq 'sound-library') { $libraryReady = $true; break }
        } catch {}
    }
    if (-not $libraryReady) { throw 'Sound Library did not start. Run node tools/sound-library/server.js to see the error.' }
}
Start-Process $libraryUrl
