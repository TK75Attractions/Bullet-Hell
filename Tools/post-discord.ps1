# Discord webhook へメッセージ+添付ファイルを投稿する。
# webhook URL は .claude\discord-webhook.txt から読む(トークンなので表示・ログ出力しない)。
# 日本語メッセージは shell エスケープで HTTP 400 になるため、payload_json を
# UTF-8(BOMなし) ファイルにして -F で渡す(過去セッションで確立した方式)。
# 使い方:
#   .\Tools\post-discord.ps1 -Message "本文" -Files video.mp4,img1.png
# 添付は Discord 無料枠の上限に合わせ 1 ファイル 8MB 未満にしておくこと。
param(
    [Parameter(Mandatory = $true)][string]$Message,
    [string[]]$Files = @()
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$webhookFile = Join-Path $repoRoot '.claude\discord-webhook.txt'
if (-not (Test-Path $webhookFile)) { throw "webhook ファイルが見つからない: $webhookFile" }
$webhook = (Get-Content $webhookFile -Raw).Trim()

foreach ($f in $Files) {
    if (-not (Test-Path $f)) { throw "添付が見つからない: $f" }
    $len = (Get-Item $f).Length
    if ($len -ge 8MB) { throw "8MB 以上の添付は不可: $f ($([math]::Round($len/1MB,1))MB)" }
}

# payload_json を UTF-8(BOMなし) で一時ファイルに書く
$payload = @{ content = $Message } | ConvertTo-Json -Compress
$payloadPath = Join-Path ([IO.Path]::GetTempPath()) ("discord_payload_{0}.json" -f (Get-Random))
[IO.File]::WriteAllText($payloadPath, $payload, (New-Object System.Text.UTF8Encoding($false)))

try {
    # 注意: payload_json は `<`(内容のみ送信)で渡す。`@` だと filename 付きの
    # multipart になり Discord が本文でなく添付ファイルとして扱う(実測)。
    $args = @('-sS', '-o', '-', '-w', "`nHTTP %{http_code}`n",
              '-F', "payload_json=<$payloadPath;type=application/json")
    for ($i = 0; $i -lt $Files.Count; $i++) {
        $args += @('-F', "files[$i]=@$($Files[$i])")
    }
    $args += $webhook
    & curl.exe @args
}
finally {
    Remove-Item $payloadPath -ErrorAction SilentlyContinue
}
