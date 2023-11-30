# PowerShot

## 説明
スクリーンショットを効率的に保存するためのPowerShellスクリプトと起動用バッチです。
クリップボードの画像データを監視し、スクリーンショットを行うとファイル保存画面と取得した画像を表示します。
保存先フォルダ、ファイル名の接頭辞(保存画面でファイル名を指定しない場合)を設定する場合は$saveDir、$headerの値を変更してください。
デフォルトではスクリプトと同じディレクトリの"Screenshots"フォルダが保存先で、接頭辞は"SS_"です。
このスクリプトはWindows11 PowerShell 5.1でのみ動作確認しています。

## 使い方
1. リポジトリをクローンして適当なフォルダに配置します。
2. powershot.batを実行します。

![image](https://github.com/rew552/PowerShot/assets/63663957/769f4460-c05d-4b0f-9b9b-cfae5dff5884)

3. スクリーンショットを取得します。(PrintScreen、Alt + PrintScreen、Win + Shift + S等)
4. ファイル保存画面にファイル名を入力し、"Save"をクリックもしくはEnterキーを押下します。
   ※1 2回目以降は連番が自動で加算されます。
   ※2 ファイル名を指定しない場合は、スクリプトの$header変数に定義した接頭辞とタイムスタンプで命名されます。
   ※3 画像ファイルはスクリプトの$saveDirに定義したファイルパスに保存されます。
   
![image](https://github.com/rew552/PowerShot/assets/63663957/bb13f7b7-8ed4-4b6c-abd4-4684c13f16cd)

5. 停止する場合はコンソールでCtrl-Cを押下してください。
