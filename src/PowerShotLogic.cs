using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace PowerShot
{
    // ============================================================
    // App Settings
    // ============================================================
    [DataContract]
    public class AppSettings
    {
        [DataMember(Order = 1)] public string SaveFolder { get; set; }

        [DataMember(Order = 4)] public int JpegQuality { get; set; }
        [DataMember(Order = 5)] public bool EmbedSysInfo { get; set; }
        [DataMember(Order = 6)] public string SysInfoPosition { get; set; }
        [DataMember(Order = 7)] public string OverlayText { get; set; }
        [DataMember(Order = 8)] public string OverlayTextPosition { get; set; }
        [DataMember(Order = 9)] public int ClipboardPollingInterval { get; set; }
        [DataMember(Order = 11)] public string TimestampTemplate { get; set; }
        [DataMember(Order = 12)] public string HotkeyMonitorCapture { get; set; }
        [DataMember(Order = 13)] public bool CropEnabled { get; set; }
        [DataMember(Order = 14)] public int CropX { get; set; }
        [DataMember(Order = 15)] public int CropY { get; set; }
        [DataMember(Order = 16)] public int CropWidth { get; set; }
        [DataMember(Order = 17)] public int CropHeight { get; set; }
        [DataMember(Order = 18)] public bool OverlayEnabled { get; set; }

        public static AppSettings Default()
        {
            return new AppSettings
            {
                SaveFolder = @".\Screenshots",

                JpegQuality = 80,
                EmbedSysInfo = false,
                SysInfoPosition = "TopLeft",
                OverlayText = "",
                OverlayTextPosition = "TopLeft",
                ClipboardPollingInterval = 200,
                TimestampTemplate = "yyyyMMdd-HHmmss",
                HotkeyMonitorCapture = "Shift+PrintScreen",
                CropEnabled = false,
                CropX = 0,
                CropY = 0,
                CropWidth = 0,
                CropHeight = 0,
                OverlayEnabled = false
            };
        }
    }

    internal static class SettingsManager
    {
        public static AppSettings Load(string path)
        {
            if (!File.Exists(path))
            {
                var def = AppSettings.Default();
                Save(path, def);
                return def;
            }
            try
            {
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read))
                {
                    var serializer = new DataContractJsonSerializer(typeof(AppSettings));
                    var settings = (AppSettings)serializer.ReadObject(fs);
                    if (string.IsNullOrEmpty(settings.TimestampTemplate)) settings.TimestampTemplate = "yyyyMMdd-HHmmss";
                    if (settings.JpegQuality <= 0) settings.JpegQuality = 80;
                    if (string.IsNullOrEmpty(settings.SaveFolder)) settings.SaveFolder = @".\Screenshots";
                    return settings;
                }
            }
            catch
            {
                return AppSettings.Default();
            }
        }

        public static void Save(string path, AppSettings settings)
        {
            try
            {
                using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write))
                {
                    var serializer = new DataContractJsonSerializer(typeof(AppSettings));
                    serializer.WriteObject(fs, settings);
                }
            }
            catch { }
        }
    }

    // ============================================================
    // Win32 Native Methods
    // ============================================================
    internal static class NativeMethods
    {
        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool AddClipboardFormatListener(IntPtr hwnd);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool RemoveClipboardFormatListener(IntPtr hwnd);

        [DllImport("user32.dll")]
        public static extern bool SetProcessDPIAware();

        public const int WM_CLIPBOARDUPDATE = 0x031D;

        // SHGetFileInfo for system icons
        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr SHGetFileInfo(
            string pszPath, uint dwFileAttributes,
            ref SHFILEINFO psfi, uint cbSizeFileInfo, uint uFlags);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool DestroyIcon(IntPtr hIcon);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct SHFILEINFO
        {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        }

        public const uint SHGFI_ICON = 0x100;
        public const uint SHGFI_SMALLICON = 0x1;
        public const uint SHGFI_USEFILEATTRIBUTES = 0x10;
        public const uint FILE_ATTRIBUTE_NORMAL = 0x80;
        public const uint FILE_ATTRIBUTE_DIRECTORY = 0x10;
    }

    // ============================================================
    // Data Model for Explorer ListView items
    // ============================================================
    public class ExplorerItem
    {
        public ImageSource Icon { get; set; }
        public string Name { get; set; }
        public string FullPath { get; set; }
        public bool IsDirectory { get; set; }
        public DateTime LastModified { get; set; }
    }

    // ============================================================
    // Session State (persisted across UI show/hide within same PS process)
    // ============================================================
    public class SessionState
    {
        public string LastDirectory { get; set; }
        public string LastPrefix { get; set; }
        public int LastSequenceDigits { get; set; }
        public bool IsSystemInfoVisible { get; set; }

        public string LastFormat { get; set; }

        public SessionState()
        {
            LastDirectory = "";
            LastPrefix = "";
            LastSequenceDigits = 3;
            IsSystemInfoVisible = false;
            LastFormat = "jpg";
        }
    }

    // ============================================================
    // Icon Helper — extracts system icons via SHGetFileInfo
    // ============================================================
    internal static class IconHelper
    {
        private static Dictionary<string, ImageSource> _iconCache = new Dictionary<string, ImageSource>(StringComparer.OrdinalIgnoreCase);

        public static ImageSource GetIcon(string path, bool isDirectory)
        {
            string cacheKey = isDirectory ? "<DIR>" : Path.GetExtension(path).ToLowerInvariant();
            if (string.IsNullOrEmpty(cacheKey)) cacheKey = "<NOEXT>";

            if (_iconCache.ContainsKey(cacheKey))
                return _iconCache[cacheKey];

            var shinfo = new NativeMethods.SHFILEINFO();
            uint flags = NativeMethods.SHGFI_ICON | NativeMethods.SHGFI_SMALLICON | NativeMethods.SHGFI_USEFILEATTRIBUTES;
            uint fileAttr = isDirectory ? NativeMethods.FILE_ATTRIBUTE_DIRECTORY : NativeMethods.FILE_ATTRIBUTE_NORMAL;

            NativeMethods.SHGetFileInfo(
                path, fileAttr, ref shinfo,
                (uint)Marshal.SizeOf(typeof(NativeMethods.SHFILEINFO)), flags);

            ImageSource iconSource = null;
            if (shinfo.hIcon != IntPtr.Zero)
            {
                try
                {
                    iconSource = Imaging.CreateBitmapSourceFromHIcon(
                        shinfo.hIcon, Int32Rect.Empty,
                        BitmapSizeOptions.FromEmptyOptions());
                    iconSource.Freeze();
                }
                finally
                {
                    NativeMethods.DestroyIcon(shinfo.hIcon);
                }
            }

            _iconCache[cacheKey] = iconSource;
            return iconSource;
        }
    }

    // ============================================================
    // Sequence Manager — intelligent auto-incrementing file numbering
    // ============================================================
    internal static class SequenceManager
    {
        /// <summary>
        /// Scans folder for files matching prefix pattern and returns nextSequence number.
        /// </summary>
        public static int GetNextSequence(string directoryPath, string prefix, string optionName)
        {
            if (!Directory.Exists(directoryPath))
                return 1;

            if (string.IsNullOrWhiteSpace(prefix))
                return 1;

            string escapedPrefix = Regex.Escape(prefix);
            string pattern;

            if (!string.IsNullOrWhiteSpace(optionName))
            {
                string escapedOption = Regex.Escape(optionName);
                pattern = string.Format(@"^{0}_{1}_(\d+)\.(png|jpg)$", escapedPrefix, escapedOption);
            }
            else
            {
                pattern = string.Format(@"^{0}_(\d+)\.(png|jpg)$", escapedPrefix);
            }

            int maxSeq = 0;
            try
            {
                var files = Directory.GetFiles(directoryPath);
                foreach (var file in files)
                {
                    string fileName = Path.GetFileName(file);
                    var match = Regex.Match(fileName, pattern, RegexOptions.IgnoreCase);
                    if (match.Success)
                    {
                        int seq;
                        if (int.TryParse(match.Groups[1].Value, out seq))
                        {
                            if (seq > maxSeq) maxSeq = seq;
                        }
                    }
                }
            }
            catch { }

            return maxSeq + 1;
        }
    }

    // ============================================================
    // File Manager — save logic, validation, naming
    // ============================================================
    internal static class FileManager
    {
        private static readonly char[] ForbiddenChars = new char[] { '\\', '/', ':', '*', '?', '"', '<', '>', '|' };

        /// <summary>
        /// Validates that the given name does not contain forbidden characters.
        /// Returns null if valid, or an error message string if invalid.
        /// </summary>
        public static string ValidateName(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;

            int index = name.IndexOfAny(ForbiddenChars);
            if (index >= 0)
            {
                return string.Format("ファイル名に使用できない文字が含まれています: '{0}'\n禁則文字: \\ / : * ? \" < > |", name[index]);
            }

            index = name.IndexOfAny(Path.GetInvalidFileNameChars());
            if (index >= 0)
            {
                return string.Format("ファイル名に使用できない文字が含まれています: (Code: {0})", (int)name[index]);
            }

            return null;
        }

        /// <summary>
        /// Generates the filename based on prefix, optionName, sequence, and format.
        /// </summary>
        public static string GenerateFileName(string prefix, string optionName, string seqStr, string format, string timestampTemplate = "yyyyMMdd-HHmmss")
        {
            string ext = format.ToLowerInvariant();

            if (string.IsNullOrWhiteSpace(prefix) && string.IsNullOrWhiteSpace(optionName))
            {
                string middle;
                if (timestampTemplate == "SEQ")
                {
                    middle = seqStr;
                }
                else if (timestampTemplate == "yyyyMMdd_SEQ")
                {
                    middle = DateTime.Now.ToString("yyyyMMdd") + "_" + seqStr;
                }
                else
                {
                    middle = DateTime.Now.ToString(timestampTemplate);
                }
                return string.Format("SS_{0}.{1}", middle, ext);
            }

            string baseName = prefix;
            if (!string.IsNullOrWhiteSpace(optionName))
            {
                baseName += "_" + optionName;
            }
            baseName += "_" + seqStr;
            return baseName + "." + ext;
        }

        /// <summary>
        /// Saves a Bitmap to the specified directory with the given filename.
        /// Returns null on success, or an error message on failure.
        /// </summary>
        public static string SaveImage(Bitmap bitmap, string directory, string fileName, string format, long jpgQuality)
        {
            try
            {
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                string fullPath = Path.Combine(directory, fileName);

                if (format.Equals("jpg", StringComparison.OrdinalIgnoreCase))
                {
                    var jpgEncoder = GetEncoder(ImageFormat.Jpeg);
                    if (jpgEncoder != null)
                    {
                        var encoderParams = new EncoderParameters(1);
                        encoderParams.Param[0] = new EncoderParameter(
                            System.Drawing.Imaging.Encoder.Quality, jpgQuality);
                        bitmap.Save(fullPath, jpgEncoder, encoderParams);
                    }
                    else
                    {
                        bitmap.Save(fullPath, ImageFormat.Jpeg);
                    }
                }
                else
                {
                    bitmap.Save(fullPath, ImageFormat.Png);
                }

                return null; // success
            }
            catch (Exception ex)
            {
                return string.Format("ファイルの保存に失敗しました:\n{0}", ex.Message);
            }
        }

        private static ImageCodecInfo GetEncoder(ImageFormat format)
        {
            var codecs = ImageCodecInfo.GetImageDecoders();
            foreach (var codec in codecs)
            {
                if (codec.FormatID == format.Guid)
                    return codec;
            }
            return null;
        }
    }

    // ============================================================
    // SettingsWindow Logic
    // ============================================================
    public class SettingsWindowController
    {
        private Window _window;
        private AppSettings _settings;
        private string _settingsPath;

        private TextBox _saveFolderTextBox;
        private Button _browseFolderButton;

        private ComboBox _timestampTemplateComboBox;
        private TextBlock _timestampPreviewLabel;
        private Slider _jpegQualitySlider;
        private TextBlock _jpegQualityValueLabel;
        private TextBox _pollingIntervalTextBox;
        private Button _okButton;
        private Button _cancelButton;
        private Button _closeToolButton;
        private Grid _titleBarGrid;
        
        private int _previewDigits;
        public bool SettingsChanged { get; private set; }

        public SettingsWindowController(Window window, AppSettings settings, string settingsPath, int previewDigits)
        {
            _window = window;
            _settings = settings;
            _settingsPath = settingsPath;
            _previewDigits = previewDigits > 0 ? previewDigits : 4;
            SettingsChanged = false;

            FindControls();
            BindEvents();
            Initialize();
        }

        private void FindControls()
        {
            _saveFolderTextBox = (TextBox)_window.FindName("SaveFolderTextBox");
            _browseFolderButton = (Button)_window.FindName("BrowseFolderButton");

            _timestampTemplateComboBox = (ComboBox)_window.FindName("TimestampTemplateComboBox");
            _timestampPreviewLabel = (TextBlock)_window.FindName("TimestampPreviewLabel");
            _jpegQualitySlider = (Slider)_window.FindName("JpegQualitySlider");
            _jpegQualityValueLabel = (TextBlock)_window.FindName("JpegQualityValueLabel");
            _pollingIntervalTextBox = (TextBox)_window.FindName("PollingIntervalTextBox");
            _okButton = (Button)_window.FindName("OkButton");
            _cancelButton = (Button)_window.FindName("CancelButton");
            _closeToolButton = (Button)_window.FindName("CloseToolButton");
            _titleBarGrid = (Grid)_window.FindName("TitleBarGrid");
        }

        private void BindEvents()
        {
            _titleBarGrid.MouseDown += (s, e) => { if (e.ChangedButton == MouseButton.Left) _window.DragMove(); };
            _closeToolButton.Click += (s, e) => _window.Close();
            
            _cancelButton.Click += (s, e) => _window.Close();
            _okButton.Click += OkButton_Click;
            _browseFolderButton.Click += BrowseFolderButton_Click;

            _jpegQualitySlider.ValueChanged += (s, e) => _jpegQualityValueLabel.Text = ((int)e.NewValue).ToString();
            
            _timestampTemplateComboBox.SelectionChanged += (s, e) => UpdateTimestampPreview();
        }

        private void Initialize()
        {
            _saveFolderTextBox.Text = _settings.SaveFolder;

            
            _jpegQualitySlider.Value = _settings.JpegQuality;
            _jpegQualityValueLabel.Text = _settings.JpegQuality.ToString();
            
            _pollingIntervalTextBox.Text = _settings.ClipboardPollingInterval.ToString();

            // Select matching timestamp template
            foreach (ComboBoxItem item in _timestampTemplateComboBox.Items)
            {
                if ((string)item.Tag == _settings.TimestampTemplate)
                {
                    _timestampTemplateComboBox.SelectedItem = item;
                    break;
                }
            }
        }

        private void UpdateTimestampPreview()
        {
            var item = _timestampTemplateComboBox.SelectedItem as ComboBoxItem;
            if (item != null)
            {
                string template = (string)item.Tag;
                if (template == "yyyyMMdd_SEQ")
                {
                    string seqStr = 1.ToString("D" + _previewDigits);
                    _timestampPreviewLabel.Text = DateTime.Now.ToString("yyyyMMdd") + "_" + seqStr;
                }
                else
                {
                    try { _timestampPreviewLabel.Text = DateTime.Now.ToString(template); }
                    catch { _timestampPreviewLabel.Text = "Invalid Format"; }
                }
            }
        }

        private void BrowseFolderButton_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                dialog.Description = "保存先フォルダを選択してください";
                if (!string.IsNullOrEmpty(_saveFolderTextBox.Text))
                {
                    try { dialog.SelectedPath = Path.GetFullPath(Path.Combine(_settingsPath, "..", _saveFolderTextBox.Text)); } catch { }
                }

                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    _saveFolderTextBox.Text = dialog.SelectedPath;
                }
            }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            _settings.SaveFolder = _saveFolderTextBox.Text;

            _settings.JpegQuality = (int)_jpegQualitySlider.Value;
            
            int interval;
            if (int.TryParse(_pollingIntervalTextBox.Text, out interval))
                _settings.ClipboardPollingInterval = interval;

            var tsItem = _timestampTemplateComboBox.SelectedItem as ComboBoxItem;
            if (tsItem != null)
                _settings.TimestampTemplate = (string)tsItem.Tag;

            SettingsManager.Save(_settingsPath, _settings);
            SettingsChanged = true;
            _window.Close();
        }

        public bool ShowDialog()
        {
            _window.ShowDialog();
            return SettingsChanged;
        }
    }

    // ============================================================
    // MainWindow Logic (CodeBehind bound dynamically)
    // ============================================================
    public class MainWindowController
    {
        private Window _window;
        private Bitmap _capturedBitmap;
        private string _rootBoundary;
        private string _scriptDir;
        private string _currentDirectory;
        private SessionState _session;
        private AppSettings _settings;
        private bool _suppressSequenceUpdate = false;

        // UI Controls
        private System.Windows.Controls.Image _previewImage;
        private ListView _explorerListView;
        private Button _backButton;
        private TextBlock _pathDisplay;
        private TextBox _prefixTextBox;
        private TextBox _sequenceTextBox;
        private ComboBox _formatComboBox;
        private TextBlock _fileNamePreview;
        private Button _saveButton;
        private ComboBox _digitsComboBox;
        private Border _newFolderPanel;
        private TextBox _newFolderNameTextBox;
        private Button _createFolderButton;
        private Button _cancelFolderButton;
        
        // Crop Controls
        private Canvas _cropCanvas;
        private System.Windows.Shapes.Rectangle _cropSelectionRect;
        private CheckBox _cropEnableCheckBox;
        private TextBox _cropXTextBox;
        private TextBox _cropYTextBox;
        private TextBox _cropWidthTextBox;
        private TextBox _cropHeightTextBox;
        private Button _resetCropButton;
        
        // Overlay Controls
        private CheckBox _overlayEnableCheckBox;
        private CheckBox _embedSysInfoCheckBox;
        private ComboBox _sysInfoPositionComboBox;
        private TextBox _overlayTextBox;
        private ComboBox _overlayTextPositionComboBox;
        private Button _updatePreviewButton;
        private Button _newFolderButton;
        private Button _deleteButton;
        private Grid _titleBarGrid;
        private Button _closeToolButton;
        private Button _settingsToolButton;

        public bool Saved { get; private set; }

        public MainWindowController(Window window, Bitmap capturedBitmap, string scriptDir, AppSettings settings, SessionState session)
        {
            _window = window;
            _capturedBitmap = capturedBitmap;
            _scriptDir = scriptDir;
            _settings = settings;
            
            string rootPath = Path.GetFullPath(Path.Combine(scriptDir, settings.SaveFolder));
            _rootBoundary = NormalizePath(rootPath);
            _session = session;
            Saved = false;

            // Determine initial directory
            if (!string.IsNullOrEmpty(_session.LastDirectory) && Directory.Exists(_session.LastDirectory))
            {
                string normLast = NormalizePath(_session.LastDirectory);
                if (normLast.StartsWith(_rootBoundary, StringComparison.OrdinalIgnoreCase))
                {
                    _currentDirectory = _session.LastDirectory;
                }
                else
                {
                    _currentDirectory = rootPath;
                }
            }
            else
            {
                _currentDirectory = rootPath;
            }

            FindControls();
            BindEvents();
            Initialize();
        }

        private void FindControls()
        {
            _previewImage = (System.Windows.Controls.Image)_window.FindName("PreviewImage");
            _explorerListView = (ListView)_window.FindName("ExplorerListView");
            _backButton = (Button)_window.FindName("BackButton");
            _pathDisplay = (TextBlock)_window.FindName("PathDisplay");
            _prefixTextBox = (TextBox)_window.FindName("PrefixTextBox");
            _sequenceTextBox = (TextBox)_window.FindName("SequenceTextBox");
            _saveButton = (Button)_window.FindName("SaveButton");
            _digitsComboBox = (ComboBox)_window.FindName("DigitsComboBox");
            _formatComboBox = (ComboBox)_window.FindName("FormatComboBox");
            _fileNamePreview = (TextBlock)_window.FindName("FileNamePreview");
            _newFolderPanel = (Border)_window.FindName("NewFolderPanel");
            _newFolderNameTextBox = (TextBox)_window.FindName("NewFolderNameTextBox");
            _createFolderButton = (Button)_window.FindName("CreateFolderButton");
            _cancelFolderButton = (Button)_window.FindName("CancelFolderButton");
            _newFolderButton = (Button)_window.FindName("NewFolderButton");
            _deleteButton = (Button)_window.FindName("DeleteButton");
            _titleBarGrid = (Grid)_window.FindName("TitleBarGrid");
            _closeToolButton = (Button)_window.FindName("CloseToolButton");
            _settingsToolButton = (Button)_window.FindName("SettingsToolButton");
            
            _cropCanvas = (Canvas)_window.FindName("CropCanvas");
            _cropSelectionRect = (System.Windows.Shapes.Rectangle)_window.FindName("CropSelectionRect");
            _cropEnableCheckBox = (CheckBox)_window.FindName("CropEnableCheckBox");
            _cropXTextBox = (TextBox)_window.FindName("CropXTextBox");
            _cropYTextBox = (TextBox)_window.FindName("CropYTextBox");
            _cropWidthTextBox = (TextBox)_window.FindName("CropWidthTextBox");
            _cropHeightTextBox = (TextBox)_window.FindName("CropHeightTextBox");
            _resetCropButton = (Button)_window.FindName("ResetCropButton");
            
            _overlayEnableCheckBox = (CheckBox)_window.FindName("OverlayEnableCheckBox");
            _embedSysInfoCheckBox = (CheckBox)_window.FindName("EmbedSysInfoCheckBox");
            _sysInfoPositionComboBox = (ComboBox)_window.FindName("SysInfoPositionComboBox");
            _overlayTextBox = (TextBox)_window.FindName("OverlayTextBox");
            _overlayTextPositionComboBox = (ComboBox)_window.FindName("OverlayTextPositionComboBox");
            _updatePreviewButton = (Button)_window.FindName("UpdatePreviewButton");
        }

        private void BindEvents()
        {
            _backButton.Click += BackButton_Click;
            _saveButton.Click += SaveButton_Click;
            _explorerListView.MouseDoubleClick += ExplorerListView_MouseDoubleClick;
            _explorerListView.SelectionChanged += ExplorerListView_SelectionChanged;
            _explorerListView.PreviewMouseLeftButtonUp += ExplorerListView_PreviewMouseLeftButtonUp;
            _prefixTextBox.TextChanged += PrefixOrOption_TextChanged;
            _sequenceTextBox.TextChanged += SequenceTextBox_TextChanged;
            _sequenceTextBox.PreviewTextInput += SequenceTextBox_PreviewTextInput;
            
            // Initial interlock check
            UpdateInputInterlock();
            
            if (_titleBarGrid != null)
            {
                _titleBarGrid.MouseDown += (s, e) => {
                    if (e.ChangedButton == MouseButton.Left) _window.DragMove();
                };
            }
            if (_closeToolButton != null)
            {
                _closeToolButton.Click += (s, e) => _window.Close();
            }
            if (_settingsToolButton != null)
            {
                _settingsToolButton.Click += SettingsToolButton_Click;
            }
            _formatComboBox.SelectionChanged += Format_SelectionChanged;
            if (_digitsComboBox != null) _digitsComboBox.SelectionChanged += DigitsComboBox_SelectionChanged;

            if (_newFolderButton != null) _newFolderButton.Click += NewFolderButton_Click;
            if (_createFolderButton != null) _createFolderButton.Click += CreateFolderButton_Click;
            if (_cancelFolderButton != null) _cancelFolderButton.Click += CancelFolderButton_Click;
            if (_newFolderNameTextBox != null) _newFolderNameTextBox.KeyDown += NewFolderNameTextBox_KeyDown;
            if (_deleteButton != null) _deleteButton.Click += DeleteButton_Click;

            _window.KeyDown += Window_KeyDown;
            _window.Closing += Window_Closing;
            
            // Register pasting handler for sequence
            DataObject.AddPastingHandler(_sequenceTextBox, SequenceTextBox_Pasting);

            // Crop Events
            if (_cropCanvas != null)
            {
                _cropCanvas.MouseDown += CropCanvas_MouseDown;
                _cropCanvas.MouseMove += CropCanvas_MouseMove;
                _cropCanvas.MouseUp += CropCanvas_MouseUp;
                _cropCanvas.MouseLeave += CropCanvas_MouseLeave;
            }
            if (_cropEnableCheckBox != null) _cropEnableCheckBox.Click += CropSettings_Changed;
            if (_resetCropButton != null) _resetCropButton.Click += ResetCropButton_Click;
            
            if (_cropXTextBox != null) _cropXTextBox.TextChanged += CropTextBox_TextChanged;
            if (_cropYTextBox != null) _cropYTextBox.TextChanged += CropTextBox_TextChanged;
            if (_cropWidthTextBox != null) _cropWidthTextBox.TextChanged += CropTextBox_TextChanged;
            if (_cropHeightTextBox != null) _cropHeightTextBox.TextChanged += CropTextBox_TextChanged;

            if (_overlayEnableCheckBox != null) _overlayEnableCheckBox.Click += OverlaySettings_Changed;
            if (_embedSysInfoCheckBox != null) _embedSysInfoCheckBox.Click += OverlaySettings_Changed;
            if (_sysInfoPositionComboBox != null) _sysInfoPositionComboBox.SelectionChanged += OverlaySettings_Changed;
            if (_overlayTextPositionComboBox != null) _overlayTextPositionComboBox.SelectionChanged += OverlaySettings_Changed;
            
            if (_updatePreviewButton != null) _updatePreviewButton.Click += UpdatePreviewButton_Click;
        }

        private void Initialize()
        {
            // Set preview image
            if (_capturedBitmap != null)
            {
                if (_cropCanvas != null)
                {
                    _cropCanvas.Width = _capturedBitmap.Width;
                    _cropCanvas.Height = _capturedBitmap.Height;
                }
                RedrawPreview();
            }

            // Restore session state
            _suppressSequenceUpdate = true;
            _prefixTextBox.Text = _session.LastPrefix ?? "";
            
            if (_digitsComboBox != null)
            {
                if (_session.LastSequenceDigits == -1)
                {
                    _digitsComboBox.SelectedIndex = 6;
                }
                else if (_session.LastSequenceDigits >= 1 && _session.LastSequenceDigits <= 6)
                {
                    _digitsComboBox.SelectedIndex = _session.LastSequenceDigits - 1;
                }

                // Ensure interlock is correct after loading session data
                UpdateInputInterlock();
                UpdateSequence();
            }

            if (_settings != null)
            {
                if (_cropEnableCheckBox != null) _cropEnableCheckBox.IsChecked = _settings.CropEnabled;
                if (_cropCanvas != null) _cropCanvas.Visibility = _settings.CropEnabled ? Visibility.Visible : Visibility.Collapsed;
                
                SyncCropTextBoxesFromSettings();
                SyncCropRectFromSettings();

                if (_overlayEnableCheckBox != null) _overlayEnableCheckBox.IsChecked = _settings.OverlayEnabled;
                if (_embedSysInfoCheckBox != null) _embedSysInfoCheckBox.IsChecked = _settings.EmbedSysInfo;
                if (_overlayTextBox != null) _overlayTextBox.Text = _settings.OverlayText;
                
                SetComboBoxByTag(_sysInfoPositionComboBox, _settings.SysInfoPosition);
                SetComboBoxByTag(_overlayTextPositionComboBox, _settings.OverlayTextPosition);
            }

            if (_formatComboBox != null)
            {
                _formatComboBox.SelectedIndex = (_session.LastFormat == "png") ? 1 : 0;
            }
            
            _suppressSequenceUpdate = false;

            // Load directory and compute sequence
            NavigateToDirectory(_currentDirectory);

            // Set Initial Focus to Prefix field
            _prefixTextBox.Focus();
        }

        // --- Navigation ---

        private void NavigateToDirectory(string path)
        {
            string normalizedTarget = NormalizePath(path);
            if (!normalizedTarget.StartsWith(_rootBoundary, StringComparison.OrdinalIgnoreCase))
            {
                return; // Block access above root boundary
            }

            if (!Directory.Exists(path))
            {
                return;
            }

            _currentDirectory = path;
            _session.LastDirectory = path;

            // Update path display (relative to root)
            string root = NormalizePath(_rootBoundary).TrimEnd('\\');
            string current = NormalizePath(_currentDirectory).TrimEnd('\\');

            if (current.Equals(root, StringComparison.OrdinalIgnoreCase))
            {
                _pathDisplay.Text = @".\";
            }
            else if (current.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                string relative = current.Substring(root.Length).TrimStart('\\', '/');
                _pathDisplay.Text = string.IsNullOrEmpty(relative) ? @".\" : @".\" + relative;
            }
            else
            {
                // Fallback if somehow outside root
                _pathDisplay.Text = Path.GetFileName(current);
            }

            RefreshExplorer();
            UpdateSequence();
            UpdateFileNamePreview();
        }

        private void RefreshExplorer()
        {
            var items = new List<ExplorerItem>();

            try
            {
                // Folders sorted by name ascending
                var dirs = Directory.GetDirectories(_currentDirectory).OrderBy(d => d, StringComparer.OrdinalIgnoreCase).ToArray();
                foreach (var dir in dirs)
                {
                    items.Add(new ExplorerItem
                    {
                        Name = Path.GetFileName(dir),
                        FullPath = dir,
                        IsDirectory = true,
                        LastModified = Directory.GetLastWriteTime(dir),
                        Icon = IconHelper.GetIcon(dir, true)
                    });
                }

                // Files sorted by name ascending
                var fileInfos = new DirectoryInfo(_currentDirectory).GetFiles();
                var sortedFiles = fileInfos.OrderBy(f => f.Name).ToArray();
                foreach (var fi in sortedFiles)
                {
                    items.Add(new ExplorerItem
                    {
                        Name = fi.Name,
                        FullPath = fi.FullName,
                        IsDirectory = false,
                        LastModified = fi.LastWriteTime,
                        Icon = IconHelper.GetIcon(fi.FullName, false)
                    });
                }

                _explorerListView.ItemsSource = items;

                // Auto-resize first column to content
                GridView gv = _explorerListView.View as GridView;
                if (gv != null && gv.Columns.Count > 0)
                {
                    gv.Columns[0].Width = 0;
                    gv.Columns[0].Width = Double.NaN;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    string.Format("フォルダの読み込みに失敗しました:\n{0}", ex.Message),
                    "PowerShot", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

        }

        // --- Sequence ---

        private void UpdateSequence()
        {
            int digits = GetSelectedDigits();
            if (_sequenceTextBox != null)
            {
                if (digits == -1) // "日時" case
                {
                    _sequenceTextBox.MaxLength = 50;
                    _sequenceTextBox.Text = DateTime.Now.ToString(_settings.TimestampTemplate);
                }
                else
                {
                    int maxLen = digits > 0 ? digits : 4;
                    _sequenceTextBox.MaxLength = maxLen;
                    
                    string effectivePrefix = _prefixTextBox.Text;
                    if (string.IsNullOrWhiteSpace(effectivePrefix))
                    {
                        if (_settings.TimestampTemplate == "SEQ") effectivePrefix = "SS";
                        else if (_settings.TimestampTemplate == "yyyyMMdd_SEQ") effectivePrefix = "SS_" + DateTime.Now.ToString("yyyyMMdd");
                    }
                    
                    int seq = SequenceManager.GetNextSequence(
                        _currentDirectory, effectivePrefix, "");
                    _sequenceTextBox.Text = seq.ToString("D" + maxLen);
                }
            }
        }

        private int GetSelectedDigits()
        {
            if (_digitsComboBox != null && _digitsComboBox.SelectedIndex >= 0)
            {
                // Index 6 is "日時" (yyyyMMdd-HHmmss)
                if (_digitsComboBox.SelectedIndex == 6) return -1;
                return _digitsComboBox.SelectedIndex + 1;
            }
            return 3;
        }

        // --- Filename Preview ---

        private void UpdateFileNamePreview()
        {
            if (_fileNamePreview == null || _prefixTextBox == null || _sequenceTextBox == null) return;

            string format = GetSelectedFormat();
            _fileNamePreview.Text = FileManager.GenerateFileName(
                _prefixTextBox.Text, "", _sequenceTextBox.Text, format, _settings.TimestampTemplate);
        }

        private string GetSelectedFormat()
        {
            if (_formatComboBox.SelectedIndex == 1) return "png";
            return "jpg";
        }

        // --- Event Handlers ---

        private void SettingsToolButton_Click(object sender, RoutedEventArgs e)
        {
            string xamlPath = Path.Combine(_scriptDir, "SettingsWindow.xaml");
            if (!File.Exists(xamlPath))
            {
                MessageBox.Show("SettingsWindow.xaml が見つかりません。", "PowerShot", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            Window settingsWindow;
            using (var fs = new FileStream(xamlPath, FileMode.Open, FileAccess.Read))
            {
                settingsWindow = (Window)XamlReader.Load(fs);
            }
            settingsWindow.Owner = _window;
            
            string settingsPath = Path.Combine(_scriptDir, "settings.json");
            int currentDigits = GetSelectedDigits();
            var controller = new SettingsWindowController(settingsWindow, _settings, settingsPath, currentDigits);
            if (controller.ShowDialog())
            {
                string newRoot = Path.GetFullPath(Path.Combine(_scriptDir, _settings.SaveFolder));
                _rootBoundary = NormalizePath(newRoot);
                
                // Force navigation if current path is no longer valid or we want to update the tree
                if (!Directory.Exists(_currentDirectory) || !_currentDirectory.StartsWith(_rootBoundary, StringComparison.OrdinalIgnoreCase))
                {
                    NavigateToDirectory(_rootBoundary);
                }
                else
                {
                    // Refresh view
                    UpdateInputInterlock();
                    UpdateSequence();
                    UpdateFileNamePreview();
                }
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            string parent = Path.GetDirectoryName(_currentDirectory);
            if (!string.IsNullOrEmpty(parent))
            {
                NavigateToDirectory(parent); // Will be blocked by root boundary check inside
            }
        }

        private void ExplorerListView_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            var dep = (DependencyObject)e.OriginalSource;
            while (dep != null && !(dep is ListViewItem))
            {
                dep = VisualTreeHelper.GetParent(dep);
            }

            ListViewItem lvi = dep as ListViewItem;
            if (lvi != null)
            {
                ExplorerItem item = lvi.Content as ExplorerItem;
                if (item != null && !item.IsDirectory)
                {
                    ParseAndPopulateFromFileName(item.Name);
                }
            }
        }

        private void ExplorerListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var item = _explorerListView.SelectedItem as ExplorerItem;
            if (item == null) return;

            if (item.IsDirectory)
            {
                NavigateToDirectory(item.FullPath);
            }
            else
            {
                // Open preview window for image files
                OpenPreviewWindow(item.FullPath);
            }
        }

        private void ExplorerListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var count = _explorerListView.SelectedItems.Count;
            if (_deleteButton != null)
            {
                _deleteButton.IsEnabled = (count > 0);
            }

            if (count != 1) return;
            var item = _explorerListView.SelectedItem as ExplorerItem;
            if (item == null || item.IsDirectory) return;

            // Parse filename and populate form fields
            ParseAndPopulateFromFileName(item.Name);
        }

        private void ParseAndPopulateFromFileName(string fileName)
        {
            string nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
            // Patterns for timestamp: yyyyMMdd-HHmmss
            var matchTimestamp = Regex.Match(nameWithoutExt, @"^(.+?)_(\d{8}-\d{6})$");
            var matchTimestampOnly = Regex.Match(nameWithoutExt, @"^(\d{8}-\d{6})$");
            // Pattern for sequence: prefix_NNN
            var matchSimple = Regex.Match(nameWithoutExt, @"^(.+?)_(\d+)$");

            _suppressSequenceUpdate = true;

            if (matchTimestamp.Success)
            {
                _prefixTextBox.Text = matchTimestamp.Groups[1].Value;
                _digitsComboBox.SelectedIndex = 6; // 日時
            }
            else if (matchTimestampOnly.Success)
            {
                _prefixTextBox.Text = "";
                _digitsComboBox.SelectedIndex = 6; // 日時
            }
            else if (matchSimple.Success)
            {
                _prefixTextBox.Text = matchSimple.Groups[1].Value;
                int len = matchSimple.Groups[2].Length;
                if (len >= 1 && len <= 6)
                    _digitsComboBox.SelectedIndex = len - 1;
            }
            else
            {
                _prefixTextBox.Text = nameWithoutExt;
            }

            _suppressSequenceUpdate = false;

            // Sequence: always use auto-tracked latest value (not the parsed one)
            UpdateInputInterlock();
            UpdateSequence();
            UpdateFileNamePreview();
        }

        private void PrefixOrOption_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_suppressSequenceUpdate) return;
            UpdateInputInterlock();
            UpdateSequence();
            UpdateFileNamePreview();
        }

        private void UpdateInputInterlock()
        {
            bool hasPrefix = !string.IsNullOrWhiteSpace(_prefixTextBox.Text);
            if (_sequenceTextBox != null) _sequenceTextBox.IsEnabled = hasPrefix;
            if (_digitsComboBox != null) _digitsComboBox.IsEnabled = hasPrefix;
            // Format can be changed even without prefix (e.g. for SS_yyyyMMdd-HHmmss format)
            if (_formatComboBox != null) _formatComboBox.IsEnabled = true;
            // Relaxed: Save button is allowed even if prefix is empty
            if (_saveButton != null) _saveButton.IsEnabled = true;
        }

        private void SequenceTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_suppressSequenceUpdate) return;
            UpdateFileNamePreview();
        }

        private void SequenceTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // Only allow digits
            e.Handled = !Regex.IsMatch(e.Text, @"^\d+$");
        }

        private void SequenceTextBox_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            if (e.DataObject.GetDataPresent(DataFormats.Text))
            {
                string text = (string)e.DataObject.GetData(DataFormats.Text);
                if (!Regex.IsMatch(text, @"^\d+$"))
                {
                    e.CancelCommand();
                }
            }
            else
            {
                e.CancelCommand();
            }
        }

        private void DigitsComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressSequenceUpdate) return;
            UpdateSequence();
            UpdateFileNamePreview();
            
            // Limit input length based on selected digits
            if (_sequenceTextBox != null)
            {
                int digits = GetSelectedDigits();
                _sequenceTextBox.MaxLength = (digits > 0) ? digits : 20;
            }
        }

        private void Format_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateFileNamePreview();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (_capturedBitmap == null)
            {
                MessageBox.Show("保存する画像がありません。", "PowerShot",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Validate prefix
            string prefixError = FileManager.ValidateName(_prefixTextBox.Text);
            if (prefixError != null)
            {
                MessageBox.Show(prefixError, "PowerShot - 入力エラー",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string format = GetSelectedFormat();
            string fileName = FileManager.GenerateFileName(
                _prefixTextBox.Text, "", _sequenceTextBox.Text, format, _settings.TimestampTemplate);

            using (Bitmap clonedBmp = (Bitmap)_capturedBitmap.Clone())
            {
                Bitmap finalBmp = clonedBmp;
                if (_settings.CropEnabled)
                {
                    Rectangle cropRect = new Rectangle(_settings.CropX, _settings.CropY, _settings.CropWidth, _settings.CropHeight);
                    if (cropRect.X < 0) cropRect.X = 0;
                    if (cropRect.Y < 0) cropRect.Y = 0;
                    if (cropRect.Width < 10) cropRect.Width = 10;
                    if (cropRect.Height < 10) cropRect.Height = 10;
                    if (cropRect.Right > clonedBmp.Width) cropRect.Width = clonedBmp.Width - cropRect.X;
                    if (cropRect.Bottom > clonedBmp.Height) cropRect.Height = clonedBmp.Height - cropRect.Y;

                    finalBmp = clonedBmp.Clone(cropRect, clonedBmp.PixelFormat);
                }

                ApplyOverlays(finalBmp, false);
                string error = FileManager.SaveImage(finalBmp, _currentDirectory, fileName, format, _settings.JpegQuality);
                
                if (_settings.CropEnabled)
                {
                    finalBmp.Dispose();
                }

                if (error != null)
                {
                    MessageBox.Show(error, "PowerShot - 保存エラー",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }

            Saved = true;

            // Update session state
            _session.LastPrefix = _prefixTextBox.Text;
            _session.LastDirectory = _currentDirectory;
            _session.LastSequenceDigits = GetSelectedDigits();
            _session.LastFormat = GetSelectedFormat();

            // Close the window to wait for the next screenshot
            _window.Close();
        }

        // --- Crop ---

        private enum CropDragMode { None, DrawNew, Move, ResizeTopLeft, ResizeTopRight, ResizeBottomLeft, ResizeBottomRight, ResizeTop, ResizeBottom, ResizeLeft, ResizeRight }
        private CropDragMode _cropDragMode = CropDragMode.None;
        private System.Windows.Point _dragStartPoint;
        private Rect _dragStartRect;
        private bool _suppressCropTextBoxUpdate = false;

        private void CropSettings_Changed(object sender, RoutedEventArgs e)
        {
            if (_suppressSequenceUpdate || _settings == null) return;
            if (_cropEnableCheckBox != null)
            {
                _settings.CropEnabled = _cropEnableCheckBox.IsChecked ?? false;
                if (_cropCanvas != null) _cropCanvas.Visibility = _settings.CropEnabled ? Visibility.Visible : Visibility.Collapsed;
            }
            SettingsManager.Save(Path.Combine(_scriptDir, "settings.json"), _settings);
            RedrawPreview();
        }

        private void ResetCropButton_Click(object sender, RoutedEventArgs e)
        {
            if (_capturedBitmap == null || _settings == null) return;
            _settings.CropX = 0;
            _settings.CropY = 0;
            _settings.CropWidth = _capturedBitmap.Width;
            _settings.CropHeight = _capturedBitmap.Height;
            SettingsManager.Save(Path.Combine(_scriptDir, "settings.json"), _settings);
            SyncCropTextBoxesFromSettings();
            SyncCropRectFromSettings();
        }

        private void SyncCropTextBoxesFromSettings()
        {
            if (_settings == null || _cropXTextBox == null) return;
            _suppressCropTextBoxUpdate = true;
            _cropXTextBox.Text = _settings.CropX.ToString();
            _cropYTextBox.Text = _settings.CropY.ToString();
            _cropWidthTextBox.Text = _settings.CropWidth.ToString();
            _cropHeightTextBox.Text = _settings.CropHeight.ToString();
            _suppressCropTextBoxUpdate = false;
        }

        private void SyncCropRectFromSettings()
        {
            if (_settings == null || _cropSelectionRect == null || _capturedBitmap == null) return;
            
            if (_settings.CropWidth <= 0 || _settings.CropHeight <= 0)
            {
                _settings.CropWidth = _capturedBitmap.Width;
                _settings.CropHeight = _capturedBitmap.Height;
            }
            
            UpdateCropRectUI(new Rect(_settings.CropX, _settings.CropY, _settings.CropWidth, _settings.CropHeight));
        }

        private void UpdateCropRectUI(Rect rect)
        {
            if (_cropSelectionRect == null) return;
            Canvas.SetLeft(_cropSelectionRect, rect.X);
            Canvas.SetTop(_cropSelectionRect, rect.Y);
            _cropSelectionRect.Width = rect.Width;
            _cropSelectionRect.Height = rect.Height;
            _cropSelectionRect.Visibility = Visibility.Visible;
        }

        private void CropTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_suppressCropTextBoxUpdate || _settings == null || _capturedBitmap == null) return;
            
            int x, y, w, h;
            if (int.TryParse(_cropXTextBox.Text, out x) &&
                int.TryParse(_cropYTextBox.Text, out y) &&
                int.TryParse(_cropWidthTextBox.Text, out w) &&
                int.TryParse(_cropHeightTextBox.Text, out h))
            {
                if (x < 0) x = 0;
                if (y < 0) y = 0;
                if (w < 10) w = 10;
                if (h < 10) h = 10;
                if (x + w > _capturedBitmap.Width) w = _capturedBitmap.Width - x;
                if (y + h > _capturedBitmap.Height) h = _capturedBitmap.Height - y;

                _settings.CropX = x;
                _settings.CropY = y;
                _settings.CropWidth = w;
                _settings.CropHeight = h;
                
                UpdateCropRectUI(new Rect(x, y, w, h));
                SettingsManager.Save(Path.Combine(_scriptDir, "settings.json"), _settings);
                RedrawPreview();
            }
        }

        private CropDragMode GetCropDragMode(System.Windows.Point p)
        {
            if (_cropSelectionRect.Visibility != Visibility.Visible) return CropDragMode.DrawNew;

            double x = Canvas.GetLeft(_cropSelectionRect);
            double y = Canvas.GetTop(_cropSelectionRect);
            double w = _cropSelectionRect.Width;
            double h = _cropSelectionRect.Height;
            
            double margin = 8.0;

            bool left = Math.Abs(p.X - x) <= margin;
            bool right = Math.Abs(p.X - (x + w)) <= margin;
            bool top = Math.Abs(p.Y - y) <= margin;
            bool bottom = Math.Abs(p.Y - (y + h)) <= margin;
            
            bool insideX = p.X >= x && p.X <= x + w;
            bool insideY = p.Y >= y && p.Y <= y + h;

            if (top && left) return CropDragMode.ResizeTopLeft;
            if (top && right) return CropDragMode.ResizeTopRight;
            if (bottom && left) return CropDragMode.ResizeBottomLeft;
            if (bottom && right) return CropDragMode.ResizeBottomRight;
            
            if (top && insideX) return CropDragMode.ResizeTop;
            if (bottom && insideX) return CropDragMode.ResizeBottom;
            if (left && insideY) return CropDragMode.ResizeLeft;
            if (right && insideY) return CropDragMode.ResizeRight;
            
            if (insideX && insideY) return CropDragMode.Move;
            
            return CropDragMode.DrawNew;
        }

        private void SetCropCursor(CropDragMode mode)
        {
            if (_cropCanvas == null) return;
            switch (mode)
            {
                case CropDragMode.ResizeTopLeft:
                case CropDragMode.ResizeBottomRight:
                    _cropCanvas.Cursor = Cursors.SizeNWSE;
                    break;
                case CropDragMode.ResizeTopRight:
                case CropDragMode.ResizeBottomLeft:
                    _cropCanvas.Cursor = Cursors.SizeNESW;
                    break;
                case CropDragMode.ResizeTop:
                case CropDragMode.ResizeBottom:
                    _cropCanvas.Cursor = Cursors.SizeNS;
                    break;
                case CropDragMode.ResizeLeft:
                case CropDragMode.ResizeRight:
                    _cropCanvas.Cursor = Cursors.SizeWE;
                    break;
                case CropDragMode.Move:
                    _cropCanvas.Cursor = Cursors.SizeAll;
                    break;
                default:
                    _cropCanvas.Cursor = Cursors.Cross;
                    break;
            }
        }

        private void CropCanvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (_capturedBitmap == null || _cropEnableCheckBox == null || _cropEnableCheckBox.IsChecked != true) return;
            
            System.Windows.Point p = e.GetPosition(_cropCanvas);
            _dragStartPoint = p;
            _cropDragMode = GetCropDragMode(p);
            
            if (_cropDragMode == CropDragMode.DrawNew)
            {
                _dragStartRect = new Rect(p, new System.Windows.Size(0, 0));
                UpdateCropRectUI(_dragStartRect);
            }
            else
            {
                _dragStartRect = new Rect(
                    Canvas.GetLeft(_cropSelectionRect),
                    Canvas.GetTop(_cropSelectionRect),
                    _cropSelectionRect.Width,
                    _cropSelectionRect.Height);
            }
            
            _cropCanvas.CaptureMouse();
        }

        private void CropCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (_capturedBitmap == null || _cropEnableCheckBox == null || _cropEnableCheckBox.IsChecked != true) return;
            
            System.Windows.Point p = e.GetPosition(_cropCanvas);

            if (_cropDragMode == CropDragMode.None)
            {
                SetCropCursor(GetCropDragMode(p));
                return;
            }

            double dx = p.X - _dragStartPoint.X;
            double dy = p.Y - _dragStartPoint.Y;
            Rect newRect = _dragStartRect;

            if (_cropDragMode == CropDragMode.DrawNew)
            {
                double x = Math.Min(p.X, _dragStartPoint.X);
                double y = Math.Min(p.Y, _dragStartPoint.Y);
                double w = Math.Abs(p.X - _dragStartPoint.X);
                double h = Math.Abs(p.Y - _dragStartPoint.Y);
                newRect = new Rect(x, y, w, h);
            }
            else if (_cropDragMode == CropDragMode.Move)
            {
                newRect.X += dx;
                newRect.Y += dy;
            }
            else
            {
                if (_cropDragMode == CropDragMode.ResizeTopLeft || _cropDragMode == CropDragMode.ResizeLeft || _cropDragMode == CropDragMode.ResizeBottomLeft)
                {
                    newRect.X += dx;
                    newRect.Width -= dx;
                }
                if (_cropDragMode == CropDragMode.ResizeTopRight || _cropDragMode == CropDragMode.ResizeRight || _cropDragMode == CropDragMode.ResizeBottomRight)
                {
                    newRect.Width += dx;
                }
                if (_cropDragMode == CropDragMode.ResizeTopLeft || _cropDragMode == CropDragMode.ResizeTop || _cropDragMode == CropDragMode.ResizeTopRight)
                {
                    newRect.Y += dy;
                    newRect.Height -= dy;
                }
                if (_cropDragMode == CropDragMode.ResizeBottomLeft || _cropDragMode == CropDragMode.ResizeBottom || _cropDragMode == CropDragMode.ResizeBottomRight)
                {
                    newRect.Height += dy;
                }
                
                if (newRect.Width < 10) { newRect.Width = 10; if (_cropDragMode.ToString().Contains("Left")) newRect.X = _dragStartRect.Right - 10; }
                if (newRect.Height < 10) { newRect.Height = 10; if (_cropDragMode.ToString().Contains("Top")) newRect.Y = _dragStartRect.Bottom - 10; }
            }

            if (newRect.X < 0) newRect.X = 0;
            if (newRect.Y < 0) newRect.Y = 0;
            if (newRect.Right > _cropCanvas.Width) newRect.X = _cropCanvas.Width - newRect.Width;
            if (newRect.Bottom > _cropCanvas.Height) newRect.Y = _cropCanvas.Height - newRect.Height;
            if (newRect.Width > _cropCanvas.Width) newRect.Width = _cropCanvas.Width;
            if (newRect.Height > _cropCanvas.Height) newRect.Height = _cropCanvas.Height;
            if (newRect.X < 0) newRect.X = 0;

            UpdateCropRectUI(newRect);
            
            _suppressCropTextBoxUpdate = true;
            if (_cropXTextBox != null) _cropXTextBox.Text = Math.Round(newRect.X).ToString();
            if (_cropYTextBox != null) _cropYTextBox.Text = Math.Round(newRect.Y).ToString();
            if (_cropWidthTextBox != null) _cropWidthTextBox.Text = Math.Round(newRect.Width).ToString();
            if (_cropHeightTextBox != null) _cropHeightTextBox.Text = Math.Round(newRect.Height).ToString();
            _suppressCropTextBoxUpdate = false;
        }

        private void CropCanvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_cropDragMode != CropDragMode.None)
            {
                _cropCanvas.ReleaseMouseCapture();
                _cropDragMode = CropDragMode.None;
                
                if (_settings != null && _cropXTextBox != null)
                {
                    int x, y, w, h;
                    int.TryParse(_cropXTextBox.Text, out x);
                    int.TryParse(_cropYTextBox.Text, out y);
                    int.TryParse(_cropWidthTextBox.Text, out w);
                    int.TryParse(_cropHeightTextBox.Text, out h);
                    
                    if (w < 10) w = 10;
                    if (h < 10) h = 10;
                    
                    _settings.CropX = x;
                    _settings.CropY = y;
                    _settings.CropWidth = w;
                    _settings.CropHeight = h;
                    SettingsManager.Save(Path.Combine(_scriptDir, "settings.json"), _settings);
                    RedrawPreview();
                }
            }
        }
        
        private void CropCanvas_MouseLeave(object sender, MouseEventArgs e)
        {
            if (_cropDragMode == CropDragMode.None && _cropCanvas != null)
            {
                _cropCanvas.Cursor = Cursors.Arrow;
            }
        }

        // --- Overlay & System Info ---

        private void OverlaySettings_Changed(object sender, RoutedEventArgs e)
        {
            if (_suppressSequenceUpdate || _settings == null) return;

            // Save to settings
            if (_overlayEnableCheckBox != null) _settings.OverlayEnabled = _overlayEnableCheckBox.IsChecked ?? false;
            if (_embedSysInfoCheckBox != null) _settings.EmbedSysInfo = _embedSysInfoCheckBox.IsChecked ?? false;
            if (_overlayTextBox != null) _settings.OverlayText = _overlayTextBox.Text;
            
            var sysItem = _sysInfoPositionComboBox != null ? _sysInfoPositionComboBox.SelectedItem as ComboBoxItem : null;
            if (sysItem != null) _settings.SysInfoPosition = (string)sysItem.Tag;
            
            var txtItem = _overlayTextPositionComboBox != null ? _overlayTextPositionComboBox.SelectedItem as ComboBoxItem : null;
            if (txtItem != null) _settings.OverlayTextPosition = (string)txtItem.Tag;

            SettingsManager.Save(Path.Combine(_scriptDir, "settings.json"), _settings);
        }

        private void UpdatePreviewButton_Click(object sender, RoutedEventArgs e)
        {
            // Sync text before drawing
            if (_overlayTextBox != null && _settings != null)
            {
                _settings.OverlayText = _overlayTextBox.Text;
                SettingsManager.Save(Path.Combine(_scriptDir, "settings.json"), _settings);
            }
            RedrawPreview();
        }

        private void RedrawPreview()
        {
            if (_capturedBitmap == null) return;
            
            using (Bitmap previewBmp = (Bitmap)_capturedBitmap.Clone())
            {
                ApplyOverlays(previewBmp, true);
                _previewImage.Source = ConvertBitmapToImageSource(previewBmp);
            }
        }

        private PointF GetOverlayPosition(SizeF textSize, Rectangle bounds, string position, float padding)
        {
            float rectW = textSize.Width + padding * 2;
            float rectH = textSize.Height + padding * 2;
            float x = bounds.X + padding;
            float y = bounds.Y + padding;

            if (position == "TopRight")
            {
                x = bounds.Right - rectW - padding;
            }
            else if (position == "BottomLeft")
            {
                y = bounds.Bottom - rectH - padding;
            }
            else if (position == "BottomRight")
            {
                x = bounds.Right - rectW - padding;
                y = bounds.Bottom - rectH - padding;
            }
            
            if (x < bounds.X) x = bounds.X;
            if (y < bounds.Y) y = bounds.Y;
            return new PointF(x, y);
        }

        private void ApplyOverlays(Bitmap bmp, bool isPreview = false)
        {
            if (_settings == null || !_settings.OverlayEnabled) return;

            Rectangle bounds = new Rectangle(0, 0, bmp.Width, bmp.Height);
            
            if (isPreview && _settings.CropEnabled)
            {
                bounds = new Rectangle(_settings.CropX, _settings.CropY, _settings.CropWidth, _settings.CropHeight);
                if (bounds.X < 0) bounds.X = 0;
                if (bounds.Y < 0) bounds.Y = 0;
                if (bounds.Right > bmp.Width) bounds.Width = bmp.Width - bounds.X;
                if (bounds.Bottom > bmp.Height) bounds.Height = bmp.Height - bounds.Y;
            }
            var overlays = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<string>>();

            if (_settings.EmbedSysInfo)
            {
                string info = GetSystemInfoString();
                string pos = string.IsNullOrEmpty(_settings.SysInfoPosition) ? "TopLeft" : _settings.SysInfoPosition;
                if (!overlays.ContainsKey(pos)) overlays[pos] = new System.Collections.Generic.List<string>();
                overlays[pos].Add(info);
            }
            if (!string.IsNullOrWhiteSpace(_settings.OverlayText))
            {
                string pos = string.IsNullOrEmpty(_settings.OverlayTextPosition) ? "TopLeft" : _settings.OverlayTextPosition;
                if (!overlays.ContainsKey(pos)) overlays[pos] = new System.Collections.Generic.List<string>();
                overlays[pos].Add(_settings.OverlayText);
            }

            foreach (var kvp in overlays)
            {
                string combinedText = string.Join("\n", kvp.Value);
                DrawOverlayBlock(bmp, bounds, kvp.Key, combinedText);
            }
        }

        private void DrawOverlayBlock(Bitmap bmp, Rectangle bounds, string position, string text)
        {
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

                using (Font font = new Font("Segoe UI", 16, System.Drawing.FontStyle.Bold))
                {
                    SizeF textSize = g.MeasureString(text, font);
                    float padding = 12;
                    PointF pt = GetOverlayPosition(textSize, bounds, position, padding);

                    using (System.Drawing.SolidBrush brush = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(160, 0, 0, 0)))
                    {
                        g.FillRectangle(brush, pt.X, pt.Y, textSize.Width + padding * 2, textSize.Height + padding * 2);
                    }

                    using (System.Drawing.SolidBrush textBrush = new System.Drawing.SolidBrush(System.Drawing.Color.White))
                    {
                        g.DrawString(text, font, textBrush, pt.X + padding, pt.Y + padding);
                    }
                }
            }
        }

        private void SetComboBoxByTag(ComboBox cb, string tagValue)
        {
            if (cb == null || string.IsNullOrEmpty(tagValue)) return;
            foreach (ComboBoxItem item in cb.Items)
            {
                if ((string)item.Tag == tagValue)
                {
                    cb.SelectedItem = item;
                    break;
                }
            }
        }

        private string GetSystemInfoString()
        {
            try
            {
                string host = Dns.GetHostName();
                string domain = IPGlobalProperties.GetIPGlobalProperties().DomainName;
                string fqdn = string.IsNullOrEmpty(domain) ? host : host + "." + domain;
                string user = Environment.UserDomainName + "\\" + Environment.UserName;
                
                var ips = Dns.GetHostAddresses(host)
                    .Where(ip => ip.AddressFamily == AddressFamily.InterNetwork)
                    .Select(ip => ip.ToString())
                    .ToList();
                
                string ipStr = ips.Count > 0 ? string.Join(", ", ips.ToArray()) : "N/A";

                return string.Format("Host: {0} | User: {1} | IP: {2}", fqdn, user, ipStr);
            }
            catch { return "System Info Error"; }
        }

        // --- Folder Creation and Deletion ---

        private void NewFolderButton_Click(object sender, RoutedEventArgs e)
        {
            if (_newFolderPanel == null) return;
            _newFolderPanel.Visibility = Visibility.Visible;
            _newFolderNameTextBox.Text = "新しいフォルダー";
            _newFolderNameTextBox.SelectAll();
            _newFolderNameTextBox.Focus();
        }

        private void CancelFolderButton_Click(object sender, RoutedEventArgs e)
        {
            if (_newFolderPanel == null) return;
            _newFolderPanel.Visibility = Visibility.Collapsed;
        }

        private void CreateFolderButton_Click(object sender, RoutedEventArgs e)
        {
            string folderName = _newFolderNameTextBox.Text.Trim();
            if (string.IsNullOrEmpty(folderName)) return;

            string error = FileManager.ValidateName(folderName);
            if (error != null)
            {
                MessageBox.Show(error, "PowerShot - 入力エラー", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                string newPath = Path.Combine(_currentDirectory, folderName);
                if (!Directory.Exists(newPath))
                {
                    Directory.CreateDirectory(newPath);
                    RefreshExplorer();
                }
                else
                {
                    MessageBox.Show("同名のフォルダーが既に存在します。", "PowerShot", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("フォルダーの作成に失敗しました:\n" + ex.Message, "PowerShot", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            _newFolderPanel.Visibility = Visibility.Collapsed;
        }

        private void NewFolderNameTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                CreateFolderButton_Click(null, null);
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                CancelFolderButton_Click(null, null);
                e.Handled = true;
            }
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedItems = _explorerListView.SelectedItems.Cast<ExplorerItem>().ToList();
            if (selectedItems.Count == 0) return;

            string msg;
            if (selectedItems.Count == 1)
            {
                msg = string.Format("「{0}」を完全に削除してもよろしいですか？\nこの操作は元に戻せません。", selectedItems[0].Name);
            }
            else
            {
                msg = string.Format("選択された {0} 個の項目を完全に削除してもよろしいですか？\nこの操作は元に戻せません。", selectedItems.Count);
            }

            var result = MessageBox.Show(msg, "PowerShot - 削除の確認", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            
            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    foreach (var item in selectedItems)
                    {
                        if (item.IsDirectory)
                        {
                            Directory.Delete(item.FullPath, true);
                        }
                        else
                        {
                            File.Delete(item.FullPath);
                        }
                    }
                    RefreshExplorer();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("削除に失敗しました:\n" + ex.Message, "PowerShot", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                _window.Close();
            }
            else if (e.Key == Key.Delete)
            {
                // Trigger deletion if list or window has focus and item is selected
                DeleteButton_Click(null, null);
                e.Handled = true;
            }
            else if (e.Key == Key.S && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                SaveButton_Click(null, null);
                e.Handled = true;
            }
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            // Save session state on close
            _session.LastPrefix = _prefixTextBox.Text;
            _session.LastDirectory = _currentDirectory;
        }

        // --- Preview Window ---

        private void OpenPreviewWindow(string filePath)
        {
            string ext = Path.GetExtension(filePath).ToLowerInvariant();
            if (ext != ".png" && ext != ".jpg" && ext != ".jpeg" && ext != ".bmp" && ext != ".gif")
            {
                return; // Not an image file
            }

            try
            {
                // Use the passed-in script directory for XAML path
                string xamlPath = Path.Combine(_scriptDir, "PreviewWindow.xaml");

                if (!File.Exists(xamlPath))
                {
                    MessageBox.Show("PreviewWindow.xaml が見つかりません。",
                        "PowerShot", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                Window previewWindow;
                using (var fs = new FileStream(xamlPath, FileMode.Open, FileAccess.Read))
                {
                    previewWindow = (Window)XamlReader.Load(fs);
                }

                previewWindow.Title = "PowerShot - " + Path.GetFileName(filePath);
                previewWindow.Owner = _window;

                var previewImage = (System.Windows.Controls.Image)previewWindow.FindName("PreviewImage");
                var previewTitle = (TextBlock)previewWindow.FindName("PreviewTitle");

                var bi = new BitmapImage();
                bi.BeginInit();
                bi.CacheOption = BitmapCacheOption.OnLoad;
                bi.UriSource = new Uri(filePath, UriKind.Absolute);
                bi.EndInit();
                bi.Freeze();

                previewImage.Source = bi;
                if (previewTitle != null)
                {
                    previewTitle.Text = Path.GetFileName(filePath);
                }

                previewWindow.Show(); // Modeless
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    string.Format("プレビューの表示に失敗しました:\n{0}", ex.Message),
                    "PowerShot", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        // --- Helpers ---

        private static string NormalizePath(string path)
        {
            return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        private static BitmapSource ConvertBitmapToImageSource(Bitmap bitmap)
        {
            IntPtr hBitmap = bitmap.GetHbitmap();
            try
            {
                var source = Imaging.CreateBitmapSourceFromHBitmap(
                    hBitmap, IntPtr.Zero, Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());
                source.Freeze();
                return source;
            }
            finally
            {
                DeleteObject(hBitmap);
            }
        }

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);
    }

    // ============================================================
    // Clipboard Monitor — hooks WM_CLIPBOARDUPDATE via HwndSource
    // ============================================================
    public class ClipboardWatcher : IDisposable
    {
        private HwndSource _hwndSource;

        private string _lastImageHash;
        private bool _isWindowOpen;

        private AppSettings _settings;
        private string _scriptPath;
        private SessionState _session;

        public ClipboardWatcher(string scriptPath, AppSettings settings, SessionState session)
        {
            _scriptPath = scriptPath;
            _settings = settings;
            _session = session;
            _isWindowOpen = false;

            string saveDir = Path.GetFullPath(Path.Combine(_scriptPath, _settings.SaveFolder));
            // Ensure Screenshots directory exists
            if (!Directory.Exists(saveDir))
            {
                Directory.CreateDirectory(saveDir);
            }
        }

        public void Start()
        {
            // Create a hidden window for clipboard message hooking
            var parameters = new HwndSourceParameters("PowerShotClipboardHook")
            {
                Width = 0,
                Height = 0,
                WindowStyle = 0 // invisible
            };
            _hwndSource = new HwndSource(parameters);
            _hwndSource.AddHook(WndProc);
            NativeMethods.AddClipboardFormatListener(_hwndSource.Handle);
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == NativeMethods.WM_CLIPBOARDUPDATE)
            {
                handled = true;
                ProcessClipboard();
            }
            return IntPtr.Zero;
        }

        private void ProcessClipboard()
        {
            if (_isWindowOpen) return;

            try
            {
                var dataObj = System.Windows.Clipboard.GetDataObject();
                if (dataObj == null) return;

                // --- Excel filter: if clipboard contains Excel-specific formats, ignore entirely ---
                var formats = dataObj.GetFormats();
                if (formats != null)
                {
                    foreach (var fmt in formats)
                    {
                        if (fmt.IndexOf("XML Spreadsheet", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            fmt.Equals("HTML Format", StringComparison.OrdinalIgnoreCase))
                        {
                            return; // Excel cell copy detected — ignore
                        }
                    }
                }

                // Check for image data
                if (!System.Windows.Clipboard.ContainsImage()) return;

                var bitmapSource = System.Windows.Clipboard.GetImage();
                if (bitmapSource == null) return;

                // Convert to System.Drawing.Bitmap for processing
                Bitmap bitmap = BitmapSourceToBitmap(bitmapSource);
                if (bitmap == null) return;

                // --- Small image filter ---
                if (bitmap.Width < 20 && bitmap.Height < 20)
                {
                    bitmap.Dispose();
                    return;
                }

                // --- Duplicate hash check ---
                string hash = ComputeImageHash(bitmap);
                if (hash == _lastImageHash)
                {
                    bitmap.Dispose();
                    return;
                }
                _lastImageHash = hash;

                // Fire event to show UI
                ShowMainWindow(bitmap);
            }
            catch { }
        }

        private void ShowMainWindow(Bitmap bitmap)
        {
            _isWindowOpen = true;

            try
            {
                string xamlPath = Path.Combine(_scriptPath, "MainWindow.xaml");
                if (!File.Exists(xamlPath))
                {
                    MessageBox.Show("MainWindow.xaml が見つかりません。",
                        "PowerShot", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                Window mainWindow;
                using (var fs = new FileStream(xamlPath, FileMode.Open, FileAccess.Read))
                {
                    mainWindow = (Window)XamlReader.Load(fs);
                }

                var controller = new MainWindowController(mainWindow, bitmap, _scriptPath, _settings, _session);

                mainWindow.Closed += (s, e) =>
                {
                    _isWindowOpen = false;
                    // Clear clipboard after save to avoid re-triggering
                    if (controller.Saved)
                    {
                        try { System.Windows.Clipboard.Clear(); } catch { }
                        _lastImageHash = null;
                    }
                };

                mainWindow.Show();
            }
            catch (Exception ex)
            {
                _isWindowOpen = false;
                MessageBox.Show(
                    string.Format("UIの表示に失敗しました:\n{0}", ex.Message),
                    "PowerShot", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static string ComputeImageHash(Bitmap bitmap)
        {
            using (var ms = new MemoryStream())
            using (var sha256 = SHA256.Create())
            {
                bitmap.Save(ms, ImageFormat.Bmp);
                byte[] hashBytes = sha256.ComputeHash(ms.ToArray());
                return BitConverter.ToString(hashBytes).Replace("-", "");
            }
        }

        private static Bitmap BitmapSourceToBitmap(BitmapSource source)
        {
            try
            {
                int width = source.PixelWidth;
                int height = source.PixelHeight;
                int stride = width * ((source.Format.BitsPerPixel + 7) / 8);

                // Ensure Bgra32 format
                FormatConvertedBitmap converted = new FormatConvertedBitmap();
                converted.BeginInit();
                converted.Source = source;
                converted.DestinationFormat = System.Windows.Media.PixelFormats.Bgra32;
                converted.EndInit();

                stride = width * 4;
                byte[] pixels = new byte[height * stride];
                converted.CopyPixels(pixels, stride, 0);

                var bitmap = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                var bmpData = bitmap.LockBits(
                    new Rectangle(0, 0, width, height),
                    ImageLockMode.WriteOnly,
                    System.Drawing.Imaging.PixelFormat.Format32bppArgb);

                Marshal.Copy(pixels, 0, bmpData.Scan0, pixels.Length);
                bitmap.UnlockBits(bmpData);
                return bitmap;
            }
            catch
            {
                return null;
            }
        }

        public void Dispose()
        {
            if (_hwndSource != null)
            {
                NativeMethods.RemoveClipboardFormatListener(_hwndSource.Handle);
                _hwndSource.RemoveHook(WndProc);
                _hwndSource.Dispose();
                _hwndSource = null;
            }
        }
    }

    // ============================================================
    // Program — Entry Point (called from PowerShell)
    // ============================================================
    public static class Program
    {
        private static SessionState _session = new SessionState();

        public static void Run(string scriptPath)
        {
            NativeMethods.SetProcessDPIAware();

            string settingsPath = Path.Combine(scriptPath, "settings.json");
            var settings = SettingsManager.Load(settingsPath);
            string saveDir = Path.GetFullPath(Path.Combine(scriptPath, settings.SaveFolder));

            Console.WriteLine("-----------------------------------------");
            Console.WriteLine("  >_ PowerShot v2.0");
            Console.WriteLine("-----------------------------------------");
            Console.WriteLine("  クリップボードの監視を開始しました。");
            Console.WriteLine("  スクリーンショットをコピーすると自動でUIが表示されます。");
            Console.WriteLine("  終了するにはこのウィンドウを閉じてください。");
            Console.WriteLine("-----------------------------------------");

            // Ensure save directory exists
            if (!Directory.Exists(saveDir))
            {
                Directory.CreateDirectory(saveDir);
                Console.WriteLine("  Screenshots フォルダを作成しました。");
            }

            var app = new Application();
            app.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            var watcher = new ClipboardWatcher(scriptPath, settings, _session);
            watcher.Start();

            // Handle console close
            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true;
                watcher.Dispose();
                Environment.Exit(0);
            };

            app.Run();

            watcher.Dispose();
            Console.WriteLine("\nPowerShotを終了しました。");
        }
    }
}
