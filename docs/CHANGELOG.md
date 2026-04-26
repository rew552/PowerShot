# Changelog

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

初期リリース。WPF ハイブリッドアーキテクチャ、設定システム、クリップボード監視、オーバーレイレンダリング、ビューアー機能を実装。
