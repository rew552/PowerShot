# インフラ性能分析ツール 基本設計書（バックエンド）

### 1. 概要

#### 1.1. 目的

本ツールは、企業の IT インフラ（ネットワーク機器、ストレージ、仮想化基盤）からパフォーマンスデータを収集・蓄積・分析するための中・小規模向け分析ツールである。性能問題の調査、ボトルネックの特定、およびキャパシティプランニングを支援することを目的とする。

#### 1.2. 分析スコープ

分析対象は、OS レイヤー（ゲスト OS 内部）より下のインフラ層に限定する。OS 内部のプロセス監視やアプリケーションログ分析は、他の専門ツールの領域とし、本ツールのスコープ外とする。

ツールの専門性を高め、インフラ担当者が真に必要とするメトリクス（ハードウェア、ハイパーバイザー、仮想 I/O）の分析に特化する。

---

### 2. アーキテクチャ

#### 2.1. 実行環境

**Windows OS に限定**する。

多くの企業環境でのクライアント OS の標準であり、タスクスケジューラ（`schtasks.exe`）など OS 固有の機能と連携するため、実行環境を限定し安定性を高める。

#### 2.2. 開発言語・バージョン

**Python 3.13.x** を採用する。

本書作成時点（2025 年 11 月）において、最新の安定版であり、`Streamlit`, `DuckDB`, `Pandas` を含む主要な依存ライブラリとの互換性が広く担保されている。

#### 2.3. 配布形態

**ポータブルなフォルダ形式**を採用する。

- **概要:** Python Embedded 環境、すべての依存ライブラリ、および起動用バッチファイルを単一のフォルダに同梱して配布する。
  インストーラーを不要とし、フォルダをコピーするだけで実行可能な手軽さを実現する。単一ファイル化（PyInstaller）は、設定ファイルやデータディレクトリの管理が複雑化するため採用しない。

#### 2.4. 分析 UI（インターフェース）

**Streamlit** を採用する。

- **起動方法:** `start_analysis.bat` 等のバッチファイルを実行すると、ローカルで Streamlit サーバーが起動し、自動的にユーザーのデフォルトブラウザで分析画面（`http://localhost:8501`）が開く形式とする。
  Python のみで高速にデータ分析 UI を構築でき、DuckDB や Pandas との親和性が非常に高い。
- **初回起動時の処理:** `config.yml` が存在しない場合、初回セットアップ画面を表示し、ユーザーは以下のいずれかを選択できる:
  - 通常のセットアップ:
    1. データストレージの保存先ディレクトリを指定
    2. SNMP または vSphere のいずれかのコレクターを有効化するか選択
    3. 有効化したコレクターの基本設定（SNMP: 監視対象ホスト、vSphere: vCenter 接続情報）を入力
    4. 設定完了後、`config.yml` を生成し、通常の設定画面に遷移
  - **セットアップをスキップして閲覧モードで開始:**
    - データ収集や設定変更機能は利用できないが、既存の計測データの参照・分析・ダッシュボード機能のみ利用可能な「閲覧モード」として起動する
    - **後からでもコレクターの設定や通常モードへの移行が可能**。設定画面で `config.yml` の新規作成または編集を行うことで、いつでもフル機能（データ収集・設定変更等）を有効化できる

#### 2.5. 分析エンジン

**DuckDB** をライブラリとして組み込む。

大量の時系列データ（例: 100 ホスト超, 20 秒間隔）に対する分析クエリ（OLAP）において、行ストア DB（SQLite 等）よりも劇的に高速なパフォーマンスを発揮する。

#### 2.6. データ保存形式

**Parquet ファイル群**を採用する。

DuckDB の分析エンジンと最も相性の良いカラム（列）型ストレージ形式である。分析クエリ実行時に不要な列（`metric_name`等）の I/O を完全にスキップでき、高いデータ圧縮率も実現する。

#### 2.7. 依存ライブラリ一覧

本ツールで使用する主要な Python ライブラリ（サードパーティおよび標準ライブラリ）は以下の通り。

- **データ収集:**
  - `pysnmp` (v5.0.0 以上): SNMP プロトコル実装
  - `pyVmomi` (v8.0.0 以上): vSphere API クライアント
- **データ処理・分析:**
  - `pandas` (v2.0.0 以上): データフレーム操作
  - `duckdb` (v0.9.0 以上): 分析エンジン
  - `pyarrow` (v14.0.0 以上): Parquet ファイル I/O
- **UI:**
  - `streamlit` (v1.28.0 以上): Web UI フレームワーク
  - `plotly` (v5.17.0 以上): グラフ可視化
- **Windows 統合:**
  - `pywin32` (v306 以上): Windows サービス実装、タスクスケジューラ制御
- **セキュリティ:**
  - `keyring` (v24.0.0 以上): Windows Credential Manager 連携
- **その他 サードパーティ:**

  - `pyyaml` (v6.0 以上): 設定ファイル（YAML）読み書き
  - `python-dateutil` (v2.8.0 以上): 日時処理
  - `kaleido` (v0.2.1 以上): plotly のグラフ画像エクスポート
  - `reportlab` (v4.0.0 以上): PDF 生成（表やテキストと画像を組み合わせる場合）

- **主要な標準ライブラリ:**
  - `zipfile`: データエクスポート時の ZIP 圧縮処理
  - `os`, `pathlib`: ファイル・ディレクトリ操作
  - `shutil`: ファイル・ディレクトリのコピー、削除
  - `subprocess`: Windows サービス・タスク操作／外部コマンド実行
  - `logging`: 各種ログ出力と管理
  - `json`: JSON ファイル（設定・中間ファイル）読み書き
  - `datetime`: 日付・時刻処理
  - `threading`, `concurrent.futures`: 並列処理（必要に応じて）
  - `sys`, `platform`: 実行環境判定・終了処理 等
  - `time`: 時間遅延、タイマー処理

※ 上記以外にも、適宜標準ライブラリを補助的に利用

#### 2.8. ディレクトリ構造

ツールの配布フォルダは以下の構造とする。

```
InfraAnalysisTool/
├── python/                    # Python Embedded 環境
│   ├── python.exe
│   └── ...
├── Lib/                       # Python 標準ライブラリ
├── Scripts/                   # 依存ライブラリ（site-packages）
├── collectors/                # コレクタープロセス
│   ├── snmp_collector.py
│   ├── vsphere_collector.py
│   └── common/                # 共通モジュール
│       ├── config_loader.py
│       ├── data_buffer.py
│       └── parquet_writer.py
├── maintenance/               # メンテナンスジョブ
│   └── maintenance_job.py     # マージ・リテンション処理
├── ui/                        # Streamlit UI
│   ├── app.py                 # メインアプリケーション
│   ├── pages/                 # 各ページ
│   │   ├── setup.py           # 初回セットアップ画面
│   │   ├── dashboard.py
│   │   ├── analysis.py
│   │   └── settings.py
│   └── backend/               # バックエンドロジック
│       ├── query_engine.py
│       ├── report_generator.py
│       └── config_manager.py
├── config/                    # 設定ファイル
│   ├── config.yml
│   └── collector_config.json
├── data_storage/              # データ保存先（ユーザーが指定可能）
│   └── date=YYYY-MM-DD/...
├── logs/                      # ログファイル
│   ├── snmp_collector.log
│   ├── vsphere_collector.log
│   ├── maintenance_job.log
│   └── ui.log
├── start_analysis.bat         # UI 起動スクリプト
├── install_service.bat        # Windows サービス登録スクリプト
├── install_maintenance_job.bat # メンテナンスジョブ登録スクリプト
├── reset_tool.bat             # OS 設定リセットスクリプト
└── README.md
```

#### 2.9. 起動スクリプトの詳細

- **`start_analysis.bat`:**

  - Streamlit サーバーを起動し、デフォルトブラウザで `http://localhost:8501` を開く
  - 実装例: `python\python.exe -m streamlit run ui/app.py --server.port 8501 --browser.gatherUsageStats false`
  - **注意:** 通常はこのスクリプトから起動し、GUI から各種設定・管理操作を実行する。

- **`install_service.bat`（オプション）:**

  - SNMP コレクターを Windows サービスとして登録
  - `pywin32` の `win32serviceutil` を使用してサービスを登録
  - 管理者権限で実行する必要がある
  - **注意:** 通常は GUI（設定画面）から実行する。コマンドラインから実行する場合のみ使用する。

- **`install_maintenance_job.bat`（オプション）:**

  - メンテナンスジョブを Windows タスクスケジューラに登録
  - `subprocess` モジュールを使用して `schtasks.exe` を実行し、1 時間ごとに実行されるタスクを登録
  - 管理者権限で実行する必要がある
  - **注意:** 通常は GUI（設定画面）から実行する。コマンドラインから実行する場合のみ使用する。

- **`reset_tool.bat`（オプション）:**
  - 本ツールで変更した OS 設定をリセット（Windows サービス、タスクスケジューラ、認証情報の削除）
  - 管理者権限で実行する必要がある
  - **注意:** 通常は GUI（設定画面）から実行する。コマンドラインから実行する場合のみ使用する。
  - 詳細は 8.6 節を参照

---

### 3. データ収集プロセス（コレクター）

#### 3.1. 共通仕様

- **実行形態:** 分析 UI（Streamlit）とは**完全に分離**した、バックグラウンドの Python プロセスとして定義する。
  データ収集の安定性・継続性を確保するため。分析 UI を起動していなくても、データ収集がバックグラウンドで継続される。
- **実行管理:** 各コレクターの実行方法は、コレクターの特性に応じて異なる（SNMP コレクターは Windows サービス、vSphere コレクターは Windows タスクスケジューラ）。詳細は各コレクターの節を参照。
- **データバッファ:** データ収集プロセスは、メモリ効率の良い **Pandas DataFrame** をデータバッファとして使用する。
  大量の時系列データを 30 分間メモリに保持するため、Pandas のメモリ効率の良いデータ構造を必須とする。

#### 3.2. SNMP コレクター

- **ライブラリ:** `pysnmp` (v5.0.0 以上)
- **サポートプロトコル:** SNMPv2/v2c のみをサポートする（SNMPv1 および SNMPv3 は非対応）

##### 3.2.1. 実行方法（Windows サービス）

1.  **コレクター本体 (`snmp_collector.py`):**

    - 起動すると、それ自体が**常駐プロセス**となり、内部タイマー（20 秒間隔）で収集ループを開始する。
    - `win32serviceutil.ServiceFramework` を継承したクラスとして実装する。
    - `SvcDoRun()` メソッド内で収集ループを実行する。

2.  **サービス登録:**

    - ツール（UI または初回セットアップスクリプト）は、`pywin32` の `win32serviceutil.HandleCommandLine()` を使用して、この `snmp_collector.py` を **Windows サービス**として登録する。
    - サービス名: `InfraAnalysisTool_SNMPCollector`
    - サービスは **SYSTEM アカウント**で実行され、起動タイプを「**自動**」に設定する。
    - サービス登録コマンド例: `python snmp_collector.py install`

3.  **多重起動防止:**
    - `snmp_collector.py` は、起動時に `win32event.CreateMutex()` を使用して Mutex を作成し、自身のプロセスがすでに実行中でないか確認する。
    - Mutex 名: `Global\InfraAnalysisTool_SNMPCollector`
    - 既に実行中の場合は、エラーログを出力して終了する。

- **設計理由:** 20 秒間隔の収集はタスクスケジューラの起動オーバーヘッドが大きすぎるため、常駐プロセス方式を採用する。Windows サービスとして SYSTEM アカウントで実行することで、ユーザーログオフ後も継続的にデータ収集が実行され、OS 再起動後も自動で収集が再開される。これにより、耐障害性と可用性を確保する。

##### 3.2.2. 収集パラメータ

- **収集間隔:** 20 秒ごと（固定）
- **書き込み周期:** **30 分ごと**。収集データを 30 分間メモリにバッファリングし、1 つの Parquet ファイルとして書き出す。
- **SNMP タイムアウト:** 各ホストへの SNMP リクエストのタイムアウトは **3 秒**とする。
- **SNMP リトライ:** タイムアウト時は **1 回**リトライする（最大 2 回の試行）。

##### 3.2.3. 並列処理（マルチスレッド）

- **スレッドプール:**

  - `concurrent.futures.ThreadPoolExecutor` を使用する。
  - **スレッド数:** 対象ホスト数に応じて動的に決定。最小 5 スレッド、最大 50 スレッド。
  - 計算式: `min(max(対象ホスト数 // 10, 5), 50)`
  - **設計理由:** シングルスレッドでの逐次処理では、ネットワーク遅延やタイムアウトにより、収集間隔（20 秒）を到底守れないため。スレッド数を動的に調整することで、ホスト数が少ない場合のリソース浪費を防ぎ、多い場合の収集遅延を防ぐ。

- **収集バッチの実行:**
  - 各スレッドは 1 つのホストに対して SNMP GET/WALK を実行する。
  - 全ホストの収集が完了するか、20 秒のタイムアウトに達するまで待機する。
  - 20 秒以内に完了しなかったホストは、次回の収集サイクルで再試行する。

##### 3.2.4. データバッファ管理

- **バッファ構造:**

  - Pandas DataFrame を使用。カラム: `timestamp`, `host`, `metric_name`, `value`, `attributes`
  - 30 分間（約 90 レコード/ホスト）のデータを保持する。
  - メモリ使用量の目安: 100 ホスト × 10 メトリクス × 90 レコード × 100 バイト ≈ 9 MB（概算）

- **バッファフラッシュ:**
  - 30 分ごと、またはバッファサイズが 2GB を超過した場合に、Parquet ファイルとして書き出す。
  - **非同期書き込み:** ファイル書き込み処理は、20 秒間隔の収集ループをブロックしないよう、**別スレッド**で非同期に実行する。
    - 書き込み処理中も、収集ループは継続してバッファにデータを追加する。
    - 書き込み完了後、バッファをクリアする。
  - **トリガーファイル作成:** 新しい日付のパーティションディレクトリ（`date=YYYY-MM-DD`）に初めてデータを書き込む際、メンテナンスジョブ用のトリガーファイル（`logs/maintenance_trigger_YYYY-MM-DD.flag`）を作成する。

##### 3.2.5. ログ管理

- **ログファイル:** `logs/snmp_collector.log`
- ログレベル、フォーマット、ローテーション等の詳細は 8.5 節を参照。

#### 3.3. vSphere コレクター (仮想化基盤)

- **ライブラリ:** `pyVmomi` (v8.0.0 以上)

##### 3.3.1. 実行方法

- **実行方法:** Windows タスクスケジューラにより **30 分ごと** にバッチ実行される。
- **タスク名:** `InfraAnalysisTool_vSphereCollector`
- **実行アカウント:** **SYSTEM アカウント**（ログオフ後も継続実行を確保するため）
  - vCenter 認証情報は Windows Credential Manager に保存され、SYSTEM アカウントからもアクセス可能とする
- **多重起動防止:** 起動時にロックファイル（`logs/vsphere_collector.lock`）を確認し、既に存在する場合は処理をスキップする。処理完了時にロックファイルを削除する。
- **トリガーファイル作成:** 新しい日付のパーティションディレクトリ（`date=YYYY-MM-DD`）に初めてデータを書き込む際、メンテナンスジョブ用のトリガーファイル（`logs/maintenance_trigger_YYYY-MM-DD.flag`）を作成する。

##### 3.3.2. 収集対象

- **収集対象:** vCenter の `PerformanceManager` が提供する 20 秒粒度のリアルタイム統計。
- **対象選択（vSphere API 利用）:**
  - UI（設定画面）において、ユーザーが分析カテゴリ（**クラスタ単位**, **ESXi 単位**, **VM 単位**）を選択し、それぞれで監視対象を指定できる。
  - 監視対象の ID（ManagedObjectReference）は `config.yml` に保存される。

##### 3.3.3. 収集メトリクス定義

データ量および vCenter への負荷を管理するため、収集するメトリクスを以下の通り定義する。

- **ESXi ホスト (デフォルト有効):**

  - **CPU:**
    - `cpu.usage.average` (%): CPU 使用率
    - `cpu.readiness.average` (%): CPU Ready 時間
    - `cpu.costop.average` (%): Co-Stop 時間（後述の換算処理あり）
  - **Memory:**
    - `mem.usage.average` (%): メモリ使用率
  - **Disk:**
    - `disk.read.average` (MBps): 読み込みスループット（デバイス/データストア単位）
    - `disk.write.average` (MBps): 書き込みスループット（デバイス/データストア単位）
    - `disk.readLatency.average` (ms): 読み込みレイテンシ（デバイス/データストア単位）
    - `disk.writeLatency.average` (ms): 書き込みレイテンシ（デバイス/データストア単位）
  - **Network:**
    - `net.received.average` (MBps): 受信スループット（vmnic 単位）
    - `net.transmitted.average` (MBps): 送信スループット（vmnic 単位）

- **仮想マシン(VM):**
  - **CPU (デフォルト有効):**
    - `cpu.usage.average` (%): CPU 使用率
    - `cpu.readiness.average` (%): CPU Ready 時間
    - `cpu.costop.average` (%): Co-Stop 時間（後述の換算処理あり）
  - **Memory (デフォルト無効):**
    - `mem.usage.average` (%): メモリ使用率
  - **Disk (デフォルト無効):**
    - `disk.read.average` (MBps): 読み込みスループット（VM 合計値）
    - `disk.write.average` (MBps): 書き込みスループット（VM 合計値）
  - **Network (デフォルト無効):**
    - `net.received.average` (MBps): 受信スループット（VM 合計値）
    - `net.transmitted.average` (MBps): 送信スループット（VM 合計値）

##### 3.3.4. メトリクス単位の変換

- **`cpu.readiness.average`:** vCenter が提供する%値をそのまま使用する。
- **`cpu.costop.average` (%):** vCenter に%単位のメトリクスが無いため、`cpu.costop.summation` (ms) を取得し、コレクター側で以下の計算を行い、**%に換算**してから保存する。
  - 計算式: `(costop.summation / 20000) * 100`
  - 20 秒（20000 ms）を基準として、Co-Stop 時間の割合を計算する。

##### 3.3.5. QueryPerf API の使用方法

- **バッチ取得:**

  - `PerformanceManager.QueryPerf()` メソッドを使用し、複数の監視対象（ESXi/VM）のメトリクスを vCenter から**一括（バッチ）で取得**する。
  - すべての監視対象オブジェクトのメトリクスを、1 回の API 呼び出しで一括取得する。

- **取得期間:**

  - データ取得は毎時 2 回に分割して実施する。
    - 1 回目: 毎時 **0 分 0 秒〜29 分 40 秒** の範囲で取得（`startTime`：その時点の直近 0 分 0 秒、`endTime`：同 29 分 40 秒）。
    - 2 回目: 毎時 **30 分 0 秒〜59 分 40 秒** の範囲で取得（`startTime`：その時点の直近 30 分 0 秒、`endTime`：同 59 分 40 秒）。
  - 各範囲とも 20 秒間隔のデータポイントを取得する（`intervalId = 20`）。

- **タイムアウト:**
  - API 呼び出しのタイムアウトは **60 秒**とする。
  - タイムアウト時は、該当バッチをスキップし、ログに記録する。

##### 3.3.6. 並列処理

- **データパース処理の並列化:**

  - 取得した結果のパース処理（データ整形）がボトルネックになる場合、当該処理も**マルチスレッド**で並列化する。
  - スレッド数: CPU コア数に応じて動的に決定（最大 8 スレッド）。

API 呼び出しをオブジェクトごとに逐次実行すると、30 分の実行時間内に完了しない可能性があるため、API のバッチ機能を最大限に活用する。

##### 3.3.7. ログ管理

- **ログファイル:** `logs/vsphere_collector.log`
- ログレベル、フォーマット、ローテーション等の詳細は 8.5 節を参照。

#### 3.4. データ収集の堅牢性（エラーハンドリング）

- **3.4.1. 収集ループのオーバーラン対策（SNMP）:**
  - SNMP コレクター（常駐）は、マルチスレッドでの 1 回の収集バッチ（全対象ホストへの GET/WALK）にかかった時間を計測する。
  - 万が一、1 回のバッチが 20 秒を超過した場合（例: タイムアウト多発）、次の収集タイミングはスキップし、遅延をログに記録する。ループ完了後、次の 20 秒の区切りから収集を再開する。
    収集遅延が累積し、リソースを際限なく消費する「デススパイラル」状態に陥ることを防ぐため。
- **3.4.2. ジョブ多重起動防止（vSphere）:**
  - vSphere コレクターは、起動時にロックファイルを確認する。前回の収集・書き込み処理がまだ実行中の場合、今回のジョブは何もせず**スキップ**し、多重起動とリソース競合を防ぐ。
- **3.4.3. 処理遅延の検知・記録:**
  - 各コレクターは、Parquet ファイルへの書き込み処理が完了した時刻を、ログファイルに記録する。
- **3.4.4. UI への警告:**
  - Streamlit アプリは、この「最終書き込み完了時刻」を監視する。最終書き込み時刻が現在時刻から著しく（例: 1 時間以上）遅れている場合、UI 上に「**データ収集中または遅延の可能性があります**」といった警告メッセージを表示するバックエンドロジックを実装する。

---

### 4. データ保存（ストレージ設計）

#### 4.1. 設計思想 (パーティション列 vs データ列)

本ツールの分析性能は、DuckDB の「**パーティション・プルーニング（枝刈り）**」機能によって担保される。この機能を最大化するため、データ列を「**パーティション列**」と「**データ列**」に厳密に分離する。

- **① パーティション列 (フォルダ名として保存)**
  - **役割:** データを絞り込むための**主要な検索キー（インデックス）**。`WHERE`句で頻繁に使用される列。
  - **対象:** `date`, `host`。vSphere 環境では `cluster`, `esxi_host` も対象とする。
  - **動作:** DuckDB はクエリ実行時、まずフォルダ名を見て、`WHERE`句に合致しないフォルダ（パーティション）を**スキャン対象から除外**する。これにより、Parquet ファイルを開く前に処理対象を劇的に減らす。
- **② データ列 (Parquet ファイル内部に保存)**
  - **役割:** 実際の**測定値**および**メトリクスの文脈**を示す列。`SELECT`句や`AVG()`などの集計関数、またはドリルダウンで使用される。
  - **対象:** `timestamp`, `metric_name`, `value`, `attributes`。

#### 4.2. ファイル配置（Hive パーティショニング）

`key=value` 形式のディレクトリ名を採用する。

- **SNMP 機器の構造（例）:**
  ```
  data_storage/
  ├── date=2025-11-15/
  │   └── host=switch-01.local/
  │       └── snmp_metrics_1700.parquet
  ```
- **vSphere 機器の構造（例）:**
  ```
  data_storage/
  ├── date=2025-11-15/
  │   └── cluster=Cluster-A/
  │       ├── esxi_host=esxi-01.local/
  │       │   ├── host=esxi-01.local/
  │       │   │   └── vsphere_metrics_1700.parquet
  │       │   └── host=vm-web-01/
  │       │       └── vsphere_metrics_1700.parquet
  │       └── esxi_host=esxi-02.local/
  │           └── host=vm-db-01/
  │               └── vsphere_metrics_1700.parquet
  ```
  vSphere 環境において「クラスタ単位」「ESXi 単位」での高速な集計を可能にするため。

##### 4.2.1. ファイル命名規則

- **コレクター書き込み時のファイル名形式:** `{コレクター種別}_metrics_{時刻}.parquet`
  - コレクター種別: `snmp` または `vsphere`
  - 時刻: `HHMM` 形式（例: `1700` = 17:00）
  - 例: `snmp_metrics_1700.parquet`, `vsphere_metrics_1730.parquet`
- **マージ後のファイル名形式:** `{コレクター種別}_metrics_{日付}.parquet`
  - コレクター種別: `snmp` または `vsphere`
  - 日付: `YYYY-MM-DD` 形式（例: `2025-11-15`）
  - 例: `snmp_metrics_2025-11-15.parquet`, `vsphere_metrics_2025-11-15.parquet`
  - **注意:** マージ処理（8.3.2 節参照）により、1 日 48 個のファイルが 1 ファイルに統合される。
- **一時ファイル名:** 書き込み中は `{ファイル名}.tmp` として保存し、完了後にリネームする。
- **ファイルサイズの目安:**
  - コレクター書き込み時: 1 ファイルあたり 1-10 MB（ホスト数・メトリクス数による）
  - マージ後: 1 ファイルあたり 50-500 MB（1 日分のデータ統合後）

#### 4.3. vMotion 発生時の動作と分析

- **vMotion 時の動作:** vMotion により VM（例: `vm-web-01`）が `esxi-01` から `esxi-02` へ移動した場合、次の 30 分バッチ（`_1730.parquet`）は、`esxi_host=esxi-02/host=vm-web-01/` のパーティションに書き込まれる。
- **VM 中心の分析:** ユーザーが `WHERE host = 'vm-web-01'` で検索した場合、DuckDB は `esxi_host` パーティションを無視し、両方の場所からデータを集約して連続したグラフを返す。
- **ESXi 中心の分析:** ユーザーが `WHERE esxi_host = 'esxi-01'` で検索した場合、vMotion 後は `vm-web-01` の負荷が除外された、ESXi ホストの「あるがまま」の負荷が描画される。
- **vMotion 履歴の取得:** 「VM 中心の分析」時に、`SELECT`句にパーティションキーである `esxi_host` を含めることで、`timestamp`ごとの所属ホスト履歴（`esxi_host`列）も同時に取得可能。
  - **クエリ例:** `SELECT timestamp, value, esxi_host FROM 'data_storage/**/*.parquet' WHERE host = 'vm-web-01'`

#### 4.4. データスキーマ (Parquet ファイル内部)

Parquet ファイル内部には、**パーティション列（`date`, `host`, `cluster`, `esxi_host`）は含めない**。

##### 4.4.1. カラム定義

- **`timestamp` (TIMESTAMP):**

  - 測定日時（20 秒ごとの正確なタイムスタンプ）
  - 形式: ISO 8601（例: `2025-11-15 17:00:00`）
  - タイムゾーン: UTC（保存時は UTC、表示時はローカルタイムゾーンに変換）

- **`metric_name` (VARCHAR):**

  - メトリクス名（例: `cpu.usage.average`, `net.bps.in`, `disk.read.average`）
  - 命名規則: `{カテゴリ}.{項目}.{集計方法}` または `{カテゴリ}.{項目}`

- **`value` (DOUBLE):**

  - 測定値（正規化・計算後の値）
  - 単位はメトリクスごとに統一（CPU/メモリ: %, ネットワーク: bps, ディスク: MBps または ms）

- **`attributes` (VARCHAR, JSON 文字列):**
  - メトリクスの文脈（どの部品か）を示す属性の集合
  - **実装方式:** Parquet の MAP 型は DuckDB でサポートされているが、クエリの複雑さを避けるため、**JSON 文字列**として保存する
  - 例: `{"ifName": "GigabitEthernet0/1", "ifIndex": "10101"}`
  - DuckDB での JSON 解析: `attributes::JSON->>'ifName'` のように JSON 関数を使用

#### 4.5. `attributes` カラムの具体的な設計

`attributes` は、`host` と `metric_name` で絞り込んだ後、さらに「**どのインスタンス（部品）か**」を特定するための詳細情報を格納する。JSON 文字列として保存する。

- **SNMP (NW 機器):**

  - **`metric_name`:** `net.bps.in`, `net.bps.out`, `net.error.rate.in`, `net.error.rate.out`, `net.discard.rate.in`, `net.discard.rate.out`
  - **`attributes`:** `{"ifName": "GigabitEthernet0/1", "ifIndex": "10101", "ifAlias": "Uplink to CoreSW"}`
  - **理由:** どの物理ポートのトラフィック、エラー率、破棄率かを示すため。

- **ESXi (vmnic):**

  - **`metric_name`:** `net.received.average`, `net.transmitted.average`
  - **`attributes`:** `{"vmnic": "vmnic0"}`
  - **理由:** どの vmnic のトラフィックかを示すため。

- **ESXi (Disk):**

  - **`metric_name`:** `disk.read.average`, `disk.write.average`, `disk.readLatency.average`
  - **`attributes`:** `{"device": "naa.6000c29...", "datastore": "datastore1"}`
  - **理由:** どの物理デバイス/データストアのメトリクスかを示すため。

- **仮想マシン(VM) (Disk/Network):**
  - **`metric_name`:** `disk.read.average`, `net.received.average` など（合計値）
  - **`attributes`:** `{}` (空の JSON オブジェクト)
  - **理由:** VM のメトリクスは「合計値」として収集するため、個別のインスタンス（vDisk/vNIC）を区別する属性は不要。

#### 4.6. 書き込み競合対策

書き込みプロセスは、データをまず一時ファイル（`*.parquet.tmp`）として書き出す。書き込みが完全に完了した後、ファイルシステムのアトミックな**リネーム操作**（`os.rename()`）により、正規のファイル名（`*.parquet`）に変更する。

DuckDB が書き込み中の中途半端なファイルを読み取ることを防ぐため。

#### 4.7. DuckDB クエリ例

##### 4.7.1. 基本的な時系列データ取得

```sql
-- 特定ホストの CPU 使用率を取得
SELECT
    timestamp,
    value
FROM 'data_storage/date=2025-11-15/host=esxi-01.local/*.parquet'
WHERE metric_name = 'cpu.usage.average'
ORDER BY timestamp;
```

##### 4.7.2. パーティション・プルーニングを活用したクエリ

```sql
-- 複数ホストのデータを集約（パーティション・プルーニングにより、不要なフォルダをスキップ）
SELECT
    date,
    host,
    timestamp,
    AVG(value) as avg_cpu_usage
FROM 'data_storage/**/*.parquet'
WHERE date >= '2025-11-15'
  AND date <= '2025-11-20'
  AND host IN ('esxi-01.local', 'esxi-02.local')
  AND metric_name = 'cpu.usage.average'
GROUP BY date, host, timestamp
ORDER BY date, host, timestamp;
```

##### 4.7.3. attributes カラムの JSON 解析

```sql
-- 特定のインターフェース（ifName）のトラフィックを取得
SELECT
    timestamp,
    value,
    attributes::JSON->>'ifName' as interface_name
FROM 'data_storage/date=2025-11-15/host=switch-01.local/*.parquet'
WHERE metric_name = 'net.bps.in'
  AND attributes::JSON->>'ifName' = 'GigabitEthernet0/1'
ORDER BY timestamp;
```

##### 4.7.4. vMotion 対応のクエリ

```sql
-- VM 中心の分析（vMotion 後も連続したデータを取得）
SELECT
    timestamp,
    value,
    esxi_host
FROM 'data_storage/**/*.parquet'
WHERE host = 'vm-web-01'
  AND metric_name = 'cpu.usage.average'
ORDER BY timestamp;
```

---

### 5. データ処理・変換

生データ（カウンター値や KB 単位など）のままでは分析が困難なため、収集プロセスが Parquet に書き込む前に、人間が解釈しやすい「指標」に変換する。

#### 5.1. SNMP データ処理

##### 5.1.1. カウンター値の差分計算

- **bps（ビット/秒）の計算:**

  - 対象 OID: `ifInHCOctets` (1.3.6.1.2.1.31.1.1.1.6), `ifOutHCOctets` (1.3.6.1.2.1.31.1.1.1.10)
  - 計算式: `bps = ((現在値 - 前回値) * 8) / 取得間隔(秒)`
  - 例: 前回値 `1000000` オクテット、現在値 `1001000` オクテット、間隔 20 秒の場合
    - `bps = ((1001000 - 1000000) * 8) / 20 = 400 bps`

- **pps（パケット/秒）の計算:**

  - 対象 OID: `ifInErrors` (1.3.6.1.2.1.2.2.1.14), `ifOutErrors` (1.3.6.1.2.1.2.2.1.20), `ifInDiscards` (1.3.6.1.2.1.2.2.1.13), `ifOutDiscards` (1.3.6.1.2.1.2.2.1.19)
  - 計算式: `pps = (現在値 - 前回値) / 取得間隔(秒)`
  - 例: 前回値 `100` パケット、現在値 `105` パケット、間隔 20 秒の場合
    - `pps = (105 - 100) / 20 = 0.25 pps`

- **エラー率・破棄率（%）の計算:**
  - **エラー率:**
    - 対象 OID:
      - エラーパケット数: `ifInErrors` (1.3.6.1.2.1.2.2.1.14), `ifOutErrors` (1.3.6.1.2.1.2.2.1.20)
      - 総パケット数: `ifHCInUcastPkts` (1.3.6.1.2.1.31.1.1.1.7) + `ifHCInMulticastPkts` (1.3.6.1.2.1.31.1.1.1.8) + `ifHCInBroadcastPkts` (1.3.6.1.2.1.31.1.1.1.9)（受信）、`ifHCOutUcastPkts` (1.3.6.1.2.1.31.1.1.1.11) + `ifHCOutMulticastPkts` (1.3.6.1.2.1.31.1.1.1.12) + `ifHCOutBroadcastPkts` (1.3.6.1.2.1.31.1.1.1.13)（送信）
    - 計算式:
      - 受信エラー率: `error_rate_in = (ifInErrors差分 / 総受信パケット数差分) * 100`
      - 送信エラー率: `error_rate_out = (ifOutErrors差分 / 総送信パケット数差分) * 100`
    - 例: 受信エラーパケット差分 `10`、総受信パケット数差分 `100000` の場合
      - `error_rate_in = (10 / 100000) * 100 = 0.01%`
  - **破棄率:**
    - 対象 OID:
      - 破棄パケット数: `ifInDiscards` (1.3.6.1.2.1.2.2.1.13), `ifOutDiscards` (1.3.6.1.2.1.2.2.1.19)
      - 総パケット数: エラー率と同様
    - 計算式:
      - 受信破棄率: `discard_rate_in = (ifInDiscards差分 / 総受信パケット数差分) * 100`
      - 送信破棄率: `discard_rate_out = (ifOutDiscards差分 / 総送信パケット数差分) * 100`
    - 例: 受信破棄パケット差分 `5`、総受信パケット数差分 `100000` の場合
      - `discard_rate_in = (5 / 100000) * 100 = 0.005%`
  - **注意事項:**
    - 総パケット数差分が `0` の場合は、エラー率・破棄率を `0%` として記録する（ゼロ除算を防ぐため）

##### 5.1.2. カウンターのロールオーバー検知

- **ロールオーバー検知ロジック:**

  - カウンター値が 32 ビット（最大値: 4294967295）または 64 ビット（最大値: 18446744073709551615）の場合、最大値に達すると 0 に戻る（ロールオーバー）。
  - 検知条件: `現在値 < 前回値` かつ `前回値 > 最大値 * 0.9`（90% 以上使用していた場合）
  - ロールオーバー検知時: 差分を `0` として記録する（正確な計算は困難なため）。

- **異常値の処理:**
  - 負の差分値: `0` として記録（ロールオーバーまたは機器リセットの可能性）。
  - 異常に大きな差分値（例: 前回値の 10 倍以上）: `None`（NULL）として記録し、ログに警告を出力。

##### 5.1.3. Gauge 値の処理

- **Gauge 型 OID（例: CPU 使用率、メモリ使用率）:**
  - 差分計算は不要。取得した値をそのまま使用する。
  - 値の範囲チェック: 0-100（%）の範囲外の値は `None` として記録。

#### 5.2. vSphere データ処理

##### 5.2.1. メトリクス値の正規化

- **CPU 使用率 (`cpu.usage.average`):**

  - vCenter から取得した%値をそのまま使用（0-100 の範囲）。

- **CPU Ready (`cpu.readiness.average`):**

  - vCenter から取得した%値をそのまま使用（0-100 の範囲）。

- **CPU Co-Stop (`cpu.costop.average`):**

  - vCenter から `cpu.costop.summation` (ms) を取得。
  - 計算式: `costop_percent = (costop_summation / 20000) * 100`
  - 20 秒（20000 ms）を基準として、Co-Stop 時間の割合を計算。
  - 例: `costop_summation = 1000 ms` の場合
    - `costop_percent = (1000 / 20000) * 100 = 5%`

- **メモリ使用率 (`mem.usage.average`):**

  - vCenter から取得した%値をそのまま使用（0-100 の範囲）。

- **ディスク/ネットワークスループット:**

  - vCenter から取得した KBps 値を MBps に変換して使用。
  - 計算式: `mbps = kbps / 1024`
  - 例: vCenter から `10240 KBps` を取得した場合、`10240 / 1024 = 10 MBps` として保存する。

- **ディスクレイテンシ:**
  - vCenter から取得した ms 値をそのまま使用。

##### 5.2.2. 異常値の処理

- **NULL 値:**

  - vCenter から NULL が返された場合、`None` として記録（データポイント欠損）。

- **範囲外の値:**

  - CPU/メモリ使用率が 0-100 の範囲外の場合、`None` として記録し、ログに警告を出力。

- **負の値:**
  - スループットやレイテンシが負の値の場合、`None` として記録。

---

### 6. UI・分析機能（バックエンドロジック）

- Streamlit UI からのトリガーに基づき、DuckDB でクエリを実行するロジックを実装する。

#### 6.1. ダッシュボード（通常閲覧モード）

UI の主要機能として、ユーザーが指定した条件（ホスト、メトリクス、期間、グラフ種別［折れ線, 散布図］）に基づき、DuckDB からデータを取得し、可視化するバックエンド機能。

分析機能とは別に、通常の時系列データを自由に閲覧・ドリルダウンできる機能がツールの基本となる。

##### 6.1.1. クエリ生成ロジック

- **UI カテゴリ連携:**

  - 「クラスタ単位」が選択された場合: `WHERE cluster IN (...)` でクエリ。
  - 「ESXi 単位」が選択された場合: `WHERE esxi_host IN (...)` でクエリ。
  - 「VM 単位」が選択された場合: `WHERE host IN (...)` でクエリ。

- **基本的なクエリ例:**

```sql
SELECT
    timestamp,
    value
FROM 'data_storage/**/*.parquet'
WHERE date >= '2025-11-15'
  AND date <= '2025-11-20'
  AND host IN ('esxi-01.local', 'esxi-02.local')
  AND metric_name = 'cpu.usage.average'
ORDER BY timestamp;
```

- **集計オプション:**
  - 時間単位集計: `GROUP BY DATE_TRUNC('hour', timestamp)` で時間単位に集約
  - 平均値: `AVG(value)`
  - 最大値: `MAX(value)`
  - 最小値: `MIN(value)`

#### 6.2. ベースライン分析

指定された期間（例: 過去 7 日）のデータを統計処理（曜日・時間帯別の中央値、95 パーセンタイル値等）し、そのベースラインから大きく逸脱している**異常値の期間を特定**するロジック。

過去に発生した性能スパイクや予期せぬ落ち込みが「いつ発生したか」を特定することで、障害の事後調査を支援する。

##### 6.2.1. ベースライン計算アルゴリズム

- **ステップ 1: 曜日・時間帯別の統計値を計算**

  - 過去 N 日（デフォルト: 7 日）のデータから、曜日（月-日）と時間帯（0-23 時）ごとに以下の統計値を計算:
    - 中央値（MEDIAN）
    - 95 パーセンタイル値（PERCENTILE_CONT(0.95)）
    - 5 パーセンタイル値（PERCENTILE_CONT(0.05)）

- **ステップ 2: 異常値の検出**

  - 各データポイントについて、対応する曜日・時間帯のベースラインと比較:
    - 異常値の条件: `value > 95パーセンタイル値 * 1.5` または `value < 5パーセンタイル値 * 0.5`
  - 連続する異常値の期間をグループ化し、期間として返す。

- **クエリ例:**

```sql
-- ベースライン計算（曜日・時間帯別）
WITH baseline AS (
  SELECT
    EXTRACT(DOW FROM timestamp) as day_of_week,
    EXTRACT(HOUR FROM timestamp) as hour,
    PERCENTILE_CONT(0.5) WITHIN GROUP (ORDER BY value) as median,
    PERCENTILE_CONT(0.95) WITHIN GROUP (ORDER BY value) as p95,
    PERCENTILE_CONT(0.05) WITHIN GROUP (ORDER BY value) as p05
  FROM 'data_storage/**/*.parquet'
  WHERE date >= CURRENT_DATE - INTERVAL '30 days'
    AND date < CURRENT_DATE
    AND host = 'esxi-01.local'
    AND metric_name = 'cpu.usage.average'
  GROUP BY day_of_week, hour
)
-- 異常値の検出
SELECT
    timestamp,
    value,
    baseline.p95,
    baseline.p05
FROM 'data_storage/**/*.parquet' data
JOIN baseline ON
    EXTRACT(DOW FROM data.timestamp) = baseline.day_of_week
    AND EXTRACT(HOUR FROM data.timestamp) = baseline.hour
WHERE date >= CURRENT_DATE - INTERVAL '7 days'
  AND host = 'esxi-01.local'
  AND metric_name = 'cpu.usage.average'
  AND (value > baseline.p95 * 1.5 OR value < baseline.p05 * 0.5)
ORDER BY timestamp;
```

#### 6.3. ボトルネック分析

対象ホスト（ESXi/VM）の主要リソース（CPU, メモリ, ディスク, NW）のメトリクスを一括取得し、定義済みルールに基づき、パフォーマンスを制限している要因を評価するロジック。

##### 6.3.1. 評価ロジック

各リソースに対し、以下の観点で評価する。

1.  **使用率 (Utilization):** リソースがどれだけビジーか

    - CPU: `cpu.usage.average > 80%` → 警告
    - メモリ: `mem.usage.average > 90%` → 警告
    - ディスク: `disk.read.average + disk.write.average > 80% of 最大スループット` → 警告
    - ネットワーク: `net.received.average + net.transmitted.average > 80% of 最大帯域` → 警告

2.  **飽和度 (Saturation):** リソースが処理しきれず待たされている度合い

    - CPU: `cpu.readiness.average > 5%` → 警告（CPU Ready が高い）
    - CPU: `cpu.costop.average > 5%` → 警告（Co-Stop が高い）
    - ディスク: `disk.readLatency.average > 20 ms` または `disk.writeLatency.average > 20 ms` → 警告

3.  **エラー (Errors):** 明示的なエラーの発生
    - ネットワーク: `net.error.rate.in > 0.1%` または `net.error.rate.out > 0.1%` → 警告（SNMP）
    - ネットワーク: `net.discard.rate.in > 0.1%` または `net.discard.rate.out > 0.1%` → 警告（SNMP）
    - ディスク: `disk.errors > 0` → 警告

##### 6.3.2. ボトルネック判定ルール

- **判定ロジック:**

  - 各リソースについて、上記の 3 つの観点で評価し、警告が 2 つ以上発生した場合、そのリソースを「ボトルネック」と判定する。
  - 複数のリソースがボトルネックと判定された場合、優先順位は以下の通り:
    1. CPU（最も重要）
    2. ディスク
    3. メモリ
    4. ネットワーク

- **クエリ例:**

```sql
-- CPU ボトルネック判定
SELECT
    timestamp,
    host,
    MAX(CASE WHEN metric_name = 'cpu.usage.average' THEN value END) as cpu_usage,
    MAX(CASE WHEN metric_name = 'cpu.readiness.average' THEN value END) as cpu_ready,
    MAX(CASE WHEN metric_name = 'cpu.costop.average' THEN value END) as cpu_costop,
    CASE
        WHEN MAX(CASE WHEN metric_name = 'cpu.usage.average' THEN value END) > 80 THEN 1 ELSE 0
    END +
    CASE
        WHEN MAX(CASE WHEN metric_name = 'cpu.readiness.average' THEN value END) > 5 THEN 1 ELSE 0
    END +
    CASE
        WHEN MAX(CASE WHEN metric_name = 'cpu.costop.average' THEN value END) > 5 THEN 1 ELSE 0
    END as bottleneck_score
FROM 'data_storage/**/*.parquet'
WHERE date >= CURRENT_DATE - INTERVAL '1 day'
  AND host IN ('esxi-01.local', 'esxi-02.local')
  AND metric_name IN ('cpu.usage.average', 'cpu.readiness.average', 'cpu.costop.average')
GROUP BY timestamp, host
HAVING bottleneck_score >= 2
ORDER BY timestamp DESC;
```

---

### 7. UI 連携機能（バックエンド）

#### 7.1. レポート機能

**専用のレポート作成画面**用のバックエンド機能を実装する。

- **CSV:** UI で設定された閾値に基づき DuckDB でデータをフィルタリングし、CSV を生成する。
- **PDF:** 分析サマリ表（統計表）およびグラフデータを PDF として生成する。
  ダッシュボードでの閲覧とは別に、オフラインでの報告や共有のために、固定フォーマットの出力機能が必要。

#### 7.2. サービス・タスクスケジューラ制御（管理者権限対応）

Streamlit の「設定」画面から、Windows サービスおよびタスクスケジューラの登録・削除・有効化/無効化を実行できる機能を提供する。管理者権限が必要な処理も、UAC（User Account Control）昇格により GUI から実行可能とする。

##### 7.2.1. Windows サービス制御（SNMP コレクター）

- **登録:** `pywin32` の `win32serviceutil.HandleCommandLine()` を使用して SNMP コレクターを Windows サービスとして登録する。
  - **実装方法:** `subprocess` モジュールを使用して、一時的なバッチファイルを作成し、`runas` コマンドで管理者権限で実行する。
  - **UAC 昇格:** ユーザーが UI で「サービス登録」ボタンをクリックすると、UAC ダイアログが表示され、管理者権限での実行を確認する。
- **削除:** `pywin32` の `win32serviceutil.RemoveService()` を使用してサービスを削除する。
- **起動/停止/再起動:** `win32serviceutil` の `StartService()`, `StopService()`, `RestartService()` を使用する。
- **状態確認:** サービスの実行状態（実行中/停止中）を取得し、UI に表示する。

##### 7.2.2. タスクスケジューラ制御（vSphere コレクター・メンテナンスジョブ）

- **登録:** `subprocess` モジュールを使用して `schtasks.exe /Create` コマンドを実行し、タスクを登録する。
  - **実装方法:** 一時的なバッチファイルを作成し、`runas` コマンドで管理者権限で実行する。
  - **UAC 昇格:** ユーザーが UI で「タスク登録」ボタンをクリックすると、UAC ダイアログが表示され、管理者権限での実行を確認する。
- **削除:** `schtasks.exe /Delete` コマンドを実行してタスクを削除する。
- **有効化/無効化:** `schtasks.exe /Change /Enable` または `/Disable` コマンドを実行する。
- **状態確認:** `schtasks.exe /Query` コマンドを実行してタスクの状態を取得し、UI に表示する。

##### 7.2.3. UAC 昇格の実装方法

- **実装方式:** Python の `subprocess` モジュールを使用して、一時的なバッチファイル（`.bat`）を作成し、`runas` コマンドまたは PowerShell の `Start-Process -Verb RunAs` を使用して管理者権限で実行する。
- **一時ファイル管理:** 実行後に一時バッチファイルを自動削除する。
- **エラーハンドリング:** UAC ダイアログでユーザーが「いいえ」を選択した場合や、管理者権限の取得に失敗した場合、エラーメッセージを UI に表示する。
  ユーザーが OS の GUI（サービス管理、タスクスケジューラ）を直接操作する手間を省き、ツール内で完結できるようにする。また、初回セットアップ時や設定変更時に、バッチファイルを手動で実行する必要をなくす。

#### 7.3. データエクスポート機能

指定した日付範囲のデータベースファイル（Parquet ファイル）を ZIP 形式でダウンロードする機能を提供する。別環境に持ち込んで閲覧するケースを想定している。

- **実装方法:**
  - UI（設定画面または専用のエクスポート画面）で、開始日と終了日を指定する。
  - 指定した日付範囲（`date=YYYY-MM-DD`）のパーティションディレクトリ内のすべての Parquet ファイルを収集する。
  - Python の `zipfile` モジュールを使用して、収集した Parquet ファイルを ZIP 形式で圧縮する。
  - ZIP ファイル名: `infra_data_YYYY-MM-DD_to_YYYY-MM-DD.zip`（例: `infra_data_2025-11-15_to_2025-11-20.zip`）
  - Streamlit の `st.download_button()` を使用して、ZIP ファイルをダウンロード可能にする。
- **ディレクトリ構造の保持:**
  - ZIP ファイル内では、元のパーティション構造（`date=YYYY-MM-DD/host=.../` など）を保持する。
  - これにより、別環境で ZIP を展開した後、DuckDB がパーティション・プルーニングを正しく実行できる。
- **別環境での閲覧方法:**
  - エクスポートした ZIP ファイルを別環境に持ち込み、任意のディレクトリに展開する。
  - 展開したディレクトリを `config.yml` の `storage.data_directory` に設定するか、展開先のパスを直接指定して DuckDB クエリを実行することで、データを閲覧・分析できる。
  - 閲覧モード（`config.yml` が存在しない状態）でも、展開したデータディレクトリを指定することで、エクスポートしたデータを参照できる。
- **ファイルサイズ制限:**
  - エクスポート対象のファイルサイズが大きい場合（例: 1 GB 超）、処理時間が長くなる可能性があることを UI に警告表示する。
  - 必要に応じて、日付範囲を分割してエクスポートするよう案内する。

#### 7.4. SNMP OID テスト機能

Streamlit の「設定」画面に、指定したホストと OID で「今すぐ SNMP GET」を実行し、生データを表示する機能を提供する。

ユーザーがカスタム OID の計算方法（Gauge/Counter）を判断する際、実際の取得値（例: `15` なのか `15342342` なのか）を確認できる必要がある。

---

### 8. 設定・管理

#### 8.1. 設定ファイル（競合対策）

UI（Streamlit）とコレクター（バックグラウンドプロセス）間の設定ファイル競合を避けるため、以下の 2 段階構成とする。

1.  `config.yml`: Streamlit UI が管理するマスター設定ファイル。
2.  `collector_config.json`: UI で設定保存時、コレクターが必要とする情報（接続先、OID リスト等）だけを抽出して生成する**読み取り専用ファイル**。
3.  各コレクタープロセスは、この `collector_config.json` のみを参照する。

ユーザーが UI で設定変更中（`config.yml`書き込み中）に、コレクターが中途半端な設定ファイルを読み込むことを防ぐため。

##### 8.1.1. config.yml スキーマ

```yaml
# データストレージ設定
storage:
  data_directory: "data_storage" # データ保存先ディレクトリ（相対パスまたは絶対パス）
  retention_policy:
    max_size_gb: 100 # ストレージ容量の上限（GB）

# SNMP 設定
snmp:
  enabled: true
  hosts:
    - hostname: "switch-01.local"
      community: "public" # 認証情報は Windows Credential Manager に保存
      version: 2 # 2 のみ（SNMPv2/v2c のみサポート）
      timeout: 3 # タイムアウト（秒）
      retries: 1
  custom_oids:
    - metric_name: "custom.cpu.usage"
      oid: "1.3.6.1.4.1.xxxxx.1.1.1.0"
      calculation_type: "gauge" # gauge または counter
      unit: "percent"

# vSphere 設定
vsphere:
  enabled: true
  vcenter:
    hostname: "vcenter.example.com"
    port: 443
    username: "admin" # 認証情報は Windows Credential Manager に保存
    ssl_verify: true
  inventory:
    clusters: [] # 監視対象クラスタ（空の場合は全クラスタ）
    esxi_hosts: [] # 監視対象 ESXi ホスト（空の場合は全ホスト）
    vms: [] # 監視対象 VM（空の場合は全 VM）
  metrics:
    esxi:
      cpu: true
      memory: true
      disk: true
      network: true
    vm:
      cpu: true
      memory: false # デフォルト無効
      disk: false # デフォルト無効
      network: false # デフォルト無効

# UI 設定
ui:
  port: 8501
  theme: "light" # light または dark
```

##### 8.1.2. collector_config.json スキーマ

```json
{
  "snmp": {
    "enabled": true,
    "hosts": [
      {
        "hostname": "switch-01.local",
        "community_key": "switch-01.local_public", // Windows Credential Manager の参照キー
        "version": 2,
        "timeout": 3,
        "retries": 1
      }
    ],
    "custom_oids": [
      {
        "metric_name": "custom.cpu.usage",
        "oid": "1.3.6.1.4.1.xxxxx.1.1.1.0",
        "calculation_type": "gauge",
        "unit": "percent"
      }
    ]
  },
  "vsphere": {
    "enabled": true,
    "vcenter": {
      "hostname": "vcenter.example.com",
      "port": 443,
      "username_key": "vcenter.example.com_admin", // Windows Credential Manager の参照キー
      "ssl_verify": true
    },
    "inventory": {
      "clusters": ["cluster-1"],
      "esxi_hosts": ["esxi-01.local"],
      "vms": ["vm-web-01"]
    },
    "metrics": {
      "esxi": {
        "cpu": true,
        "memory": true,
        "disk": true,
        "network": true
      },
      "vm": {
        "cpu": true,
        "memory": false,
        "disk": false,
        "network": false
      }
    }
  },
  "storage": {
    "data_directory": "data_storage"
  }
}
```

##### 8.1.3. 設定ファイルの生成・更新フロー

1. **UI での設定変更:**

   - ユーザーが UI（設定画面）で設定を変更
   - `config.yml` に保存（一時ファイル `config.yml.tmp` に書き込み後、リネーム）

2. **collector_config.json の生成:**

   - `config.yml` の保存完了後、`collector_config.json` を生成
   - 一時ファイル `collector_config.json.tmp` に書き込み後、リネーム（アトミック操作）

3. **コレクターの設定読み込み:**
   - コレクターは起動時および定期的に（30 秒ごと）`collector_config.json` をチェック
   - ファイルの更新時刻が前回読み込み時より新しい場合、設定を再読み込み

#### 8.2. 設定 UI（バックエンド要件）

Streamlit の「設定」画面用に、以下のバックエンドロジックを提供する。

##### 8.2.0. 初回起動時の処理

- **`config.yml` が存在しない場合:**
  - Streamlit アプリ起動時（`ui/app.py`）に `config.yml` の存在をチェックする。
  - 存在しない場合、初回セットアップ画面（`ui/pages/setup.py`）を自動的に表示する。
  - 初回セットアップ画面では、以下の情報を順次入力・設定する:
    1. **データストレージ設定:** データ保存先ディレクトリの指定（デフォルト: `data_storage`）
    2. **コレクター選択:** SNMP コレクターまたは vSphere コレクターの有効化を選択（両方有効化も可能）
    3. **SNMP 設定（SNMP を有効化した場合）:**
       - 監視対象ホストの追加（ホスト名、コミュニティ文字列）
       - コミュニティ文字列は Windows Credential Manager に保存
    4. **vSphere 設定（vSphere を有効化した場合）:**
       - vCenter 接続情報（ホスト名、ポート、ユーザー名、パスワード）
       - 認証情報は Windows Credential Manager に保存
  - 設定完了後、「設定を保存」ボタンをクリックすると、`config.yml` を生成し、通常の設定画面（`ui/pages/settings.py`）に遷移する。
  - `config.yml` が生成された後は、通常の設定画面から設定を変更できる。

##### 8.2.1. データ収集設定

- **SNMP カスタム OID 登録:** ユーザーが GUI（`st.data_editor`等）で入力した「メトリクス名」「OID」「計算方法（Gauge/Counter）」を `config.yml` に書き込む機能。
- **vSphere インベントリ更新:** vCenter に接続し、インベントリ階層（クラスタ、ホスト、VM）を取得・キャッシュする機能。
- **vSphere 対象選択:**
  - 更新したインベントリを基に、UI の分析カテゴリ（クラスタ/ESXi/VM）に応じた選択リストを生成するロジック。
  - 「VM 単位」分析では、「直近の所属 ESXi ホスト」をインベントリから参照し、VM 名の横に併記するデータを提供するロジック。
  - **VM メトリクス制御:** VM の Memory/Disk/Network メトリクスを GUI 上で**デフォルト無効**とし、個別に有効化できる機能を提供する。
  - ユーザーが選択した監視対象の ID を `config.yml` に保存する機能。
- **UI 警告表示:**
  - VM 単位のデータ取得設定画面（またはダッシュボード）において、「**VM の Memory, Disk, Network メトリクスは、vCenter への負荷およびデータ処理遅延の原因となるため、ボトルネックが疑われる VM に限り対象を絞って有効化してください。**」という警告を表示するためのフラグ/機能を提供する。

##### 8.2.2. サービス・タスクスケジューラ管理（GUI から実行）

Streamlit の「設定」画面から、Windows サービスおよびタスクスケジューラの登録・削除・有効化/無効化を実行できる機能を提供する。詳細は 7.2 節を参照。

- **SNMP コレクター（Windows サービス）:** 登録・削除・起動/停止/再起動・状態表示
- **vSphere コレクター（タスクスケジューラ）:** 登録・削除・有効化/無効化・状態表示
- **メンテナンスジョブ（タスクスケジューラ）:** 登録・削除・有効化/無効化・状態表示

##### 8.2.3. データエクスポート機能（GUI から実行）

Streamlit の「設定」画面または専用のエクスポート画面から、指定した日付範囲のデータベースファイルを ZIP 形式でダウンロードできる機能を提供する。詳細は 7.3 節を参照。

- **日付範囲指定:** 開始日と終了日をカレンダーウィジェットまたは日付入力フィールドで指定
- **エクスポート実行:** 「エクスポート」ボタンをクリックすると、指定した日付範囲の Parquet ファイルを ZIP 形式で圧縮し、ダウンロード可能にする
- **進捗表示:** エクスポート処理中は進捗バーを表示し、処理完了後にダウンロードボタンを表示

##### 8.2.4. リセット機能（GUI から実行）

Streamlit の「設定」画面から、リセット機能を実行できる。詳細は 8.6 節を参照。

- **リセット対象の選択:** チェックボックスで、削除する対象を選択可能（Windows サービス、タスクスケジューラ、認証情報）
- **実行:** 「リセット実行」ボタンで、選択した対象をリセット（管理者権限必要、UAC 昇格）
- **確認ダイアログ:** リセット実行前に確認ダイアログを表示
- **実行結果表示:** リセット処理の成功/失敗をサマリとして表示

#### 8.3. データメンテナンスジョブ（マージ・リテンション）

データ取得や分析処理とは別軸で実行される、**統合的なデータメンテナンスジョブ**を実装する。このジョブは、Parquet ファイルのマージ処理とリテンションポリシーの実行を一括で行う。

##### 8.3.1. 実行タイミング

- **実行頻度:** Windows タスクスケジューラにより **1 時間ごと** にバッチ実行される。
- **トリガー検知:**
  - コレクター（SNMP/vSphere）が新しい日付のパーティションディレクトリ（`date=YYYY-MM-DD`）に初めてデータを書き込む際、トリガーファイル（`logs/maintenance_trigger_YYYY-MM-DD.flag`）を作成する。
  - メンテナンスジョブは実行時に、未処理のトリガーファイル（前日の日付）が存在するかチェックする。
  - トリガーファイルが存在する場合、前日の Parquet ファイルをマージし、リテンションポリシーを実行する。
  - 処理完了後、トリガーファイルを削除する。
- **実行順序:**
  1. 前日の Parquet ファイルをマージ（8.3.2 節参照）
  2. リテンションポリシーの実行（8.3.3 節参照）

##### 8.3.2. Parquet ファイルのマージ処理

- **目的:** 1 日あたり 48 個（30 分間隔 × 48 回）作成される Parquet ファイルを、1 日 1 ファイルに統合することで、クエリ性能の向上とファイル管理の簡素化を図る。
- **対象:** 前日（`date=YYYY-MM-DD`）のすべてのパーティションディレクトリ内の Parquet ファイル。
- **マージ方法:**
  - DuckDB の `COPY INTO` を使用して、同一パーティション内のすべての Parquet ファイルを効率的に結合する。
  - マージ後のファイル名: `{コレクター種別}_metrics_{日付}.parquet`（例: `snmp_metrics_2025-11-15.parquet`）
  - マージ元のファイル（`snmp_metrics_HHMM.parquet`）は、マージ完了後に削除する。
- **実装方法:**
  - 各パーティションディレクトリ（`date=YYYY-MM-DD/host=.../` など）ごとに、そのディレクトリ内のすべての Parquet ファイルをスキャンする。
  - DuckDB の `COPY INTO '新しいファイル名.parquet' FROM 'パーティションディレクトリ/*.parquet' (FORMAT PARQUET)` を使用して、効率的にマージする。
  - 書き込み完了後、元のファイル（`snmp_metrics_HHMM.parquet`）を削除する。
- **エラーハンドリング:**
  - マージ処理中にエラーが発生した場合、元のファイルは削除せず、ログに記録する。
  - マージ処理は、データ取得や分析処理とは別軸で実行されるため、処理時間が長くても問題ない（高速処理は求めない）。
- **設計理由:** 1 日 48 個のファイルを 1 ファイルに統合することで、DuckDB のクエリ実行時に開くファイル数を削減し、分析性能を向上させる。また、ファイル数の削減により、ファイルシステムの管理オーバーヘッドも軽減される。

##### 8.3.3. リテンションポリシー

`config.yml` で指定された**ストレージ容量の上限**（GB）に基づき、超過分を古い日付（`date=...`）のパーティションディレクトリから削除する。

- **実装方法:** 現在のストレージ使用量を計算し、上限を超過している場合は、最も古い日付のパーティションディレクトリから順に削除する。削除後も上限を超過している場合は、次に古い日付のディレクトリを削除する処理を繰り返し、上限を下回るまで継続する。
  ディスク容量の枯渇を防ぐため。日付フォルダ作成時に実行することで、データ収集のタイミングと同期し、定期的なメンテナンスを自動化する。

##### 8.3.4. メンテナンスジョブの実装形態

- **実行形態:** コレクタープロセスとは**完全に分離**した、独立した Python プロセス/ジョブとして実装する。
  データ収集処理の安定性を確保するため。メンテナンス処理が長時間かかっても、コレクターのデータ収集に影響を与えない。
- **実行方法:** Windows タスクスケジューラにより **1 時間ごと** にバッチ実行される。
  - **タスク名:** `InfraAnalysisTool_MaintenanceJob`
  - **実行アカウント:** **SYSTEM アカウント**（ログオフ後も継続実行を確保するため）
  - **実行スクリプト:** `maintenance/maintenance_job.py`
- **多重起動防止:** 起動時にロックファイル（`logs/maintenance_job.lock`）を確認し、既に存在する場合は処理をスキップする。処理完了時にロックファイルを削除する。
- **ログ出力:** メンテナンスジョブの実行開始・完了、マージ処理の進捗、リテンション処理の削除対象をログに記録する。
  - **ログファイル:** `logs/maintenance_job.log`

#### 8.4. 認証情報管理（セキュリティ）

SNMP 認証情報（コミュニティ文字列）および vCenter 認証情報（ユーザー名・パスワード）は、**Windows Credential Manager** を使用して安全に管理する。

- **ライブラリ:** `keyring` (v24.0.0 以上)
  `keyring` ライブラリは、Windows では Windows Credential Manager と自動的に連携し、認証情報を OS ネイティブのセキュアストレージに保存する。これにより、平文での設定ファイル保存を避け、セキュリティを向上させる。
- **実装方法:**
  - **認証情報の保存:** UI（設定画面）でユーザーが認証情報を入力した際、`keyring.set_password(service_id, username, password)` を使用して Windows Credential Manager に保存する。
  - **認証情報の取得:** コレクタープロセス（SYSTEM アカウントで実行）は、`keyring.get_password(service_id, username)` を使用して認証情報を取得する。
  - **SYSTEM アカウントからのアクセス:** Windows Credential Manager に保存された認証情報は、SYSTEM アカウントからもアクセス可能である。これにより、vSphere コレクターやメンテナンスジョブが SYSTEM アカウントで実行されても、認証情報を取得できる。
  - **サービス ID の命名規則:** `service_id` は、`"InfraAnalysisTool_SNMP"` や `"InfraAnalysisTool_vCenter"` のように、ツール名と用途を組み合わせた識別子を使用する。
  - **設定ファイルとの連携:** `config.yml` には認証情報自体は保存せず、認証情報の参照キー（ユーザー名やホスト名）のみを保存する。実際の認証情報は Windows Credential Manager から動的に取得する。
    認証情報を平文で設定ファイルに保存すると、ファイルアクセス権限の設定ミスやファイルの誤送信により情報漏洩のリスクが生じる。Windows Credential Manager を使用することで、OS レベルのセキュリティ機能を活用し、認証情報の保護を強化する。

#### 8.5. ログ管理

##### 8.5.1. ログファイルの配置

- **ログディレクトリ:** `logs/`（ツールのルートディレクトリ配下）
- **ログファイル一覧:**
  - `snmp_collector.log`: SNMP コレクターのログ
  - `vsphere_collector.log`: vSphere コレクターのログ
  - `maintenance_job.log`: メンテナンスジョブのログ
  - `ui.log`: Streamlit UI のログ

##### 8.5.2. ログレベル

- **DEBUG:** 詳細なデバッグ情報（開発時のみ使用）
- **INFO:** 通常の動作情報（収集開始、書き込み完了等）
- **WARNING:** 警告（タイムアウト、リトライ、設定読み込み失敗等）
- **ERROR:** エラー（致命的なエラー、例外発生等）

##### 8.5.3. ログフォーマット

- **フォーマット:** `[YYYY-MM-DD HH:MM:SS] [LEVEL] [機能名] メッセージ`
- **例:**
  - `[2025-11-15 17:00:00] [INFO] [SNMP Collector] 収集開始: 100 ホスト`
  - `[2025-11-15 17:00:05] [WARNING] [SNMP Collector] タイムアウト: switch-01.local`
  - `[2025-11-15 17:30:00] [INFO] [SNMP Collector] 書き込み完了: snmp_metrics_1700.parquet`

##### 8.5.4. ログローテーション

- **ローテーション条件:** ファイルサイズが **10 MB** に達したらローテーション
- **ローテーション方法:**
  - 現在のログファイルを `{ログファイル名}.1` にリネーム
  - 既存の `{ログファイル名}.1` は `{ログファイル名}.2` にリネーム（以下同様）
- **保持ファイル数:** 最大 **5 ファイル**（`{ログファイル名}`, `.1`, `.2`, `.3`, `.4`）
- **実装:** Python の `logging.handlers.RotatingFileHandler` を使用

##### 8.5.5. ログの用途

- **トラブルシューティング:** エラー発生時の原因調査
- **パフォーマンス監視:** 収集遅延、書き込み遅延の検知
- **運用監視:** コレクターの稼働状況の確認

#### 8.6. リセット機能（OS 設定の復元）

本ツールで変更した OS の設定を元に戻すリセット機能を実装する。

ツールのアンインストール時や、設定を完全にクリアしたい場合に、OS に残った設定を手動で削除する手間を省き、確実にクリーンアップする。

##### 8.6.1. リセット対象

以下の OS 設定をリセットする。

1.  **Windows サービス（SNMP コレクター）:**

    - サービス名: `InfraAnalysisTool_SNMPCollector`
    - 実装方法: `pywin32` の `win32serviceutil.RemoveService()` を使用してサービスを削除
    - 実行条件: サービスが停止している必要がある（実行中の場合は先に停止）

2.  **Windows タスクスケジューラ（vSphere コレクター）:**

    - タスク名: `InfraAnalysisTool_vSphereCollector`
    - 実装方法: `subprocess` モジュールを使用して `schtasks.exe /Delete /TN "InfraAnalysisTool_vSphereCollector" /F` を実行
    - `/F` オプションで確認なしで削除

3.  **Windows タスクスケジューラ（メンテナンスジョブ）:**

    - タスク名: `InfraAnalysisTool_MaintenanceJob`
    - 実装方法: `subprocess` モジュールを使用して `schtasks.exe /Delete /TN "InfraAnalysisTool_MaintenanceJob" /F` を実行
    - `/F` オプションで確認なしで削除

4.  **Windows Credential Manager（認証情報）:**

    - サービス ID: `InfraAnalysisTool_SNMP`, `InfraAnalysisTool_vCenter`
    - 実装方法: `keyring.delete_password(service_id, username)` を使用して認証情報を削除
    - `config.yml` に保存されている認証情報の参照キーを読み取り、対応する認証情報をすべて削除

##### 8.6.2. 実装方法

- **UI での実行:** 詳細は 8.2.3 節を参照。
- **リセット処理の順序:**
  1. Windows サービスを停止・削除（管理者権限必要、UAC 昇格）
  2. Windows タスクスケジューラのタスクを削除（vSphere コレクター、メンテナンスジョブ）（管理者権限必要、UAC 昇格）
  3. Windows Credential Manager から認証情報を削除（管理者権限不要）
- **エラーハンドリング:** 各リセット処理でエラーが発生した場合、ログに記録し、処理を継続する。UAC ダイアログでユーザーが「いいえ」を選択した場合や、管理者権限の取得に失敗した場合、エラーメッセージを UI に表示する。

##### 8.6.3. データストレージの扱い

- **データストレージ（`data_storage/`）は削除しない:**
  - データストレージはユーザーが指定した場所に保存されており、ツールの設定とは独立している
  - データの削除は、リテンションポリシーまたはユーザーが手動で行う

---

### 9. テスト戦略

#### 9.1. 単体テスト

各モジュールや関数が期待通りに動作することを確認する。

- **テストフレームワーク:** `pytest` を採用する。
  Python の標準的なテストフレームワークであり、豊富な機能（フィクスチャ、パラメータ化テスト、カバレッジ測定）を提供する。
- **テスト対象:**
  - **データ処理・変換ロジック:** SNMP カウンターの差分計算、ロールオーバー検知、vSphere メトリクスの単位変換（`costop` の%換算等）
  - **設定ファイル管理:** `config.yml` の読み書き、`collector_config.json` の生成ロジック
  - **認証情報管理:** `keyring` を使用した認証情報の保存・取得
  - **リテンションポリシー:** ストレージ容量計算、古いパーティションディレクトリの削除ロジック
- **テスト環境:** モックオブジェクトを使用し、外部依存（SNMP 機器、vCenter、ファイルシステム）を排除した環境で実行する。

#### 9.2. 統合テスト

複数のモジュールやコンポーネントが連携して正しく動作することを確認する。

- **テスト対象:**
  - **コレクター - ストレージ連携:** SNMP/vSphere コレクターが収集したデータが、正しいパーティション構造で Parquet ファイルとして保存されることを確認
  - **UI - DuckDB 連携:** Streamlit UI からのクエリが DuckDB で正しく実行され、期待通りのデータが返されることを確認
  - **設定 UI - コレクター連携:** UI で設定変更した内容が `collector_config.json` に正しく反映され、コレクターが新しい設定を読み込むことを確認
  - **Windows サービス連携:** SNMP コレクターが Windows サービスとして正しく登録・起動・停止されることを確認
- **テスト環境:** 実際の SNMP 機器や vCenter への接続は不要。テスト用のモックサーバー（SNMP シミュレータ、vSphere API モック）を使用する。

#### 9.3. パフォーマンステスト

システムが想定される負荷下で適切に動作することを確認する。

- **テスト対象:**
  - **データ収集性能:** 100 ホスト超への SNMP 収集が 20 秒以内に完了することを確認
  - **データ書き込み性能:** 30 分間のバッファリングデータ（想定最大データ量）を Parquet ファイルとして書き込む処理が、次回の書き込み周期（30 分）までに完了することを確認
  - **クエリ性能:** DuckDB による分析クエリ（期間指定、複数ホスト集計、ベースライン分析等）の応答時間を測定し、実用的な範囲（例: 10 秒以内）に収まることを確認
  - **メモリ使用量:** 30 分間のバッファリング時のメモリ使用量を測定し、想定環境でのメモリ制約内に収まることを確認
- **テストデータ:** 実際の運用環境に近い規模のテストデータ（ホスト数、メトリクス数、データ保持期間）を生成し、パフォーマンステストに使用する。
- **測定ツール:** Python の `cProfile` や `memory_profiler` を使用して、ボトルネックやメモリリークを特定する。
