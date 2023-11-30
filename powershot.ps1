<#
    .SYNOPSIS
        クリップボードの画像データを監視し、スクリーンショットを行うとファイル保存画面と取得した画像を表示します。

    .DESCRIPTION
        クリップボードの画像データを監視し、スクリーンショットを行うとファイル保存画面と取得した画像を表示します。
		保存先フォルダ、ファイル名の接頭辞(保存画面でファイル名を指定しない場合)を設定する場合は$saveDir、$headerの値を変更してください。
		デフォルトではスクリプトと同じディレクトリの"Screenshots"フォルダが保存先で、接頭辞は"SS_"です。
		このスクリプトはWindows11 PowerShell 5.1でのみ動作確認しています。

    .PARAMETER Path
        なし

    .INPUTS
        なし

    .OUTPUTS
		なし

    .EXAMPLE
		1. リポジトリをクローンして適当なフォルダに配置します。
		2. powershot.batを実行します。
		3. スクリーンショットを取得します。(PrintScreen、Alt + PrintScreen、Win + Shift + S等)
		4. 画像のプレビューとファイル保存画面が表示されるため、ファイル名を入力し"Save"をクリックもしくはEnterキーを押下します。
			※1 2回目以降は連番が自動で加算されます。
			※2 ファイル名を指定しない場合は、スクリプトの$header変数に定義した接頭辞とタイムスタンプで命名されます。
			※3 画像ファイルはスクリプトの$saveDirに定義したファイルパスに保存されます。
		5. 停止する場合はコンソールでCtrl-Cを押下してください。

    .NOTES
		作成者: Rew552
		作成日: 2023/12/01
		バージョン: 1.0
#>

# パスの定義
$scriptPath = Split-Path $MyInvocation.MyCommand.Path

# ★保存先フォルダの定義★
$saveDir = $scriptPath + "\ScreenShots"
#$saveDir = "任意のパス"

# ★ファイル名の接頭辞★
$header = "SS_"

# アセンブリのロード
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
[System.Windows.Forms.Application]::EnableVisualStyles();

# ============================================関数作成============================================

# スクリーンショット保存関数
Function saveSS($FileName) {

	# ファイル名に拡張子を付与
	$fileExt = ($FileName) + ".png"

	# フルパスにする
	$imagePath = Join-Path $saveDir $fileExt

	# ファイル名が重複している場合
	if ($(Test-Path $imagePath)) {
		# エラーメッセージを出力
		[void][System.Windows.Forms.MessageBox]::Show("ファイル名が重複しています。", "エラー", "OK", "Error")

		# 連番カウントしない
		$global:seqNum = [int]$seqTextBox.Text
		$global:seqNumzero = "{0:000}" -f $global:seqNum

	}
	else {
		# スクリーンショットを保存
		$image.Save($imagePath, [System.Drawing.Imaging.ImageFormat]::Png)
		Write-Host "Saved:"$imagePath"`r`n"

		# クリップボードを空にする
		Set-Clipboard

		# 変数解放
		$image = $null
		Remove-Variable image -ErrorAction SilentlyContinue
	}
}

# フォーム作成関数
Function showForm() {
	# ピクチャーボックス フォーム全体の設定
	$scrWidth = [System.Windows.Forms.Screen]::PrimaryScreen.WorkingArea.Width
	$scrHeight = [System.Windows.Forms.Screen]::PrimaryScreen.WorkingArea.Height
	$picWidth = $scrWidth / 3
	$picHeight = $scrHeight / 3
	$picForm = New-Object System.Windows.Forms.Form
	$picForm.ShowInTaskbar = $False
	$picForm.FormBorderStyle = "None"
	$picForm.Size = "$picWidth,$picHeight"
	$picForm.StartPosition = "manual"
	$picForm.Location = "20,$picHeight"

	# ピクチャーボックス作成
	$picBox = New-Object System.Windows.Forms.PictureBox
	$picBox.Size = "$picWidth,$picHeight"
	$picBox.SizeMode = [System.Windows.Forms.PictureBoxSizeMode]::Zoom
	$picBox.Image = $image
	$picForm.Controls.Add($picBox)

	# サブフォーム
	$picForm.Add_Shown({
			# フォントの指定
			$font = New-Object System.Drawing.Font("Yu Gothic UI", 11)

			# フォーム全体の設定
			$inputForm = New-Object System.Windows.Forms.Form
			$inputForm.Text = ">_ PowerShot"
			$inputForm.Size = New-Object System.Drawing.Size(340, 205)
			$inputForm.StartPosition = "CenterScreen"
			$inputForm.BackColor = "white"
			$inputForm.FormBorderStyle = "FixedDialog"
			$inputForm.MaximizeBox = $False
			$inputForm.MinimizeBox = $False
			$inputForm.ControlBox = $False
			$inputForm.TopLevel = $True
			$inputForm.font = $font
			$inputForm.Owner = $picForm

			# ラベルを表示
			$label = New-Object System.Windows.Forms.Label
			$label.Location = New-Object System.Drawing.Point(20, 10)
			$label.Size = New-Object System.Drawing.Size(270, 20)
			$label.Text = "ファイル名を入力(拡張子不要)"
			$inputForm.Controls.Add($label)

			# 入力ボックスの設定
			$textBox = New-Object System.Windows.Forms.TextBox
			$textBox.Location = New-Object System.Drawing.Point(20, 40)
			$textBox.Size = New-Object System.Drawing.Size(280, 30)
			$textBox.font = $font
			$textBox.Text = $recentText
			$textBox.Select($recentText.Length, 1)
			$inputForm.Controls.Add($textBox)

			# ラベルを表示
			$seqlabel = New-Object System.Windows.Forms.Label
			$seqlabel.Location = New-Object System.Drawing.Point(20, 85)
			$seqlabel.Size = New-Object System.Drawing.Size(270, 20)
			$seqlabel.Text = "連番(0埋め3桁)"
			$inputForm.Controls.Add($seqlabel)

			# 入力ボックスの設定
			$seqTextBox = New-Object System.Windows.Forms.TextBox
			$seqTextBox.Location = New-Object System.Drawing.Point(20, 115)
			$seqTextBox.Size = New-Object System.Drawing.Size(80, 30)
			$seqTextBox.font = $font
			$seqTextBox.Text = $global:seqNumzero
			$inputForm.Controls.Add($seqTextBox)

			# 保存ボタンの設定
			$OKButton = New-Object System.Windows.Forms.Button
			$OKButton.Location = New-Object System.Drawing.Point(135, 113)
			$OKButton.Size = New-Object System.Drawing.Size(75, 30)
			$OKButton.Text = "Save"
			$OKButton.FlatStyle = "System"
			$inputForm.AcceptButton = $OKButton
			$inputForm.Controls.Add($OKButton)
			$OKButton.Add_Click{

				### テキストボックスが空の場合 ###
				if ([string]::IsNullOrEmpty($textBox.Text)) {

					# 接頭辞 + タイムスタンプを変数に格納
					$AutoName = $Header + $(get-date -Format 'yyyyMMdd-HHmmss')

					# ファイル名を接頭辞 + タイムスタンプで保存
					saveSS $AutoName
					$picForm.Dispose()
				}
				### 空じゃない場合 ###
				else {
					# テキストボックスに入力したファイル名で保存 + 連番カウントアップ
					$global:seqNum = [int]$seqTextBox.Text + 1
					$global:seqNumzero = "{0:000}" -f $global:seqNum
					$seqName = $textBox.Text + "_" + $seqTextBox.Text
					$global:recentText = $textBox.Text

					# ファイル名に使用不可の文字を除外
					$invalidCharsPattern = [RegEx]::Escape([string][System.IO.Path]::GetInvalidFileNameChars())
					saveSS $([RegEx]::Replace($seqName, "[{0}]" -f $invalidCharsPattern, ''))
					$picForm.Dispose()
				}
			}

			# キャンセルボタンの設定
			$CancelButton = New-Object System.Windows.Forms.Button
			$CancelButton.Location = New-Object System.Drawing.Point(225, 113)
			$CancelButton.Size = New-Object System.Drawing.Size(75, 30)
			$CancelButton.Text = "Cancel"
			$CancelButton.FlatStyle = "System"
			$inputForm.CancelButton = $CancelButton
			$inputForm.Controls.Add($CancelButton)
			$CancelButton.Add_Click{
				Set-Clipboard
				$picForm.Dispose()
			}
			$inputForm.Add_Shown({ $this.Activate() })
			[void]$inputForm.ShowDialog()
		})
	# フォームを表示
	[void]$picForm.ShowDialog()
}

# ============================================メイン処理============================================

# 保存先ディレクトリが存在する場合、処理を実行
if ($(Test-Path $saveDir)) {

	Write-Host ===================================================================
	Write-Host "                        SS保存スクリプト"
	Write-Host ===================================================================
	Write-Host ""
	Write-Host "  クリップボードの画像データを監視中です。"
	Write-Host "  スクリーンショットを行うとファイル保存画面が表示されます。"
	Write-Host "  スクリプトを停止するにはこのコンソールでCtrl-Cを押下してください。"
	Write-Host ""
	Write-Host -------------------------------------------------------------------
	Write-Host ""

	# クリップボード初期化
	Set-Clipboard

	# 連番初期化
	$global:seqNum = 1
	$global:seqNumzero = "{0:000}" -f $seqNum

	# クリップボード監視
	try {
		while ($true) {
			# クリップボードにデータが含まれているかを監視
			:clipMon while ([Windows.Forms.Clipboard]::ContainsImage() -eq $True) {

				# クリップボードの内容がテキスト形式の場合にデータを変数に格納(テキストコピー時の挙動制御)
				$clipData = Get-Clipboard -Format Text

				# クリップボードにデータが含まれており、かつclipData変数が空の場合、Image変数に画像データを格納
				if ($null -eq $clipData) {
					$image = [Windows.Forms.Clipboard]::GetImage()
				}
				# テキスト形式のデータがクリップボードにある場合、何もせずループを抜ける
				else {
					continue :clipMon
				}
				# フォーム表示
				showForm

				# 次のループへ
				continue :clipMon
			}
			# 0.1秒待機
			Start-Sleep -Milliseconds 100;
		}
	}

	# エラー処理
	catch [System.Net.WebException], [System.IO.IOException] {
		[void][System.Windows.Forms.MessageBox]::Show("予期しないエラーが発生しました。", "エラー", "OK", "Error")

		# スクリプト停止
		exit 1
	}
	finally {
		# スクリプト停止
		exit 0
	}

	# 保存先フォルダが存在しない場合、処理を停止
	else {
		# エラーメッセージを出力
		[void][System.Windows.Forms.MessageBox]::Show("保存先フォルダの設定が不正です。", "エラー", "OK", "Error")

		# スクリプト停止
		exit 1
	}
}
