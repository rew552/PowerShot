# Nutanix �ڑ����
$NutanixServerIp = ""  # CVM��IP�A�h���X
$NutanixUsername = ""  # Nutanix�Ǘ��҂̃��[�U�[��
$NutanixPassword = ""  # Nutanix�Ǘ��҂̃p�X���[�h

# �p�t�H�[�}���X�擾�Ώێ�����ϐ��Ɋi�[
$UnixEpoch = New-Object -Type DateTime -ArgumentList 1970, 1, 1, 9, 0, 0, 0  # UNIX���Ԃ̊�ƂȂ������ݒ�i���{�W�����j

# ���ݎ������擾���A���ƕb��0�ɐݒ肵�Ċ�����Ƃ���
$CurrentTime = (Get-Date -Minute 0 -Second 0)  # ���ݎ������擾���A���ƕb��0�ɐݒ�

# �J�n���������������1���ԑO�ɐݒ肵�AUNIX�^�C���X�^���v�ɕϊ��i�}�C�N���b�j
$StartTime = [int]($(Get-Date($CurrentTime).AddHours(-1)) - $UnixEpoch).TotalSeconds  # �J�n������1���ԑO�ɐݒ肵�AUNIX�b�ɕϊ�
$StartTimeMicroseconds = "$StartTime" + "000000"  # �}�C�N���b�P�ʂɕϊ�

# �I�����������������30�b�O�ɐݒ肵�AUNIX�^�C���X�^���v�ɕϊ��i�}�C�N���b�j
$EndTime = [int]($(Get-Date($CurrentTime).AddSeconds(-30)) - $UnixEpoch).TotalSeconds  # �I��������30�b�O�ɐݒ肵�AUNIX�b�ɕϊ�
$EndTimeMicroseconds = "$EndTime" + "000000"  # �}�C�N���b�P�ʂɕϊ�

# �X�N���v�g�̎��s�f�B���N�g���p�X���擾
$ScriptDirectory = Split-Path $MyInvocation.MyCommand.Path  # �X�N���v�g�̎��s�f�B���N�g���p�X���擾

# �o�̓t�H���_�p�X����яo�̓t�@�C���̃x�[�X�p�X��ݒ�
$OutputDirectory = $ScriptDirectory + "\NTNX_PerfmonData"  # �o�̓t�H���_�̃p�X��ݒ�
$OutputBasePath = $OutputDirectory + "\" + $(Get-Date($CurrentTime.AddMinutes(-30)) -Format "yyyyMMdd")  # �o�̓t�@�C���̃x�[�X�p�X��ݒ�

# �o�̓t�H���_�����݂��Ȃ��ꍇ�͍쐬
if ( -not (Test-Path "$OutputDirectory") ) {
    New-Item -ItemType Directory -Path $OutputDirectory  # �o�̓t�H���_�����݂��Ȃ��ꍇ�͍쐬
}

# SSL�ؖ����̃`�F�b�N�𖳎�����ݒ�
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
[System.Net.ServicePointManager]::CertificatePolicy = New-Object TrustAllCertsPolicy  # ���ׂĂ�SSL�ؖ�����M������|���V�[��ݒ�
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12  # TLS 1.2���g�p����悤�ɐݒ�

# API���N�G�X�g�p�w�b�_�[���`
$AuthHeader = @{
    "Authorization" = "Basic " + [System.Convert]::ToBase64String([System.Text.Encoding]::UTF8.GetBytes($NutanixUsername + ":" + $NutanixPassword))  # Basic�F�ؗp�w�b�_�[��ݒ�
}
$ContentType = "application/json"  # �R���e���c�^�C�v��JSON�ɐݒ�
$HttpMethod = "GET"  # HTTP���\�b�h��GET�ɐݒ�
$HostsApiUrl = "https://${NutanixServerIp}:9440/api/nutanix/v2.0/hosts/"  # �z�X�g�����擾����API�G���h�|�C���g��ݒ�

# �z�X�g�����擾����API���N�G�X�g�����s
$HostsApiResponse = Invoke-WebRequest -method $HttpMethod -Uri $HostsApiUrl -Headers $AuthHeader -ContentType $ContentType  # API���N�G�X�g�����s���A���X�|���X���擾

# JSON���X�|���X��PowerShell�I�u�W�F�N�g�ɕϊ�
$HostsData = $HostsApiResponse.Content | ConvertFrom-Json  # JSON���X�|���X��PowerShell�I�u�W�F�N�g�ɕϊ�

# �擾�����z�X�g��UUID���X�g���擾
$HostUuids = $HostsData.entities.uuid  # �擾�����z�X�g��UUID���X�g���擾

# �e�z�X�g�ɑ΂��ăp�t�H�[�}���X�f�[�^���擾
$HostCounter = 0
foreach ($HostUuid in $HostUuids) {
    # UUID�ɑΉ�����z�X�g�����擾
    $HostName = ($HostsData.entities | Where-Object { $_.uuid -eq $HostUuid }).name  # UUID�ɑΉ�����z�X�g�����擾

    # �p�t�H�[�}���X�f�[�^���擾����API���N�G�X�g��URI��ݒ�
    $PerfDataApiUrl = "https://${NutanixServerIp}:9440/api/nutanix/v2.0/hosts/${HostUuid}/stats/?metrics=hypervisor_cpu_usage_ppm%2Chypervisor_memory_usage_ppm%2Cnum_iops%2Chypervisor_num_received_bytes%2Chypervisor_num_transmitted_bytes`&start_time_in_usecs=${StartTimeMicroseconds}`&end_time_in_usecs=${EndTimeMicroseconds}`&interval_in_secs=30"

    # �p�t�H�[�}���X�f�[�^���擾����API���N�G�X�g�����s
    $PerfDataApiResponse = Invoke-WebRequest -method $HttpMethod -Uri $PerfDataApiUrl -Headers $AuthHeader -ContentType $ContentType  # �p�t�H�[�}���X�f�[�^���擾����API���N�G�X�g�����s

    # JSON���X�|���X��PowerShell�I�u�W�F�N�g�ɕϊ�
    $PerfData = ($PerfDataApiResponse.content | ConvertFrom-Json).stats_specific_responses  # JSON���X�|���X��PowerShell�I�u�W�F�N�g�ɕϊ�

    # �e���g���b�N�̃f�[�^���t�B���^�����O
    $CpuUsageData = $PerfData | Where-Object { $_.metric -eq "hypervisor_cpu_usage_ppm" }
    $MemoryUsageData = $PerfData | Where-Object { $_.metric -eq "hypervisor_memory_usage_ppm" }
    $IopsData = $PerfData | Where-Object { $_.metric -eq "num_iops" }

    # ���񃋁[�v���͔z��I�u�W�F�N�g���쐬
    if ($HostCounter -eq 0) {
        $IntervalCounter = 0
        $CpuUsageArray = New-Object System.Collections.ArrayList
        foreach ($Value in $CpuUsageData.values) {
            # �^�C���X�^���v�ƃz�X�g���A���\�[�X�l���܂ރI�u�W�F�N�g���쐬
            $CpuUsageObject = New-Object psobject | Select-Object @{n = 'timestamp'; e = { $CurrentTime.AddHours(-1).AddSeconds($IntervalCounter * 30) } }, @{n = $HostName; e = { $Value } }
            $CpuUsageArray += $CpuUsageObject  # �z��I�u�W�F�N�g�ɒǉ�
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
        # 2��ڈȍ~�̃��[�v�ł͊����̔z��I�u�W�F�N�g�ɒǉ�
        $IntervalCounter = 0
        foreach ($Value in $CpuUsageData.values) {
            $CpuUsageObject = New-Object psobject | Select-Object @{n = 'timestamp'; e = { $CurrentTime.AddHours(-1).AddSeconds($IntervalCounter * 30) } }, @{n = $HostName; e = { $Value } }
            foreach ($Row in $CpuUsageArray) {
                if ($Row.timestamp -eq $CpuUsageObject.timestamp) {
                    $Row | Add-Member $HostName $Value  # �z��Ƀf�[�^��ǉ�
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

    $HostCounter ++  # �J�E���^���C���N�������g
}

# �e���g���b�N�̔z���CSV�t�@�C���ɃG�N�X�|�[�g
$CpuUsageArray | Export-Csv -Encoding Default -NoTypeInformation -Append $OutputBasePath"_ntnx_cpu.csv"  # CPU�g�p���̃f�[�^��CSV�t�@�C���ɃG�N�X�|�[�g
$MemoryUsageArray | Export-Csv -Encoding Default -NoTypeInformation -Append $OutputBasePath"_ntnx_memory.csv"  # �������g�p���̃f�[�^��CSV�t�@�C���ɃG�N�X�|�[�g
$IopsArray | Export-Csv -Encoding Default -NoTypeInformation -Append $OutputBasePath"_ntnx_iops.csv"  # IOPS�̃f�[�^��CSV�t�@�C���ɃG�N�X�|�[�g
