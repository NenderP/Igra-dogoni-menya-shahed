param([string]$Uri = "ws://localhost:5050/ws")

$ct = [System.Threading.CancellationToken]::None
$ws = New-Object System.Net.WebSockets.ClientWebSocket
$ws.ConnectAsync([Uri]$Uri, $ct).Wait()
Write-Host "== подключился к $Uri =="

function Send($obj) {
    $json = $obj | ConvertTo-Json -Compress -Depth 6
    $bytes = [Text.Encoding]::UTF8.GetBytes($json)
    $ws.SendAsync($bytes, 'Text', $true, $ct).Wait()
}

function Recv() {
    $buf = New-Object byte[] 262144
    $sb = New-Object Text.StringBuilder
    do { $r = $ws.ReceiveAsync($buf, $ct).Result; [void]$sb.Append([Text.Encoding]::UTF8.GetString($buf, 0, $r.Count)) } until ($r.EndOfMessage)
    return $sb.ToString()
}

function Show($label, $msg) {
    if ($msg.Length -gt 260) { $msg = $msg.Substring(0, 260) + "..." }
    Write-Host "<< [$label] $msg"
}

Send @{ type = 'hello'; payload = @{ player_id = 'tester1'; display_name = 'Tester' } }
Show 'welcome' (Recv)

Send @{ type = 'vs_bot'; payload = @{ difficulty = 'easy' } }
Show 'match_found' (Recv)
Show 'round_start' (Recv)
Show 'dice_rolled' (Recv)
$sync = Recv
Show 'state_sync' $sync
$state = $sync | ConvertFrom-Json
$myUid = $state.payload.active_character
$enemyUid = $state.payload.opponent.characters[0].uid
Write-Host "== мой боец: $myUid, враг: $enemyUid =="

Send @{ type = 'reroll_dice'; payload = @{ indexes = @(0, 1) } }
Show 'reroll->sync' (Recv)

Send @{ type = 'use_skill'; payload = @{ character_uid = $myUid; target_uid = $enemyUid } }
Show 'skill' (Recv)

Send @{ type = 'end_turn'; payload = @{} }
# читаем действия бота, пока не начнётся раунд 2 (round_start → dice_rolled → state_sync)
foreach ($i in 1..12) {
    $m = Recv
    Show 'bot/round' $m
    if ($m -match '"type":"round_start"') {
        Show 'dice_rolled' (Recv)
        Show 'state_sync' (Recv)
        break
    }
}

Send @{ type = 'gacha_pull'; payload = @{ count = 10 } }
Show 'gacha_result' (Recv)

Send @{ type = 'collection_sync'; payload = @{} }
Show 'collection_state' (Recv)

$ws.CloseAsync('NormalClosure', '', $ct).Wait()
Write-Host "== смоук-тест пройден =="
