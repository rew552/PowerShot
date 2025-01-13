<#
    .SYNOPSIS
        �N���b�v�{�[�h�̉摜�f�[�^���Ď����A�X�N���[���V���b�g���s���ƃt�@�C���ۑ���ʂƎ擾�����摜��\�����܂��B

    .DESCRIPTION
        �N���b�v�{�[�h�̉摜�f�[�^���Ď����A�X�N���[���V���b�g���s���ƃt�@�C���ۑ���ʂƎ擾�����摜��\�����܂��B
        �ۑ���t�H���_�A�t�@�C�����̐ړ���(�ۑ���ʂŃt�@�C�������w�肵�Ȃ��ꍇ)��ݒ肷��ꍇ��$saveDir�A$header�̒l��ύX���Ă��������B
        �f�t�H���g�ł̓X�N���v�g�Ɠ����f�B���N�g����"Screenshots"�t�H���_���ۑ���ŁA�ړ�����"SS_"�ł��B
        ���̃X�N���v�g��Windows11 PowerShell 5.1�ł̂ݓ���m�F���Ă��܂��B

    .PARAMETER Path
        �Ȃ�

    .INPUTS
        �Ȃ�

    .OUTPUTS
        �Ȃ�

    .EXAMPLE
        1. �X�N���v�g�t�H���_��K���ȃt�H���_�ɔz�u���܂��B
        2. powershot.bat�����s���܂��B
        3. �X�N���[���V���b�g���擾���܂��B(PrintScreen�AAlt + PrintScreen�AWin + Shift + S��)
        4. �摜�̃v���r���[�ƃt�@�C���ۑ���ʂ��\������邽�߁A�t�@�C��������͂�"Save"���N���b�N��������Enter�L�[���������܂��B
            ��1 2��ڈȍ~�͘A�Ԃ������ŉ��Z����܂��B
            ��2 �t�@�C�������w�肵�Ȃ��ꍇ�́A�X�N���v�g��$header�ϐ��ɒ�`�����ړ����ƃ^�C���X�^���v�Ŗ�������܂��B
            ��3 �摜�t�@�C���̓X�N���v�g��$saveDir�ɒ�`�����t�@�C���p�X�ɕۑ�����܂��B
        5. ��~����ꍇ�̓R���\�[����Ctrl-C���������Ă��������B

    .NOTES
        �쐬��: 2023/12/01
        �X�V��: 2025/01/13
        �o�[�W����: 1.1
#>

# �p�X�̒�`
$scriptPath = Split-Path $MyInvocation.MyCommand.Path

# ���ۑ���t�H���_�̒�`��
$saveDir = $scriptPath + "\ScreenShots"
#$saveDir = "�C�ӂ̃p�X"

# ���t�@�C�����̐ړ�����
$header = "SS_"

# �A�Z���u���̃��[�h
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
[System.Windows.Forms.Application]::EnableVisualStyles()

# ============================================�֐��쐬============================================

# �X�N���[���V���b�g�ۑ��֐�
Function saveSS($FileName) {
    # �t�@�C�����Ɋg���q��t�^
    $fileExt = ($FileName) + ".jpg"

    # �t���p�X�ɂ���
    $imagePath = Join-Path $saveDir $fileExt

    # �t�@�C�������d�����Ă���ꍇ
    if (Test-Path -Path $imagePath) {
        # �G���[���b�Z�[�W���o��
        [void][System.Windows.Forms.MessageBox]::Show("�t�@�C�������d�����Ă��܂��B", "�G���[", "OK", "Error")

        # �A�ԃC���N�������g���Ȃ�
        $global:seqNum = [int]$seqTextBox.Text
        $global:seqNumzero = "{0:000}" -f $global:seqNum
    }
    else {
        try {
            # �X�N���[���V���b�g��ۑ�
            $image.Save($imagePath, [System.Drawing.Imaging.ImageFormat]::Jpeg)
            Write-Host "Saved:" $imagePath "`r`n"

            # �N���b�v�{�[�h����ɂ���
            Set-Clipboard
        }
        catch {
            # �G���[���b�Z�[�W�𐶐�
            $errorMessage = @"
�G���[�̎��: $($_.Exception.GetType().FullName)
�G���[���b�Z�[�W: $($_.Exception.Message)
�X�^�b�N�g���[�X: $($_.Exception.StackTrace)
��������: $(Get-Date)
"@

            # �G���[���b�Z�[�W��\��
            [void][System.Windows.Forms.MessageBox]::Show($errorMessage, "�G���[", "OK", "Error")
        }
        finally {
            # �ϐ����
            $image = $null
            Remove-Variable image -ErrorAction SilentlyContinue
        }
    }
}

# �t�H�[���쐬�֐�
Function showForm() {
    # �s�N�`���[�{�b�N�X �t�H�[���S�̂̐ݒ�
    $picWidth = [int]($image.Width / 3)
    $picHeight = [int]($image.Height / 3)
    $picForm = New-Object System.Windows.Forms.Form
    $picForm.Text = "�X�N���[���V���b�g �v���r���["
    $picForm.ShowInTaskbar = $False
    $picForm.FormBorderStyle = "Fixed3D"
    $picForm.Size = New-Object System.Drawing.Size($picWidth, $picHeight)
    $picForm.StartPosition = "manual"
    $picForm.Location = "20,20"

    # �s�N�`���[�{�b�N�X�쐬
    $picBox = New-Object System.Windows.Forms.PictureBox
    $picBox.Size = "$picWidth,$picHeight"
    $picBox.SizeMode = [System.Windows.Forms.PictureBoxSizeMode]::Zoom
    $picBox.Image = $image
    $picForm.Controls.Add($picBox)

    # �T�u�t�H�[��
    $picForm.Add_Shown({
            # �t�H���g�̎w��
            $font = New-Object System.Drawing.Font("Yu Gothic UI", 11)

            # �t�H�[���S�̂̐ݒ�
            $inputForm = New-Object System.Windows.Forms.Form
            $inputForm.Text = ">_ PowerShot"
            $inputForm.Size = New-Object System.Drawing.Size(340, 205)
            $inputForm.StartPosition = "CenterScreen"
            $inputForm.BackColor = "white"
            $inputForm.FormBorderStyle = "Fixed3D"
            $inputForm.MaximizeBox = $False
            $inputForm.MinimizeBox = $False
            $inputForm.ControlBox = $False
            $inputForm.TopLevel = $True
            $inputForm.font = $font
            $inputForm.Owner = $picForm

            # ���x����\��
            $label = New-Object System.Windows.Forms.Label
            $label.Location = New-Object System.Drawing.Point(20, 10)
            $label.Size = New-Object System.Drawing.Size(270, 20)
            $label.Text = "�t�@�C����(�g���q�s�v):"
            $inputForm.Controls.Add($label)

            # ���̓{�b�N�X�̐ݒ�
            $textBox = New-Object System.Windows.Forms.TextBox
            $textBox.Location = New-Object System.Drawing.Point(20, 40)
            $textBox.Size = New-Object System.Drawing.Size(280, 30)
            $textBox.font = $font
            $textBox.Text = $recentText
            $textBox.Select($recentText.Length, 1)
            $inputForm.Controls.Add($textBox)

            # ���x����\��
            $seqlabel = New-Object System.Windows.Forms.Label
            $seqlabel.Location = New-Object System.Drawing.Point(20, 85)
            $seqlabel.Size = New-Object System.Drawing.Size(80, 20)
            $seqlabel.Text = "�A��:"
            $inputForm.Controls.Add($seqlabel)

            # ���̓{�b�N�X�̐ݒ�
            $seqTextBox = New-Object System.Windows.Forms.TextBox
            $seqTextBox.Location = New-Object System.Drawing.Point(20, 115)
            $seqTextBox.Size = New-Object System.Drawing.Size(80, 30)
            $seqTextBox.font = $font
            $seqTextBox.Text = $global:seqNumzero
            $inputForm.Controls.Add($seqTextBox)

            # �ۑ��{�^���̐ݒ�
            $OKButton = New-Object System.Windows.Forms.Button
            $OKButton.Location = New-Object System.Drawing.Point(135, 113)
            $OKButton.Size = New-Object System.Drawing.Size(75, 30)
            $OKButton.Text = "�ۑ�"
            $OKButton.FlatStyle = "System"
            $inputForm.AcceptButton = $OKButton
            $inputForm.Controls.Add($OKButton)
            $OKButton.Add_Click{
                # �t�@�C�����Ƃ��Ďg�p�ł��Ȃ������̃`�F�b�N
                $invalidChars = [IO.Path]::GetInvalidFileNameChars()
                $isValidFileName = $true

                foreach ($char in $textBox.Text.ToCharArray()) {
                    if ($invalidChars -contains $char) {
                        $isValidFileName = $false
                        break
                    }
                }

                # �t�@�C�����Ƃ��Ďg�p�ł��Ȃ��������܂܂�Ă���ꍇ
                if (-not $isValidFileName) {
                    # �G���[���b�Z�[�W��\��
                    [void][System.Windows.Forms.MessageBox]::Show("�t�@�C�����Ɏg�p�ł��Ȃ��������܂܂�Ă��܂��B", "�G���[", "OK", "Error")
                }
                else {
                    ### �e�L�X�g�{�b�N�X����̏ꍇ ###
                    if ([string]::IsNullOrEmpty($textBox.Text)) {
                        # �ړ��� + �^�C���X�^���v��ϐ��Ɋi�[
                        $AutoName = $Header + $(get-date -Format 'yyyyMMdd-HHmmss')

                        # �t�@�C������ړ��� + �^�C���X�^���v�ŕۑ�
                        saveSS $AutoName
                        $picForm.Dispose()
                    } ### �󂶂�Ȃ��ꍇ ###
                    else {
                        # ���͒l���������ǂ������`�F�b�N
                        if ($seqTextBox.Text -match "^\d+$") {
                            # �����̏ꍇ�A�A�Ԃ��C���N�������g
                            $global:seqNum = [int]$seqTextBox.Text + 1
                            $global:seqNumzero = "{0:000}" -f $global:seqNum
                            $seqName = $textBox.Text + "_" + $seqTextBox.Text
                            $global:recentText = $textBox.Text

                            # �X�N���[���V���b�g��ۑ�
                            saveSS $seqName
                            $picForm.Dispose()
                        }
                        else {
                            # �����ȊO�̏ꍇ�A�G���[���b�Z�[�W��\��
                            [void][System.Windows.Forms.MessageBox]::Show("�A�Ԃɂ͐�������͂��Ă��������B", "�G���[", "OK", "Error")

                            # �A�Ԃ��f�t�H���g�l�i1�j�ɐݒ�
                            $global:seqNum = 1
                            $global:seqNumzero = "{0:000}" -f $global:seqNum
                            $seqTextBox.Text = $global:seqNumzero
                        }
                    }
                }
            }
            # �L�����Z���{�^���̐ݒ�
            $CancelButton = New-Object System.Windows.Forms.Button
            $CancelButton.Location = New-Object System.Drawing.Point(225, 113)
            $CancelButton.Size = New-Object System.Drawing.Size(75, 30)
            $CancelButton.Text = "�L�����Z��"
            $CancelButton.FlatStyle = "System"
            $inputForm.CancelButton = $CancelButton
            $inputForm.Controls.Add($CancelButton)
            $CancelButton.Add_Click{
                Set-Clipboard
                $picForm.Dispose()
            }

            # �A�ԃ��Z�b�g�{�^���̐ݒ�
            $ResetButton = New-Object System.Windows.Forms.Button
            $ResetButton.Location = New-Object System.Drawing.Point( 200, 76)
            $ResetButton.Size = New-Object System.Drawing.Size(100, 30)
            $ResetButton.Text = "�A�ԏ�����"
            $ResetButton.FlatStyle = "System"
            $inputForm.Controls.Add($ResetButton)


            # ���Z�b�g�{�^���̃N���b�N�C�x���g
            $ResetButton.Add_Click{
                $global:seqNum = 1
                $global:seqNumzero = "{0:000}" -f $global:seqNum
                $seqTextBox.Text = $global:seqNumzero
            }

            $inputForm.Add_Shown({ $this.Activate() })
            [void]$inputForm.ShowDialog()
        })
    # �t�H�[����\��
    [void]$picForm.ShowDialog()
}

# ============================================���C������============================================

# �ۑ���f�B���N�g�������݂���ꍇ�A���������s
if ($(Test-Path $saveDir)) {
    Write-Host ===================================================================
    Write-Host "                        SS�ۑ��X�N���v�g"
    Write-Host ===================================================================
    Write-Host ""
    Write-Host "  �N���b�v�{�[�h�̉摜�f�[�^���Ď����ł��B"
    Write-Host "  �X�N���[���V���b�g���s���ƃt�@�C���ۑ���ʂ��\������܂��B"
    Write-Host "  �X�N���v�g���~����ɂ͂��̃R���\�[����Ctrl-C���������Ă��������B"
    Write-Host ""
    Write-Host -------------------------------------------------------------------
    Write-Host ""

    # �N���b�v�{�[�h������
    Set-Clipboard

    # �A�ԏ�����
    $global:seqNum = 1
    $global:seqNumzero = "{0:000}" -f $seqNum

    # �N���b�v�{�[�h�Ď�
    try {
        while ($true) {
            # �N���b�v�{�[�h�Ƀf�[�^���܂܂�Ă��邩���Ď�
            :clipMon while ([Windows.Forms.Clipboard]::ContainsImage() -eq $True) {
                # �N���b�v�{�[�h�̓��e���e�L�X�g�`���̏ꍇ�Ƀf�[�^��ϐ��Ɋi�[(�e�L�X�g�R�s�[���̋�������)
                $clipData = Get-Clipboard -Format Text

                # �N���b�v�{�[�h�Ƀf�[�^���܂܂�Ă���A����clipData�ϐ�����̏ꍇ�AImage�ϐ��ɉ摜�f�[�^���i�[
                if ($null -eq $clipData) {
                    $image = [Windows.Forms.Clipboard]::GetImage()
                }
                # �e�L�X�g�`���̃f�[�^���N���b�v�{�[�h�ɂ���ꍇ�A�����������[�v�𔲂���
                else {
                    # 0.1�b�ҋ@
                    Start-Sleep -Milliseconds 100

                    continue :clipMon
                }

                # �t�H�[���\��
                showForm

                # ���̃��[�v��
                continue :clipMon
            }
            # 0.1�b�ҋ@
            Start-Sleep -Milliseconds 100
        }
    }
    catch {
        # �G���[���b�Z�[�W�𐶐�
        $errorMessage = @"
�G���[�̎��: $($_.Exception.GetType().FullName)
�G���[���b�Z�[�W: $($_.Exception.Message)
�X�^�b�N�g���[�X: $($_.Exception.StackTrace)
��������: $(Get-Date)
"@

        # �G���[���b�Z�[�W��\��
        [void][System.Windows.Forms.MessageBox]::Show($errorMessage, "�G���[", "OK", "Error")
    }
    finally {
        # �X�N���v�g��~
        exit 0
    }
}
# �ۑ���t�H���_�����݂��Ȃ��ꍇ�A�������~
else {
    # �G���[���b�Z�[�W���o��
    [void][System.Windows.Forms.MessageBox]::Show("�ۑ���t�H���_�̐ݒ肪�s���ł��B", "�G���[", "OK", "Error")

    # �X�N���v�g��~
    exit 1
}
