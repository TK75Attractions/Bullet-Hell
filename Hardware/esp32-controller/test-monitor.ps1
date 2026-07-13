# ESP32 2P コントローラーの動作確認モニタ
# 使い方: .\test-monitor.ps1 [-Port COM3]  （ポート省略時は自動検出を試みる）
param([string]$Port)

if (-not $Port) {
    $ports = [System.IO.Ports.SerialPort]::GetPortNames()
    if ($ports.Count -eq 0) { Write-Host "シリアルポートが見つかりません。USB 接続を確認してください。" -ForegroundColor Red; exit 1 }
    $Port = $ports[-1]
    Write-Host "ポート自動選択: $Port（違う場合は -Port COMx で指定。候補: $($ports -join ', ')）"
}

$sp = New-Object System.IO.Ports.SerialPort $Port, 115200
$sp.ReadTimeout = 500
try { $sp.Open() } catch { Write-Host "ポート $Port を開けません: $($_.Exception.Message)（Arduino IDE のシリアルモニタが開いていたら閉じてください）" -ForegroundColor Red; exit 1 }
Write-Host "接続しました（$Port・115200bps）。ESP32 の EN(リセット)ボタンを押すと HELLO 行が見えます。Ctrl+C で終了。" -ForegroundColor Cyan

$names = @('上','下','左','右','ボタン')
function Decode([int]$v) {
    $on = for ($i = 0; $i -lt 5; $i++) { if ($v -band (1 -shl $i)) { $names[$i] } }
    if ($on) { $on -join '+' } else { '—' }
}

while ($true) {
    try { $line = $sp.ReadLine().Trim() } catch { continue }
    if ($line -match '^S\s+([0-9A-Fa-f]{2})\s+([0-9A-Fa-f]{2})') {
        $p1 = [Convert]::ToInt32($Matches[1], 16); $p2 = [Convert]::ToInt32($Matches[2], 16)
        Write-Host ("{0:HH:mm:ss.f}  P1 [{1,-12}]  P2 [{2,-12}]  (raw: {3})" -f (Get-Date), (Decode $p1), (Decode $p2), $line)
    } elseif ($line) {
        Write-Host "RX: $line" -ForegroundColor Yellow
    }
}
