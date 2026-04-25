using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
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
    public class MainWindowController
    {
        private Window _window;
        private Bitmap _capturedBitmap;
        private string _rootBoundary;
        private string _scriptDir;
        private string _settingsPath;
        private string _currentDirectory;
        private SessionState _session;
        private AppSettings _settings;
        private CropController _cropController;
        private DispatcherTimer _previewRedrawTimer;
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
            _settingsPath = Path.Combine(scriptDir, "settings.json");
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
            if (_cropEnableCheckBox != null) _cropEnableCheckBox.Click += CropSettings_Changed;
            if (_resetCropButton != null) _resetCropButton.Click += ResetCropButton_Click;

            if (_overlayEnableCheckBox != null) _overlayEnableCheckBox.Click += OverlaySettings_Changed;
            if (_embedSysInfoCheckBox != null) _embedSysInfoCheckBox.Click += OverlaySettings_Changed;
            if (_sysInfoPositionComboBox != null) _sysInfoPositionComboBox.SelectionChanged += OverlaySettings_Changed;
            if (_overlayTextPositionComboBox != null) _overlayTextPositionComboBox.SelectionChanged += OverlaySettings_Changed;
            
            if (_updatePreviewButton != null) _updatePreviewButton.Click += UpdatePreviewButton_Click;
        }

        private void Initialize()
        {
            // Initialize crop sub-controller (canvas dims + drag wiring + initial sync)
            _cropController = new CropController(
                _cropCanvas, _cropSelectionRect,
                _cropXTextBox, _cropYTextBox, _cropWidthTextBox, _cropHeightTextBox,
                _capturedBitmap, _settings, OnCropChanged);
            _cropController.Initialize();

            if (_capturedBitmap != null)
            {
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

                UpdateInputInterlock();
                UpdateSequence();
            }

            if (_settings != null)
            {
                if (_cropEnableCheckBox != null) _cropEnableCheckBox.IsChecked = _settings.CropEnabled;
                _cropController.SetActive(_settings.CropEnabled);

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
            string xamlPath = Path.Combine(_scriptDir, "Views", "SettingsWindow.xaml");
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
            
            int currentDigits = GetSelectedDigits();
            var controller = new SettingsWindowController(settingsWindow, _settings, _settingsPath, currentDigits);
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
                    Rectangle cropRect = ClampCropRect(
                        _settings.CropX, _settings.CropY,
                        _settings.CropWidth, _settings.CropHeight,
                        clonedBmp.Width, clonedBmp.Height);
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

        private void OnCropChanged()
        {
            SaveSettings();
            RedrawPreviewDebounced();
        }

        private void CropSettings_Changed(object sender, RoutedEventArgs e)
        {
            if (_suppressSequenceUpdate || _settings == null) return;
            if (_cropEnableCheckBox != null)
            {
                _settings.CropEnabled = _cropEnableCheckBox.IsChecked ?? false;
                if (_cropController != null) _cropController.SetActive(_settings.CropEnabled);
            }
            SaveSettings();
            RedrawPreview();
        }

        private void ResetCropButton_Click(object sender, RoutedEventArgs e)
        {
            if (_cropController != null) _cropController.Reset();
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

            SaveSettings();
        }

        private void UpdatePreviewButton_Click(object sender, RoutedEventArgs e)
        {
            // Sync text before drawing
            if (_overlayTextBox != null && _settings != null)
            {
                _settings.OverlayText = _overlayTextBox.Text;
                SaveSettings();
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

        // Coalesces bursts of redraw requests (e.g. crop textbox typing) into a single render.
        // Why: cloning a 4K bitmap + GDI roundtrip per keystroke is wasteful.
        private void RedrawPreviewDebounced()
        {
            if (_previewRedrawTimer == null)
            {
                _previewRedrawTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
                _previewRedrawTimer.Tick += (s, e) => { _previewRedrawTimer.Stop(); RedrawPreview(); };
            }
            _previewRedrawTimer.Stop();
            _previewRedrawTimer.Start();
        }

        private void ApplyOverlays(Bitmap bmp, bool isPreview = false)
        {
            if (_settings == null || !_settings.OverlayEnabled) return;

            Rectangle bounds = (isPreview && _settings.CropEnabled)
                ? ClampCropRect(_settings.CropX, _settings.CropY, _settings.CropWidth, _settings.CropHeight, bmp.Width, bmp.Height)
                : new Rectangle(0, 0, bmp.Width, bmp.Height);

            OverlayRenderer.Apply(bmp, _settings, bounds);
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
            if (_previewRedrawTimer != null) _previewRedrawTimer.Stop();
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
                string xamlPath = Path.Combine(_scriptDir, "Views", "PreviewWindow.xaml");

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

        private void SaveSettings()
        {
            SettingsManager.Save(_settingsPath, _settings);
        }

        private static Rectangle ClampCropRect(int x, int y, int w, int h, int srcW, int srcH)
        {
            const int MinSize = 10;
            if (x < 0) x = 0;
            if (y < 0) y = 0;
            if (x > srcW - MinSize) x = srcW - MinSize;
            if (y > srcH - MinSize) y = srcH - MinSize;
            if (w < MinSize) w = MinSize;
            if (h < MinSize) h = MinSize;
            if (x + w > srcW) w = srcW - x;
            if (y + h > srcH) h = srcH - y;
            return new Rectangle(x, y, w, h);
        }

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
}

