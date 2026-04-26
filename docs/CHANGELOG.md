# Changelog

## v3.2 (2026-04-26)

### 🚀 パフォーマンス改善
- **DLL キャッシング** — 初回実行時にコンパイル済み DLL を `src/.cache/` に保存し、次回以降の起動時間を大幅に短縮。
- **高速画像ハッシュ計算** — `MemoryStream` 経由の BMP 保存を廃止し、`LockBits` + `Marshal.Copy` によるダイレクトアクセスで重複検知を高速化。
- **アイコンの遅延読み込み** — エクスプローラーのアイコンを起動時の一括読み込みから、必要に応じた遅延読み込みに変更。

### 🏗️ リファクタリング
- **名前空間の標準化** — 全ソースファイルの名前空間を `PowerShot.App`, `PowerShot.Controllers`, `PowerShot.Models`, `PowerShot.Utils` に整理。
- **`SettingsManager` 抽出** — 設定のロード/保存ロジックを `AppSettings.cs` から独立したファイルに抽出。
- **PowerShell スクリプトの簡略化** — アセンブリ参照の構文を整理。

### 🐞 バグ修正
- **再コンパイルガードの修正** — 名前空間変更に伴い、古い型名（`PowerShot.Program`）を参照していたガードロジックを修正。

### 🧪 テスト
- **xUnit テストスイートの追加** — `FileNamingLogic`, `SequenceManager`, `SettingsManager`, `FileManager`, `ViewerController`, `CropController`, `OverlayRenderer` を対象とした網羅的なテストを追加。
- `build-check.ps1` を `tests/` ディレクトリに移動。

---

## v3.1 (2026-04-26)

### セキュリティ修正
- **XSS 脆弱性修正** — `viewer_template.html` の `innerHTML` による動的レンダリングを `textContent` / `appendChild` に置換し、悪意あるファイル名によるスクリプト実行を防止
- **パストラバーサル修正** — `MainWindowController` のディレクトリ境界チェックで末尾セパレータなしの `StartsWith` 比較が `C:\Photos` に対して `C:\Photos_evil` をマッチさせてしまう脆弱性を修正
- **空の catch ブロック修正** — `AppSettings`・`OverlayRenderer`・`ClipboardWatcher` の素の `catch` を `catch (Exception ex)` + ログ出力に変更し、サイレント障害を排除

### パフォーマンス改善
- **ディレクトリ列挙の N+1 I/O 解消** — `Directory.GetLastWriteTime()` の個別呼び出しを `DirectoryInfo.LastWriteTime` に統合
- **`EnumerateDirectories`/`EnumerateFiles` 採用** — 中間配列アロケーションを削減
- **`ImageCodecInfo.GetImageDecoders()` キャッシュ** — 画像保存ごとの不要な再取得を防止
- **`MemoryStream.ToArray()` コピー排除** — ハッシュ計算時の不要なバッファコピーを `ms.Position = 0` + ストリーム直接渡しに変更

### リファクタリング
- **`FileNamingLogic` 分離** — ファイル名生成・バリデーションロジックを `FileManager` から独立クラスに抽出し、テスト可能な `DateTime` 注入オーバーロードを追加
- **`CreateExplorerItem` ヘルパー抽出** — `RefreshExplorer` 内の重複するアイテム生成コードを共通メソッドに統合
- **`SequenceManager` 簡略化** — 連番取得ループの深いネストを `Math.Max` で平坦化
- **`Array.Exists` 採用** — `ClipboardWatcher` の Excel フォーマット判定ループを簡潔化

### テスト追加
- `FileNamingLogic` — ファイル名生成・バリデーション（全禁則文字）の網羅テスト
- `SequenceManager.GetNextSequence` — 空ディレクトリ・プレフィックス・連番ギャップ・大文字小文字などのエッジケース
- `SettingsManager.Load` — 欠損ファイル・不正 JSON・`JpegQuality` 境界値・`SaveFolder` 空値のフォールバック
- `OverlayRenderer.GetPosition` — TopLeft/TopRight/BottomLeft/BottomRight・無効位置・境界外クランプ

---

## v3.0
PowerShot v3.0 は、v2.0 の「キャプチャ→保存」というシンプルなフローを保ったまま、**画像編集・設定永続化・独立型ビューア** を追加した大型アップデートです。

### ✨ 新機能
- **画像編集機能** — トリミング（ドラッグ操作対応）およびオーバーレイ（テキスト・システム情報の合成）機能を追加。
- **設定画面** — 保存先フォルダ、JPEG品質、タイムスタンプ形式などをUIから変更可能になり、`settings.json` に保存。
- **独立型ビューア** — ブラウザで動作する軽量な画像ビューア（`powershot-viewer.bat`）を同梱。
- **イベント駆動監視** — クリップボード監視を `WM_CLIPBOARDUPDATE` によるイベント駆動に変更し、低負荷化。
- **重複検知・フィルタ** — 同一画像の連続ポップアップ防止や、Excelセルコピー・極小画像の除外ロジックを追加。
- **DPI 対応** — 高 DPI 環境での座標ズレを解消。

### 🏗️ 内部アーキテクチャ
- 単一スクリプト構成から、App/Controllers/Models/Utils/Views に整理されたモジュラー構造へ刷新。
- **OverlayRenderer / CropController** などのロジックを独立クラスとして分離。
- XAML ファイルの動的ロード方式を採用。

### ⌨️ ショートカットキー
- **Shift + PrintScreen** — カーソルのあるモニターを即座にキャプチャする独自機能を追加。
