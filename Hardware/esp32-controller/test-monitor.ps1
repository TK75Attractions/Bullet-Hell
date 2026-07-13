# ESP32 2P controller serial monitor.
# Usage: .\test-monitor.ps1 [-Port COM3]
param([string]$Port)

if (-not $Port) {
    $ports = @([System.IO.Ports.SerialPort]::GetPortNames())
    if ($ports.Count -eq 0) {
        Write-Host 'No serial ports found. Check both USB connections.' -ForegroundColor Red
        exit 1
    }

    $Port = $ports[-1]
    Write-Host "Auto-selected $Port. Available ports: $($ports -join ', ')"
    Write-Host 'The firmware mirrors controller output to both CH343 UART and native USB CDC.'
}

$sp = New-Object System.IO.Ports.SerialPort $Port, 115200
$sp.ReadTimeout = 500
$sp.DtrEnable = $false
$sp.RtsEnable = $false

try {
    $sp.Open()
} catch {
    Write-Host "Could not open ${Port}: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host 'Close any PlatformIO or Arduino serial monitor and try again.' -ForegroundColor Red
    exit 1
}

Write-Host "Connected to $Port at 115200 bps with DTR/RTS disabled." -ForegroundColor Cyan
Write-Host 'Press RST to check HELLO output. Press Ctrl+C to stop.' -ForegroundColor Cyan

$names = @('Up', 'Down', 'Left', 'Right', 'A', 'B')
function Decode([int]$v) {
    $on = for ($i = 0; $i -lt 6; $i++) {
        if ($v -band (1 -shl $i)) { $names[$i] }
    }
    if ($on) { $on -join '+' } else { '-' }
}

try {
    while ($true) {
        try {
            $line = $sp.ReadLine().Trim()
        } catch [System.TimeoutException] {
            continue
        }

        if ($line -match '^S\s+([0-9A-Fa-f]{2})\s+([0-9A-Fa-f]{2})') {
            $p1 = [Convert]::ToInt32($Matches[1], 16)
            $p2 = [Convert]::ToInt32($Matches[2], 16)
            Write-Host ('{0:HH:mm:ss.f}  P1 [{1,-12}]  P2 [{2,-12}]  (raw: {3})' -f (Get-Date), (Decode $p1), (Decode $p2), $line)
        } elseif ($line) {
            Write-Host "RX: $line" -ForegroundColor Yellow
        }
    }
} finally {
    if ($sp.IsOpen) { $sp.Close() }
    $sp.Dispose()
}
