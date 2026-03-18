using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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

namespace PowerShot
{
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

        public SessionState()
        {
            LastDirectory = "";
            LastPrefix = "";
            LastSequenceDigits = 3;
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

            foreach (char c in ForbiddenChars)
            {
                if (name.IndexOf(c) >= 0)
                {
                    return string.Format("ファイル名に使用できない文字が含まれています: '{0}'\n禁則文字: \\ / : * ? \" < > |", c);
                }
            }

            foreach (char c in Path.GetInvalidFileNameChars())
            {
                if (name.IndexOf(c) >= 0)
                {
                    return string.Format("ファイル名に使用できない文字が含まれています: (Code: {0})", (int)c);
                }
            }

            return null;
        }

        /// <summary>
        /// Generates the filename based on prefix, optionName, sequence, and format.
        /// </summary>
        public static string GenerateFileName(string prefix, string optionName, string seqStr, string format)
        {
            string ext = format.ToLowerInvariant();

            if (string.IsNullOrWhiteSpace(prefix) && string.IsNullOrWhiteSpace(optionName))
            {
                return string.Format("SS_{0}.{1}", DateTime.Now.ToString("yyyyMMdd-HHmmss"), ext);
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
        private Button _newFolderButton;
        private Button _deleteButton;
        private Grid _titleBarGrid;
        private Button _closeToolButton;

        public bool Saved { get; private set; }

        public MainWindowController(Window window, Bitmap capturedBitmap, string scriptDir, string rootBoundary, SessionState session)
        {
            _window = window;
            _capturedBitmap = capturedBitmap;
            _scriptDir = scriptDir;
            _rootBoundary = NormalizePath(rootBoundary);
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
                    _currentDirectory = rootBoundary;
                }
            }
            else
            {
                _currentDirectory = rootBoundary;
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
            _formatComboBox = (ComboBox)_window.FindName("FormatComboBox");
            _fileNamePreview = (TextBlock)_window.FindName("FileNamePreview");
            _saveButton = (Button)_window.FindName("SaveButton");
            _newFolderPanel = (Border)_window.FindName("NewFolderPanel");
            _digitsComboBox = (ComboBox)_window.FindName("DigitsComboBox");
            _newFolderNameTextBox = (TextBox)_window.FindName("NewFolderNameTextBox");
            _createFolderButton = (Button)_window.FindName("CreateFolderButton");
            _cancelFolderButton = (Button)_window.FindName("CancelFolderButton");
            _newFolderButton = (Button)_window.FindName("NewFolderButton");
            _deleteButton = (Button)_window.FindName("DeleteButton");
            _titleBarGrid = (Grid)_window.FindName("TitleBarGrid");
            _closeToolButton = (Button)_window.FindName("CloseToolButton");
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
        }

        private void Initialize()
        {
            // Set preview image
            if (_capturedBitmap != null)
            {
                _previewImage.Source = ConvertBitmapToImageSource(_capturedBitmap);
            }

            // Restore session state
            _suppressSequenceUpdate = true;
            _prefixTextBox.Text = _session.LastPrefix ?? "";
            
            if (_digitsComboBox != null && _session.LastSequenceDigits >= 1 && _session.LastSequenceDigits <= 6)
            {
                if (_digitsComboBox != null) _digitsComboBox.SelectedIndex = _session.LastSequenceDigits - 1;

                // Ensure interlock is correct after loading session data
                UpdateInputInterlock();

                UpdateSequence();
            }
            
            _suppressSequenceUpdate = false;

            // Load directory and compute sequence
            NavigateToDirectory(_currentDirectory);
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
            try
            {
                Uri rootUri = new Uri(_rootBoundary + Path.DirectorySeparatorChar);
                Uri currentUri = new Uri(_currentDirectory + Path.DirectorySeparatorChar);
                string relative = Uri.UnescapeDataString(rootUri.MakeRelativeUri(currentUri).ToString())
                                      .Replace('/', Path.DirectorySeparatorChar)
                                      .TrimEnd(Path.DirectorySeparatorChar);
                _pathDisplay.Text = string.IsNullOrEmpty(relative) ? "Screenshots" : "Screenshots\\" + relative;
            }
            catch
            {
                _pathDisplay.Text = "Screenshots";
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
                // Folders first (alphabetical)
                var dirs = Directory.GetDirectories(_currentDirectory);
                Array.Sort(dirs, StringComparer.OrdinalIgnoreCase);
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

                // Files sorted by modification date descending
                var fileInfos = new DirectoryInfo(_currentDirectory).GetFiles();
                var sortedFiles = fileInfos.OrderByDescending(f => f.LastWriteTime).ToArray();
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
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    string.Format("フォルダの読み込みに失敗しました:\n{0}", ex.Message),
                    "PowerShot", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            _explorerListView.ItemsSource = items;
        }

        // --- Sequence ---

        private void UpdateSequence()
        {
            int digits = GetSelectedDigits();
            if (_sequenceTextBox != null) _sequenceTextBox.MaxLength = digits;
            
            int seq = SequenceManager.GetNextSequence(
                _currentDirectory, _prefixTextBox.Text, "");
            _sequenceTextBox.Text = seq.ToString("D" + digits);
        }

        private int GetSelectedDigits()
        {
            if (_digitsComboBox != null && _digitsComboBox.SelectedIndex >= 0)
            {
                return _digitsComboBox.SelectedIndex + 1;
            }
            return 3;
        }

        // --- Filename Preview ---

        private void UpdateFileNamePreview()
        {
            string format = GetSelectedFormat();
            _fileNamePreview.Text = FileManager.GenerateFileName(
                _prefixTextBox.Text, "", _sequenceTextBox.Text, format);
        }

        private string GetSelectedFormat()
        {
            if (_formatComboBox.SelectedIndex == 1) return "png";
            return "jpg";
        }

        // --- Event Handlers ---

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
            // Match pattern: prefix_NNN
            var matchSimple = Regex.Match(nameWithoutExt, @"^(.+?)_(\d{3,})$");

            _suppressSequenceUpdate = true;

            if (matchSimple.Success)
            {
                _prefixTextBox.Text = matchSimple.Groups[1].Value;
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
            if (_formatComboBox != null) _formatComboBox.IsEnabled = hasPrefix;
            if (_saveButton != null) _saveButton.IsEnabled = hasPrefix;
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
                _sequenceTextBox.MaxLength = GetSelectedDigits();
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
                _prefixTextBox.Text, "", _sequenceTextBox.Text, format);

            string error = FileManager.SaveImage(_capturedBitmap, _currentDirectory, fileName, format, 80L);
            if (error != null)
            {
                MessageBox.Show(error, "PowerShot - 保存エラー",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            Saved = true;

            // Update session state
            _session.LastPrefix = _prefixTextBox.Text;
            _session.LastDirectory = _currentDirectory;
            _session.LastSequenceDigits = GetSelectedDigits();

            // Close the window to wait for the next screenshot
            _window.Close();
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

        private string _rootBoundary;
        private string _scriptPath;
        private SessionState _session;

        public ClipboardWatcher(string scriptPath, string saveDir, SessionState session)
        {
            _scriptPath = scriptPath;
            _rootBoundary = saveDir;
            _session = session;
            _isWindowOpen = false;

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

                var controller = new MainWindowController(mainWindow, bitmap, _scriptPath, _rootBoundary, _session);

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

        public static void Run(string scriptPath, string saveDir)
        {
            NativeMethods.SetProcessDPIAware();

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

            var watcher = new ClipboardWatcher(scriptPath, saveDir, _session);
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
