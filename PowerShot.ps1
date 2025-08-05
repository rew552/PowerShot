<#
.SYNOPSIS
    クリップボードを監視し、高機能なUIでスクリーンショットを効率的に整理・保存する常駐型ツール。

.DESCRIPTION
    このスクリプトは、クリップボードにコピーされた画像を検知し、保存用のウィンドウを表示します。
    ユーザーはプレビューを確認しながら、フォルダ名やファイル名を柔軟に設定して画像を保存できます。
    ファイル履歴の検索や連番の自動インクリメントなど、スクリーンショットの管理を効率化するための
    多数の機能を備えています。コアロジックはC#で記述されており、PowerShell上で安定して動作します。

.NOTES
    Version: 1.0

    Key Features:
    - クリップボード画像の自動検知
    - 画像プレビューとリサイズ可能なUI
    - サブフォルダへの保存機能
    - ファイル名・フォルダ名の履歴検索
    - 連番の自動インクリメントとリセット
    - 保存前ファイル名の重複を警告表示
#>

# --- C# Core Logic with Windows Forms UI ---
$csharpSource = @"
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

namespace PowerShot.WinForms
{
    // --- Data Structures ---

    /// <summary>
    /// アプリケーションの設定値を保持するクラス。
    /// </summary>
    public class AppState
    {
        public string SaveDirectory { get; set; }
        public string FileNamePrefix { get; set; }
        public string DefaultFormat { get; set; }
        public long JpgQuality { get; set; }
    }

    // --- Win32 Interop ---

    internal static class User32
    {
        [DllImport("user32.dll", SetLastError = true)] public static extern bool AddClipboardFormatListener(IntPtr hwnd);
        [DllImport("user32.dll", SetLastError = true)] public static extern bool RemoveClipboardFormatListener(IntPtr hwnd);
        [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
        public const int WM_CLIPBOARDUPDATE = 0x031D;
    }

    // --- UI Components & Helpers ---

    internal class ClipboardMonitor : NativeWindow, IDisposable
    {
        public event EventHandler ClipboardUpdate;
        public ClipboardMonitor() { CreateHandle(new CreateParams()); User32.AddClipboardFormatListener(this.Handle); }
        protected override void WndProc(ref Message m) { if (m.Msg == User32.WM_CLIPBOARDUPDATE) { if (ClipboardUpdate != null) { ClipboardUpdate.Invoke(this, EventArgs.Empty); } } base.WndProc(ref m); }
        public void Dispose() { if (this.Handle != IntPtr.Zero) { User32.RemoveClipboardFormatListener(this.Handle); this.DestroyHandle(); } }
    }

    internal class SaveForm : Form
    {
        // Fields
        private AppState _state;
        private int _sequenceNumber;
        private string _lastUsedPrefix;
        private string _lastUsedOptionalName;
        private string _lastUsedDirectory;
        private PictureBox _previewBox;
        private TextBox _textDirectory, _textFileName, _textOptionalName, _textSequence;
        private ComboBox _comboFormat;
        private ListBox _resultListBox;
        private Label _previewFileNameLabel;
        private Button _btnSave, _btnResetSeq;

        // Constructor
        public SaveForm(Bitmap sourceBitmap, AppState initialState, int currentSeq, string sessionPrefix, string sessionOptional, string sessionDirectory)
        {
            _state = initialState; // Use the state passed from CoreController
            _sequenceNumber = currentSeq;
            _lastUsedPrefix = sessionPrefix;
            _lastUsedOptionalName = sessionOptional;
            _lastUsedDirectory = sessionDirectory;

            InitializeComponent(sourceBitmap);
        }

        // Public properties to return state to CoreController
        public int GetNextSequenceNumber() { return _sequenceNumber; }
        public string GetLastUsedPrefix() { return _lastUsedPrefix; }
        public string GetLastUsedOptionalName() { return _lastUsedOptionalName; }
        public string GetLastUsedDirectory() { return _lastUsedDirectory; }

        private void InitializeComponent(Bitmap sourceBitmap)
        {
            // Form Style & Basic Properties
            this.Text = ">_ PowerShot";
            this.AutoScaleMode = AutoScaleMode.Dpi;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.MaximizeBox = true;
            this.MinimizeBox = true;
            this.TopMost = true;
            this.KeyPreview = true;
            var darkBg = Color.FromArgb(37, 37, 38);
            this.BackColor = darkBg;
            this.ForeColor = Color.FromArgb(241, 241, 241);
            this.Font = new Font("Yu Gothic UI", 10F);
            this.Padding = new Padding(10);
            var screen = Screen.FromControl(this);
            this.Size = new Size((int)(screen.WorkingArea.Width * 0.8), (int)(screen.WorkingArea.Height * 0.8));
            this.MinimumSize = new Size(800, 600);

            // Main Layout Panels
            var mainTable = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
            mainTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            mainTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 450F));
            this.Controls.Add(mainTable);

            _previewBox = new PictureBox { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom, BorderStyle = BorderStyle.FixedSingle, Image = sourceBitmap, Margin = new Padding(0, 0, 10, 0) };
            mainTable.Controls.Add(_previewBox, 0, 0);

            var rightPanel = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3 };
            rightPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            rightPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            rightPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 60F));
            mainTable.Controls.Add(rightPanel, 1, 0);

            // Controls for Search Group
            var searchGroup = new GroupBox { Text = "履歴検索", Dock = DockStyle.Fill, Padding = new Padding(10) };
            var searchLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5 };
            searchLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 45F));
            searchLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 45F));
            searchLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 45F));
            searchLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            searchLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            var btnSearch = new Button { Text = "設定したファイル名で検索", Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 5) };
            var btnListAll = new Button { Text = "ファイル名一覧(連番・拡張子除く)", Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 5) };
            var btnListDirectories = new Button { Text = "フォルダ一覧", Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 5) };
            searchLayout.Controls.Add(btnSearch, 0, 0);
            searchLayout.Controls.Add(btnListAll, 0, 1);
            searchLayout.Controls.Add(btnListDirectories, 0, 2);
            var listLabel = new Label { Text = "履歴 (ダブルクリックで入力):", AutoSize = true, Padding = new Padding(0, 10, 0, 5) };
            searchLayout.Controls.Add(listLabel, 0, 3);
            _resultListBox = new ListBox { Dock = DockStyle.Fill, DrawMode = DrawMode.OwnerDrawVariable, Tag = "Files" };
            searchLayout.Controls.Add(_resultListBox, 0, 4);
            searchGroup.Controls.Add(searchLayout);
            rightPanel.Controls.Add(searchGroup, 0, 0);

            // Controls for Settings Group
            var settingsGroup = new GroupBox { Text = "ファイル設定", Dock = DockStyle.Fill, Padding = new Padding(10) };
            var settingsTable = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 11 };
            for (int i = 0; i < 11; i++) { settingsTable.RowStyles.Add(new RowStyle(SizeType.AutoSize)); }
            settingsTable.RowStyles[8] = new RowStyle(SizeType.Percent, 100F);
            settingsTable.RowStyles[10] = new RowStyle(SizeType.Absolute, 45F);
            settingsTable.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            settingsTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            settingsTable.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            settingsTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));

            var labelDirectory = new Label { Text = "フォルダ名 (オプション):", Anchor = AnchorStyles.Left | AnchorStyles.Bottom, AutoSize = true };
            _textDirectory = new TextBox { Text = _lastUsedDirectory, Dock = DockStyle.Fill, Margin = new Padding(0, 3, 0, 8) };
            settingsTable.Controls.Add(labelDirectory, 0, 0); settingsTable.SetColumnSpan(labelDirectory, 4);
            settingsTable.Controls.Add(_textDirectory, 0, 1); settingsTable.SetColumnSpan(_textDirectory, 4);

            var labelFileName = new Label { Text = "ファイル名1 (接頭辞):", Anchor = AnchorStyles.Left | AnchorStyles.Bottom, AutoSize = true };
            _textFileName = new TextBox { Text = _lastUsedPrefix, Dock = DockStyle.Fill, Margin = new Padding(0, 3, 0, 8) };
            settingsTable.Controls.Add(labelFileName, 0, 2); settingsTable.SetColumnSpan(labelFileName, 4);
            settingsTable.Controls.Add(_textFileName, 0, 3); settingsTable.SetColumnSpan(_textFileName, 4);

            var labelOptionalName = new Label { Text = "ファイル名2 (オプション):", Anchor = AnchorStyles.Left | AnchorStyles.Bottom, AutoSize = true };
            _textOptionalName = new TextBox { Text = _lastUsedOptionalName, Dock = DockStyle.Fill, Margin = new Padding(0, 3, 0, 8) };
            settingsTable.Controls.Add(labelOptionalName, 0, 4); settingsTable.SetColumnSpan(labelOptionalName, 4);
            settingsTable.Controls.Add(_textOptionalName, 0, 5); settingsTable.SetColumnSpan(_textOptionalName, 4);

            var labelSequence = new Label { Text = "連番:", Anchor = AnchorStyles.Left, AutoSize = true, TextAlign = ContentAlignment.MiddleLeft };
            _textSequence = new TextBox { Text = _sequenceNumber.ToString("D3"), Dock = DockStyle.Fill };
            var labelFormat = new Label { Text = "形式:", Anchor = AnchorStyles.Left, AutoSize = true, TextAlign = ContentAlignment.MiddleLeft, Margin = new Padding(5, 0, 0, 0) };
            _comboFormat = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
            _comboFormat.Items.AddRange(new object[] { "PNG", "JPG" });
            _comboFormat.SelectedIndex = (_state.DefaultFormat == "JPG") ? 1 : 0;
            settingsTable.Controls.Add(labelSequence, 0, 6);
            settingsTable.Controls.Add(_textSequence, 1, 6);
            settingsTable.Controls.Add(labelFormat, 2, 6);
            settingsTable.Controls.Add(_comboFormat, 3, 6);

            _btnResetSeq = new Button { Text = "連番リセット", Dock = DockStyle.Fill, Margin = new Padding(0, 8, 0, 0), Height = 40 };
            settingsTable.Controls.Add(_btnResetSeq, 0, 7); settingsTable.SetColumnSpan(_btnResetSeq, 4);

            settingsTable.Controls.Add(new Panel(), 0, 8); // Spacer
            settingsTable.SetColumnSpan(settingsTable.GetControlFromPosition(0,8), 4);

            var previewLabel = new Label { Text = "ファイル名プレビュー:", Anchor = AnchorStyles.Left | AnchorStyles.Bottom, AutoSize = true };
            _previewFileNameLabel = new Label { Text = "", ForeColor = Color.Gray, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, AutoSize = false };
            var previewPanel = new Panel { Dock = DockStyle.Fill, BorderStyle = BorderStyle.FixedSingle, Padding = new Padding(5), Margin = new Padding(0, 3, 0, 0)};
            previewPanel.Controls.Add(_previewFileNameLabel);
            settingsTable.Controls.Add(previewLabel, 0, 9); settingsTable.SetColumnSpan(previewLabel, 4);
            settingsTable.Controls.Add(previewPanel, 0, 10); settingsTable.SetColumnSpan(previewPanel, 4);

            settingsGroup.Controls.Add(settingsTable);
            rightPanel.Controls.Add(settingsGroup, 0, 1);

            // Controls for Button Panel
            var buttonPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(0, 8, 0, 8) };
            _btnSave = new Button { Text = "保存", Size = new Size(135, 42) };
            var btnCancel = new Button { Text = "キャンセル", Size = new Size(135, 42) };
            buttonPanel.Controls.Add(btnCancel);
            buttonPanel.Controls.Add(_btnSave);
            rightPanel.Controls.Add(buttonPanel, 0, 2);

            // Control Styles
            var controlBg = Color.FromArgb(60, 60, 60);
            var lightText = Color.FromArgb(241, 241, 241);
            this.Font = new Font("Yu Gothic UI", 11F);
            searchGroup.ForeColor = lightText;
            settingsGroup.ForeColor = lightText;
            _resultListBox.BackColor = controlBg; _resultListBox.ForeColor = lightText; _resultListBox.BorderStyle = BorderStyle.FixedSingle;
            previewPanel.BackColor = controlBg;
            _previewFileNameLabel.BackColor = controlBg;
            foreach (var label in new Label[] { labelDirectory, labelFileName, labelOptionalName, labelSequence, labelFormat, listLabel, previewLabel }) { label.BackColor = Color.Transparent; label.ForeColor = lightText; }
            foreach (var control in new Control[] { _textDirectory, _textFileName, _textOptionalName, _textSequence, _comboFormat }) { control.BackColor = controlBg; control.ForeColor = lightText; if (control is TextBoxBase) { ((TextBoxBase)control).BorderStyle = BorderStyle.FixedSingle; } if (control is ComboBox) { ((ComboBox)control).FlatStyle = FlatStyle.Flat; } }
            foreach (var button in new Button[] { _btnSave, btnCancel, _btnResetSeq, btnSearch, btnListAll, btnListDirectories }) { button.FlatStyle = FlatStyle.Flat; button.FlatAppearance.BorderColor = Color.FromArgb(80, 80, 80); button.BackColor = controlBg; button.ForeColor = lightText; button.Font = new Font(this.Font.FontFamily, 10F, FontStyle.Bold); }
            _btnSave.BackColor = Color.FromArgb(0, 122, 204); _btnSave.ForeColor = Color.White; _btnSave.FlatAppearance.MouseOverBackColor = Color.FromArgb(28, 151, 234);

            // Event Handlers
            this.AcceptButton = _btnSave; this.CancelButton = btnCancel;
            this.Shown += (s, e) => { this.ActiveControl = _btnSave; };
            _btnSave.Click += (s, e) => OnSaveClick();
            btnCancel.Click += (s, e) => this.DialogResult = DialogResult.Cancel;
            _btnResetSeq.Click += (s, e) => { _sequenceNumber = 1; _textSequence.Text = "001"; UpdateFileNamePreview(); };
            btnSearch.Click += (s, e) => SearchFiles(true);
            btnListAll.Click += (s, e) => SearchFiles(false);
            btnListDirectories.Click += (s, e) => ListDirectories();
            _resultListBox.DoubleClick += (s, e) => { if (_resultListBox.SelectedItem != null) { PopulateFromListItem(_resultListBox.SelectedItem.ToString()); } };
            _resultListBox.MeasureItem += ListBox_MeasureItem;
            _resultListBox.DrawItem += ListBox_DrawItem;
            var textControls = new Control[] { _textDirectory, _textFileName, _textOptionalName, _textSequence };
            foreach(var c in textControls) { c.TextChanged += (s, e) => UpdateFileNamePreview(); }
            _comboFormat.SelectedIndexChanged += (s, e) => UpdateFileNamePreview();
            this.KeyDown += (s, ev) => {
                if (ev.KeyCode == Keys.Enter && !(this.ActiveControl is TextBoxBase)) {
                    _btnSave.PerformClick();
                    ev.SuppressKeyPress = true;
                }
            };

            UpdateFileNamePreview();
        }

        // --- Event Handler Methods ---

        private void ListBox_MeasureItem(object sender, MeasureItemEventArgs e)
        {
            if (e.Index < 0) return;
            string text = _resultListBox.Items[e.Index].ToString();
            e.ItemHeight = (int)e.Graphics.MeasureString(text, _resultListBox.Font).Height + 6;
        }

        private void ListBox_DrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0) return;
            e.DrawBackground();
            string text = _resultListBox.Items[e.Index].ToString();
            string previewText = _previewFileNameLabel.Text;
            Brush brush = (_resultListBox.Tag.ToString() == "Files" && text == previewText) ? Brushes.Red : Brushes.White;
            e.Graphics.DrawString(text, e.Font, brush, e.Bounds, StringFormat.GenericDefault);
            e.DrawFocusRectangle();
        }

        private void UpdateFileNamePreview()
        {
            string format = _comboFormat.SelectedIndex == 0 ? "png" : "jpg";
            string baseName = _textFileName.Text;
            if (!string.IsNullOrWhiteSpace(_textOptionalName.Text))
            {
                baseName += "_" + _textOptionalName.Text;
            }
            string finalName = string.IsNullOrWhiteSpace(baseName) ? _state.FileNamePrefix + "[タイムスタンプ]" : baseName + "_" + _textSequence.Text;
            _previewFileNameLabel.Text = finalName + "." + format;
            _resultListBox.Invalidate();
        }

        private void ListDirectories()
        {
            try
            {
                _resultListBox.Tag = "Folders";
                var directories = Directory.GetDirectories(_state.SaveDirectory).Select(d => Path.GetFileName(d));
                _resultListBox.Items.Clear();
                _resultListBox.Items.AddRange(directories.OrderBy(n => n).ToArray());
            }
            catch (Exception ex) { MessageBox.Show("フォルダの検索中にエラー: " + ex.Message); }
        }

        private void SearchFiles(bool byPrefix)
        {
            try
            {
                _resultListBox.Tag = "Files";
                string targetDir = Path.Combine(_state.SaveDirectory, _textDirectory.Text);
                if (!Directory.Exists(targetDir))
                {
                    _resultListBox.Items.Clear();
                    return;
                }

                var files = Directory.GetFiles(targetDir);
                IEnumerable<string> names;

                if (byPrefix)
                {
                    if (string.IsNullOrWhiteSpace(_textFileName.Text))
                    {
                        _resultListBox.Items.Clear();
                        return;
                    }
                    names = files.Where(f => Path.GetFileName(f).StartsWith(_textFileName.Text, StringComparison.OrdinalIgnoreCase))
                                 .Select(f => Path.GetFileName(f));
                }
                else
                {
                    var regex = new Regex(@"_(\d{3,})$");
                    names = files.Select(f => regex.Replace(Path.GetFileNameWithoutExtension(f), "")).Distinct();
                }
                _resultListBox.Items.Clear();
                _resultListBox.Items.AddRange(names.OrderBy(n => n).ToArray());
            }
            catch (Exception ex) { MessageBox.Show("ファイルの検索中にエラー: " + ex.Message); }
        }

        private void PopulateFromListItem(string item)
        {
            if (_resultListBox.Tag.ToString() == "Folders")
            {
                _textDirectory.Text = item;
            }
            else
            {
                string baseName = Path.GetFileNameWithoutExtension(item);
                var regex = new Regex(@"_(\d{3,})$");
                baseName = regex.Replace(baseName, "");
                var parts = baseName.Split(new[] { '_' }, 2);
                _textFileName.Text = parts.Length > 0 ? parts[0] : "";
                _textOptionalName.Text = parts.Length > 1 ? parts[1] : "";
            }
        }

        private void OnSaveClick()
        {
            if (_textDirectory.Text.IndexOfAny(Path.GetInvalidPathChars()) >= 0 || _textFileName.Text.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || _textOptionalName.Text.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) { MessageBox.Show("ファイル名またはフォルダ名に使用できない文字が含まれています。", "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error); return; }
            int seqNum;
            if (!int.TryParse(_textSequence.Text, out seqNum)) { MessageBox.Show("連番には数値を入力してください。", "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error); return; }

            string finalDir = Path.Combine(_state.SaveDirectory, _textDirectory.Text);
            if (!Directory.Exists(finalDir))
            {
                Directory.CreateDirectory(finalDir);
            }

            string format = _comboFormat.SelectedIndex == 0 ? "png" : "jpg";
            string baseName = _textFileName.Text;
            if (!string.IsNullOrWhiteSpace(_textOptionalName.Text))
            {
                baseName += "_" + _textOptionalName.Text;
            }

            bool useTimestamp = string.IsNullOrWhiteSpace(baseName);
            string finalName = useTimestamp
                ? _state.FileNamePrefix + DateTime.Now.ToString("yyyyMMdd-HHmmss")
                : baseName + "_" + _textSequence.Text;
            string filePath = Path.Combine(finalDir, finalName + "." + format);

            if (File.Exists(filePath)) { MessageBox.Show("同じ名前のファイルが既に存在します。", "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error); return; }

            try {
                if (format == "jpg")
                {
                    var jpgEncoder = GetEncoder(ImageFormat.Jpeg);
                    var myEncoderParameters = new EncoderParameters(1);
                    myEncoderParameters.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, _state.JpgQuality);
                    _previewBox.Image.Save(filePath, jpgEncoder, myEncoderParameters);
                }
                else
                {
                    _previewBox.Image.Save(filePath, ImageFormat.Png);
                }

                Console.WriteLine(string.Format("Saved: {0}", filePath));
                _lastUsedPrefix = _textFileName.Text;
                _lastUsedOptionalName = _textOptionalName.Text;
                _lastUsedDirectory = _textDirectory.Text;
                if (!useTimestamp)
                {
                    _sequenceNumber = seqNum + 1;
                }
                this.DialogResult = DialogResult.OK;
                this.Close();
            } catch (Exception ex) { MessageBox.Show(string.Format("ファイルの保存中にエラーが発生しました。\n{0}", ex.Message), "保存エラー", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        private ImageCodecInfo GetEncoder(ImageFormat format)
        {
            ImageCodecInfo[] codecs = ImageCodecInfo.GetImageDecoders();
            foreach (ImageCodecInfo codec in codecs) { if (codec.FormatID == format.Guid) { return codec; } }
            return null;
        }
    }

    // --- Core Application Logic ---

    public class CoreController : ApplicationContext
    {
        // Fields
        private AppState _state;
        private ClipboardMonitor _monitor;
        private string _lastImageHash;
        private bool _isWindowOpen = false;
        private int _sessionSequenceNumber = 1;
        private string _sessionLastUsedPrefix = "";
        private string _sessionLastUsedOptionalName = "";
        private string _sessionLastUsedDirectory = "";

        // Constructor
        public CoreController(string baseSaveDirectory)
        {
            // Initialize state with hardcoded defaults
            _state = new AppState
            {
                SaveDirectory = baseSaveDirectory,
                FileNamePrefix = "SS_",
                DefaultFormat = "JPG",
                JpgQuality = 80L
            };

            if (!Directory.Exists(_state.SaveDirectory))
            {
                Directory.CreateDirectory(_state.SaveDirectory);
            }

            // Start clipboard monitoring
            _monitor = new ClipboardMonitor();
            _monitor.ClipboardUpdate += OnClipboardUpdate;
        }

        public string GetSaveDirectory()
        {
            return _state.SaveDirectory;
        }

        private void OnClipboardUpdate(object sender, EventArgs e)
        {
            if (_isWindowOpen || !Clipboard.ContainsImage()) return;

            Bitmap clipboardBitmap = null;
            try
            {
                _isWindowOpen = true;
                clipboardBitmap = new Bitmap(Clipboard.GetImage());
                if (clipboardBitmap == null) return;

                string currentHash = GetImageHash(clipboardBitmap);
                if (currentHash == _lastImageHash) return;
                _lastImageHash = currentHash;

                using (var form = new SaveForm(clipboardBitmap, _state, _sessionSequenceNumber, _sessionLastUsedPrefix, _sessionLastUsedOptionalName, _sessionLastUsedDirectory))
                {
                    if (form.ShowDialog() == DialogResult.OK)
                    {
                        _sessionSequenceNumber = form.GetNextSequenceNumber();
                        _sessionLastUsedPrefix = form.GetLastUsedPrefix();
                        _sessionLastUsedOptionalName = form.GetLastUsedOptionalName();
                        _sessionLastUsedDirectory = form.GetLastUsedDirectory();
                        Clipboard.Clear();
                        _lastImageHash = null;
                    }
                }
            }
            catch {}
            finally
            {
                if (clipboardBitmap != null) { clipboardBitmap.Dispose(); }
                _isWindowOpen = false;
            }
        }

        private string GetImageHash(Bitmap bitmap)
        {
            using (var ms = new MemoryStream())
            using (var sha256 = SHA256.Create())
            {
                bitmap.Save(ms, ImageFormat.Bmp);
                byte[] hashBytes = sha256.ComputeHash(ms.ToArray());
                return BitConverter.ToString(hashBytes).Replace("-", "");
            }
        }

        public void Shutdown()
        {
            Console.WriteLine("\nPowerShotを終了します。");
            this.ExitThread();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _monitor != null)
            {
                _monitor.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    // --- Application Entry Point ---

    public static class Program
    {
        public static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            if (Environment.OSVersion.Version.Major >= 6) { User32.SetProcessDPIAware(); }
            if (args == null || args.Length < 1) return;
            string baseSaveDirectory = args[0];

            using (var controller = new CoreController(baseSaveDirectory))
            {
                Console.CancelKeyPress += (sender, e) => { e.Cancel = true; controller.Shutdown(); };

                Console.WriteLine("--- PowerShot v1.0 ---");
                Console.WriteLine("クリップボードの監視を開始しました。");
                Console.WriteLine(string.Format("保存先フォルダ: {0}", controller.GetSaveDirectory()));
                Console.WriteLine("終了するにはこのウィンドウで Ctrl+C を押してください。");
                Console.WriteLine("---------------------------------------------");

                Application.Run(controller);
            }
        }
    }
}
"@

# --- PowerShell Host Script ---

# C#の型がまだコンパイルされていない場合のみ、一度だけコンパイルを実行
if (-not ("PowerShot.WinForms.Program" -as [type])) {
    # 必要な.NETアセンブリをロード
    $requiredAssemblies = @(
        "System.Drawing",
        "System.Windows.Forms"
    )
    $requiredAssemblies | ForEach-Object { Add-Type -AssemblyName $_ }

    # C#ソースコードをメモリ上でコンパイル
    Add-Type -TypeDefinition $csharpSource -ReferencedAssemblies $requiredAssemblies
}

# スクリプトのパスを基準に保存先のルートフォルダを決定
$scriptPath = Split-Path $MyInvocation.MyCommand.Path -Parent
$saveDirectory = Join-Path $scriptPath "ScreenShots"

# コンパイルされたC#コードのMainメソッドを呼び出し、アプリケーションを起動
[PowerShot.WinForms.Program]::Main(@($saveDirectory))
