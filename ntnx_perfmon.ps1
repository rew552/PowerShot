# Nutanix 接続情報
$NutanixServerIp = ""  # CVMのIPアドレス
$NutanixUsername = ""  # Nutanix管理者のユーザー名
$NutanixPassword = ""  # Nutanix管理者のパスワード

# パフォーマンス取得対象時刻を変数に格納
$UnixEpoch = New-Object -Type DateTime -ArgumentList 1970, 1, 1, 9, 0, 0, 0  # UNIX時間の基準となる日時を設定（日本標準時）

# 現在時刻を取得し、分と秒を0に設定して基準時刻とする
$CurrentTime = (Get-Date -Minute 0 -Second 0)  # 現在時刻を取得し、分と秒を0に設定

# 開始時刻を基準時刻から1時間前に設定し、UNIXタイムスタンプに変換（マイクロ秒）
$StartTime = [int]($(Get-Date($CurrentTime).AddHours(-1)) - $UnixEpoch).TotalSeconds  # 開始時刻を1時間前に設定し、UNIX秒に変換
$StartTimeMicroseconds = "$StartTime" + "000000"  # マイクロ秒単位に変換

# 終了時刻を基準時刻から30秒前に設定し、UNIXタイムスタンプに変換（マイクロ秒）
$EndTime = [int]($(Get-Date($CurrentTime).AddSeconds(-30)) - $UnixEpoch).TotalSeconds  # 終了時刻を30秒前に設定し、UNIX秒に変換
$EndTimeMicroseconds = "$EndTime" + "000000"  # マイクロ秒単位に変換

# スクリプトの実行ディレクトリパスを取得
$ScriptDirectory = Split-Path $MyInvocation.MyCommand.Path  # スクリプトの実行ディレクトリパスを取得

# 出力フォルダパスおよび出力ファイルのベースパスを設定
$OutputDirectory = $ScriptDirectory + "\NTNX_PerfmonData"  # 出力フォルダのパスを設定
$OutputBasePath = $OutputDirectory + "\" + $(Get-Date($CurrentTime.AddMinutes(-30)) -Format "yyyyMMdd")  # 出力ファイルのベースパスを設定

# 出力フォルダが存在しない場合は作成
if ( -not (Test-Path "$OutputDirectory") ) {
    New-Item -ItemType Directory -Path $OutputDirectory  # 出力フォルダが存在しない場合は作成
}

# SSL証明書のチェックを無視する設定
add-type @"
    using System.Net;
    using System.Security.Cryptography.X509Certificates;
    public class TrustAllCertsPolicy : ICertificatePolicy {
        public bool CheckValidationResult(
            ServicePoint srvPoint, X509Certificate certificate,
            WebRequest request, int certificateProblem) {
            return true;
        }
    }
"@
[System.Net.ServicePointManager]::CertificatePolicy = New-Object TrustAllCertsPolicy  # すべてのSSL証明書を信頼するポリシーを設定
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12  # TLS 1.2を使用するように設定

# APIリクエスト用ヘッダーを定義
$AuthHeader = @{
    "Authorization" = "Basic " + [System.Convert]::ToBase64String([System.Text.Encoding]::UTF8.GetBytes($NutanixUsername + ":" + $NutanixPassword))  # Basic認証用ヘッダーを設定
}
$ContentType = "application/json"  # コンテンツタイプをJSONに設定
$HttpMethod = "GET"  # HTTPメソッドをGETに設定
$HostsApiUrl = "https://${NutanixServerIp}:9440/api/nutanix/v2.0/hosts/"  # ホスト情報を取得するAPIエンドポイントを設定

# ホスト情報を取得するAPIリクエストを実行
$HostsApiResponse = Invoke-WebRequest -method $HttpMethod -Uri $HostsApiUrl -Headers $AuthHeader -ContentType $ContentType  # APIリクエストを実行し、レスポンスを取得

# JSONレスポンスをPowerShellオブジェクトに変換
$HostsData = $HostsApiResponse.Content | ConvertFrom-Json  # JSONレスポンスをPowerShellオブジェクトに変換

# 取得したホストのUUIDリストを取得
$HostUuids = $HostsData.entities.uuid  # 取得したホストのUUIDリストを取得

# 各ホストに対してパフォーマンスデータを取得
$HostCounter = 0
foreach ($HostUuid in $HostUuids) {
    # UUIDに対応するホスト名を取得
    $HostName = ($HostsData.entities | Where-Object { $_.uuid -eq $HostUuid }).name  # UUIDに対応するホスト名を取得

    # パフォーマンスデータを取得するAPIリクエストのURIを設定
    $PerfDataApiUrl = "https://${NutanixServerIp}:9440/api/nutanix/v2.0/hosts/${HostUuid}/stats/?metrics=hypervisor_cpu_usage_ppm%2Chypervisor_memory_usage_ppm%2Cnum_iops%2Chypervisor_num_received_bytes%2Chypervisor_num_transmitted_bytes`&start_time_in_usecs=${StartTimeMicroseconds}`&end_time_in_usecs=${EndTimeMicroseconds}`&interval_in_secs=30"

    # パフォーマンスデータを取得するAPIリクエストを実行
    $PerfDataApiResponse = Invoke-WebRequest -method $HttpMethod -Uri $PerfDataApiUrl -Headers $AuthHeader -ContentType $ContentType  # パフォーマンスデータを取得するAPIリクエストを実行

    # JSONレスポンスをPowerShellオブジェクトに変換
    $PerfData = ($PerfDataApiResponse.content | ConvertFrom-Json).stats_specific_responses  # JSONレスポンスをPowerShellオブジェクトに変換

    # 各メトリックのデータをフィルタリング
    $CpuUsageData = $PerfData | Where-Object { $_.metric -eq "hypervisor_cpu_usage_ppm" }
    $MemoryUsageData = $PerfData | Where-Object { $_.metric -eq "hypervisor_memory_usage_ppm" }
    $IopsData = $PerfData | Where-Object { $_.metric -eq "num_iops" }

    # 初回ループ時は配列オブジェクトを作成
    if ($HostCounter -eq 0) {
        $IntervalCounter = 0
        $CpuUsageArray = New-Object System.Collections.ArrayList
        foreach ($Value in $CpuUsageData.values) {
            # タイムスタンプとホスト名、リソース値を含むオブジェクトを作成
            $CpuUsageObject = New-Object psobject | Select-Object @{n = 'timestamp'; e = { $CurrentTime.AddHours(-1).AddSeconds($IntervalCounter * 30) } }, @{n = $HostName; e = { $Value } }
            $CpuUsageArray += $CpuUsageObject  # 配列オブジェクトに追加
            $IntervalCounter ++
        }

        $IntervalCounter = 0
        $MemoryUsageArray = New-Object System.Collections.ArrayList
        foreach ($Value in $MemoryUsageData.values) {
            $MemoryUsageObject = New-Object psobject | Select-Object @{n = 'timestamp'; e = { $CurrentTime.AddHours(-1).AddSeconds($IntervalCounter * 30) } }, @{n = $HostName; e = { $Value } }
            $MemoryUsageArray += $MemoryUsageObject
            $IntervalCounter ++
        }

        $IntervalCounter = 0
        $IopsArray = New-Object System.Collections.ArrayList
        foreach ($Value in $IopsData.values) {
            $IopsObject = New-Object psobject | Select-Object @{n = 'timestamp'; e = { $CurrentTime.AddHours(-1).AddSeconds($IntervalCounter * 30) } }, @{n = $HostName; e = { $Value } }
            $IopsArray += $IopsObject
            $IntervalCounter ++
        }
    }
    else {
        # 2回目以降のループでは既存の配列オブジェクトに追加
        $IntervalCounter = 0
        foreach ($Value in $CpuUsageData.values) {
            $CpuUsageObject = New-Object psobject | Select-Object @{n = 'timestamp'; e = { $CurrentTime.AddHours(-1).AddSeconds($IntervalCounter * 30) } }, @{n = $HostName; e = { $Value } }
            foreach ($Row in $CpuUsageArray) {
                if ($Row.timestamp -eq $CpuUsageObject.timestamp) {
                    $Row | Add-Member $HostName $Value  # 配列にデータを追加
                }
            }
            $IntervalCounter ++
        }

        $IntervalCounter = 0
        foreach ($Value in $MemoryUsageData.values) {
            $MemoryUsageObject = New-Object psobject | Select-Object @{n = 'timestamp'; e = { $CurrentTime.AddHours(-1).AddSeconds($IntervalCounter * 30) } }, @{n = $HostName; e = { $Value } }
            foreach ($Row in $MemoryUsageArray) {
                if ($Row.timestamp -eq $MemoryUsageObject.timestamp) {
                    $Row | Add-Member $HostName $Value
                }
            }
            $IntervalCounter ++
        }

        $IntervalCounter = 0
        foreach ($Value in $IopsData.values) {
            $IopsObject = New-Object psobject | Select-Object @{n = 'timestamp'; e = { $CurrentTime.AddHours(-1).AddSeconds($IntervalCounter * 30) } }, @{n = $HostName; e = { $Value } }
            foreach ($Row in $IopsArray) {
                if ($Row.timestamp -eq $IopsObject.timestamp) {
                    $Row | Add-Member $HostName $Value
                }
            }
            $IntervalCounter ++
        }

    }

    $HostCounter ++  # カウンタをインクリメント
}

# 各メトリックの配列をCSVファイルにエクスポート
$CpuUsageArray | Export-Csv -Encoding Default -NoTypeInformation -Append $OutputBasePath"_ntnx_cpu.csv"  # CPU使用率のデータをCSVファイルにエクスポート
$MemoryUsageArray | Export-Csv -Encoding Default -NoTypeInformation -Append $OutputBasePath"_ntnx_memory.csv"  # メモリ使用率のデータをCSVファイルにエクスポート
$IopsArray | Export-Csv -Encoding Default -NoTypeInformation -Append $OutputBasePath"_ntnx_iops.csv"  # IOPSのデータをCSVファイルにエクスポート
