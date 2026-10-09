$ErrorActionPreference = 'Stop'
$boardPort = if ($env:SOUND_BOARD_PORT) { [int]$env:SOUND_BOARD_PORT } else { 8100 }
$boardUrl = "http://127.0.0.1:$boardPort"
try {
    $boardExisting = Invoke-RestMethod "$boardUrl/api/board" -TimeoutSec 2
    if (-not $boardExisting.effects -or -not $boardExisting.token) { throw 'Port is occupied by another application.' }
} catch {
    if (Get-NetTCPConnection -LocalPort $boardPort -State Listen -ErrorAction SilentlyContinue) {
        throw "Port $boardPort is already in use. Set SOUND_BOARD_PORT to another port."
    }
    Start-Process -FilePath (Get-Command node).Source -ArgumentList 'tools/sound-board/server.js' -WorkingDirectory $PSScriptRoot -WindowStyle Hidden
    $boardReady = $false
    for ($boardAttempt = 0; $boardAttempt -lt 30; $boardAttempt++) {
        Start-Sleep -Milliseconds 200
        try { $null = Invoke-RestMethod "$boardUrl/api/board" -TimeoutSec 1; $boardReady = $true; break } catch {}
    }
    if (-not $boardReady) { throw 'Sound board did not start. Run node tools/sound-board/server.js to see the error.' }
}
Start-Process $boardUrl
