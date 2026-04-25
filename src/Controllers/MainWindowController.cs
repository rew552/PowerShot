using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace PowerShot
{
    public class MainWindowController
    {
        private readonly Window _window;
        private readonly Bitmap _capturedBitmap;
        private readonly string _scriptDir;
        private readonly string _settingsPath;
        private readonly AppSettings _settings;
        private readonly SessionState _session;

        private string _rootBoundary;
        private string _currentDirectory;

        private CropController _cropController;
        private OverlayController _overlayController;
        private FileNameComposer _fileNameComposer;
        private DispatcherTimer _previewRedrawTimer;

        // Window chrome
        private Grid _titleBarGrid;
        private Button _closeToolButton;
        private Button _settingsToolButton;

        // Preview surface
        private System.Windows.Controls.Image _previewImage;

        // Explorer
        private ListView _explorerListView;
        private Button _backButton;
        private TextBlock _pathDisplay;
        private Button _newFolderButton;
        private Button _deleteButton;
        private Border _newFolderPanel;
        private TextBox _newFolderNameTextBox;
        private Button _createFolderButton;
        private Button _cancelFolderButton;

        // Save
        private Button _saveButton;

        public bool Saved { get; private set; }

        public MainWindowController(Window window, Bitmap capturedBitmap, string scriptDir, AppSettings settings, SessionState session)
        {
            _window = window;
            _capturedBitmap = capturedBitmap;
            _scriptDir = scriptDir;
            _settingsPath = Path.Combine(scriptDir, "settings.json");
            _settings = settings;
            _session = session;
            Saved = false;

            string projectRoot = Path.GetDirectoryName(scriptDir);
            string rootPath = Path.GetFullPath(Path.Combine(projectRoot, settings.SaveFolder));
            _rootBoundary = NormalizePath(rootPath);
            _currentDirectory = ResolveInitialDirectory(rootPath);

            FindControls();
            BindEvents();
            Initialize();
        }

        private string ResolveInitialDirectory(string rootPath)
        {
            if (!string.IsNullOrEmpty(_session.LastDirectory) && Directory.Exists(_session.LastDirectory))
            {
                string normLast = NormalizePath(_session.LastDirectory) + Path.DirectorySeparatorChar;
                string normRoot = _rootBoundary + Path.DirectorySeparatorChar;
                if (normLast.StartsWith(normRoot, StringComparison.OrdinalIgnoreCase))
                    return _session.LastDirectory;
            }
            return rootPath;
        }

        private void FindControls()
        {
            _previewImage = (System.Windows.Controls.Image)_window.FindName("PreviewImage");
            _titleBarGrid = (Grid)_window.FindName("TitleBarGrid");
            _closeToolButton = (Button)_window.FindName("CloseToolButton");
            _settingsToolButton = (Button)_window.FindName("SettingsToolButton");

            _explorerListView = (ListView)_window.FindName("ExplorerListView");
            _backButton = (Button)_window.FindName("BackButton");
            _pathDisplay = (TextBlock)_window.FindName("PathDisplay");
            _newFolderButton = (Button)_window.FindName("NewFolderButton");
            _deleteButton = (Button)_window.FindName("DeleteButton");
            _newFolderPanel = (Border)_window.FindName("NewFolderPanel");
            _newFolderNameTextBox = (TextBox)_window.FindName("NewFolderNameTextBox");
            _createFolderButton = (Button)_window.FindName("CreateFolderButton");
            _cancelFolderButton = (Button)_window.FindName("CancelFolderButton");

            _saveButton = (Button)_window.FindName("SaveButton");
        }

        private void BindEvents()
        {
            if (_titleBarGrid != null)
                _titleBarGrid.MouseDown += (s, e) => { if (e.ChangedButton == MouseButton.Left) _window.DragMove(); };
            if (_closeToolButton != null)
                _closeToolButton.Click += (s, e) => _window.Close();
            if (_settingsToolButton != null)
                _settingsToolButton.Click += SettingsToolButton_Click;

            _backButton.Click += BackButton_Click;
            _explorerListView.MouseDoubleClick += ExplorerListView_MouseDoubleClick;
            _explorerListView.SelectionChanged += ExplorerListView_SelectionChanged;
            _explorerListView.PreviewMouseLeftButtonUp += ExplorerListView_PreviewMouseLeftButtonUp;

            if (_newFolderButton != null) _newFolderButton.Click += NewFolderButton_Click;
            if (_createFolderButton != null) _createFolderButton.Click += CreateFolderButton_Click;
            if (_cancelFolderButton != null) _cancelFolderButton.Click += CancelFolderButton_Click;
            if (_newFolderNameTextBox != null) _newFolderNameTextBox.KeyDown += NewFolderNameTextBox_KeyDown;
            if (_deleteButton != null) _deleteButton.Click += DeleteButton_Click;

            _saveButton.Click += SaveButton_Click;

            _window.KeyDown += Window_KeyDown;
            _window.Closing += Window_Closing;
        }

        private void Initialize()
        {
            _cropController = new CropController(
                (Canvas)_window.FindName("CropCanvas"),
                (System.Windows.Shapes.Rectangle)_window.FindName("CropSelectionRect"),
                (TextBox)_window.FindName("CropXTextBox"),
                (TextBox)_window.FindName("CropYTextBox"),
                (TextBox)_window.FindName("CropWidthTextBox"),
                (TextBox)_window.FindName("CropHeightTextBox"),
                _capturedBitmap, _settings, OnCropChanged);
            _cropController.Initialize();

            CheckBox cropEnableBox = (CheckBox)_window.FindName("CropEnableCheckBox");
            Button resetCropButton = (Button)_window.FindName("ResetCropButton");
            if (cropEnableBox != null)
            {
                cropEnableBox.IsChecked = _settings.CropEnabled;
                cropEnableBox.Click += (s, e) => OnCropEnabledToggled(cropEnableBox.IsChecked ?? false);
            }
            _cropController.SetActive(_settings.CropEnabled);
            if (resetCropButton != null) resetCropButton.Click += (s, e) => _cropController.Reset();

            _overlayController = new OverlayController(
                (CheckBox)_window.FindName("OverlayEnableCheckBox"),
                (CheckBox)_window.FindName("EmbedSysInfoCheckBox"),
                (ComboBox)_window.FindName("SysInfoPositionComboBox"),
                (TextBox)_window.FindName("OverlayTextBox"),
                (ComboBox)_window.FindName("OverlayTextPositionComboBox"),
                (Button)_window.FindName("UpdatePreviewButton"),
                _settings,
                SaveSettings,
                RedrawPreview);
            _overlayController.Initialize();

            _fileNameComposer = new FileNameComposer(
                (TextBox)_window.FindName("PrefixTextBox"),
                (TextBox)_window.FindName("SequenceTextBox"),
                (ComboBox)_window.FindName("DigitsComboBox"),
                (ComboBox)_window.FindName("FormatComboBox"),
                (TextBlock)_window.FindName("FileNamePreview"),
                _settings, () => _currentDirectory);
            _fileNameComposer.Initialize(_session);

            if (_capturedBitmap != null) RedrawPreview();

            NavigateToDirectory(_currentDirectory);

            // Initial focus on prefix entry
            ((TextBox)_window.FindName("PrefixTextBox")).Focus();
        }

        // --- Navigation ---

        private void NavigateToDirectory(string path)
        {
            string normalizedTarget = NormalizePath(path) + Path.DirectorySeparatorChar;
            string normalizedRoot = _rootBoundary + Path.DirectorySeparatorChar;
            if (!normalizedTarget.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase)) return;
            if (!Directory.Exists(path)) return;

            _currentDirectory = path;
            _session.LastDirectory = path;
            UpdatePathDisplay();
            RefreshExplorer();
            _fileNameComposer.Refresh();
        }

        private void UpdatePathDisplay()
        {
            string root = _rootBoundary.TrimEnd('\\');
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
                _pathDisplay.Text = Path.GetFileName(current);
            }
        }

        private void RefreshExplorer()
        {
            var items = new List<ExplorerItem>();
            try
            {
                foreach (var dir in Directory.GetDirectories(_currentDirectory).OrderBy(d => d, StringComparer.OrdinalIgnoreCase))
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

                foreach (var fi in new DirectoryInfo(_currentDirectory).GetFiles().OrderBy(f => f.Name))
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
                    gv.Columns[0].Width = double.NaN;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    string.Format("フォルダの読み込みに失敗しました:\n{0}", ex.Message),
                    "PowerShot", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            string parent = Path.GetDirectoryName(_currentDirectory);
            if (!string.IsNullOrEmpty(parent)) NavigateToDirectory(parent);
        }

        private void ExplorerListView_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            var dep = (DependencyObject)e.OriginalSource;
            while (dep != null && !(dep is ListViewItem))
                dep = VisualTreeHelper.GetParent(dep);

            ListViewItem lvi = dep as ListViewItem;
            if (lvi == null) return;

            ExplorerItem item = lvi.Content as ExplorerItem;
            if (item != null && !item.IsDirectory)
                _fileNameComposer.ApplyFromFileName(item.Name);
        }

        private void ExplorerListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var item = _explorerListView.SelectedItem as ExplorerItem;
            if (item == null) return;

            if (item.IsDirectory)
                NavigateToDirectory(item.FullPath);
            else
                PreviewLauncher.Show(_scriptDir, _window, item.FullPath);
        }

        private void ExplorerListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            int count = _explorerListView.SelectedItems.Count;
            if (_deleteButton != null) _deleteButton.IsEnabled = count > 0;

            if (count != 1) return;
            var item = _explorerListView.SelectedItem as ExplorerItem;
            if (item == null || item.IsDirectory) return;

            _fileNameComposer.ApplyFromFileName(item.Name);
        }

        // --- Folder Management ---

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
            if (_newFolderPanel != null) _newFolderPanel.Visibility = Visibility.Collapsed;
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
                if (Directory.Exists(newPath))
                {
                    MessageBox.Show("同名のフォルダーが既に存在します。", "PowerShot", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    Directory.CreateDirectory(newPath);
                    RefreshExplorer();
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

            string msg = selectedItems.Count == 1
                ? string.Format("「{0}」を完全に削除してもよろしいですか？\nこの操作は元に戻せません。", selectedItems[0].Name)
                : string.Format("選択された {0} 個の項目を完全に削除してもよろしいですか？\nこの操作は元に戻せません。", selectedItems.Count);

            var result = MessageBox.Show(msg, "PowerShot - 削除の確認", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes) return;

            try
            {
                foreach (var item in selectedItems)
                {
                    if (item.IsDirectory) Directory.Delete(item.FullPath, true);
                    else File.Delete(item.FullPath);
                }
                RefreshExplorer();
            }
            catch (Exception ex)
            {
                MessageBox.Show("削除に失敗しました:\n" + ex.Message, "PowerShot", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // --- Save ---

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (_capturedBitmap == null)
            {
                MessageBox.Show("保存する画像がありません。", "PowerShot",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string prefixError = FileManager.ValidateName(_fileNameComposer.GetPrefix());
            if (prefixError != null)
            {
                MessageBox.Show(prefixError, "PowerShot - 入力エラー",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string format = _fileNameComposer.GetFormat();
            string fileName = _fileNameComposer.GetFileName();

            using (Bitmap clonedBmp = (Bitmap)_capturedBitmap.Clone())
            {
                Bitmap cropBmp = null;
                string error;
                try
                {
                    if (_settings.CropEnabled)
                    {
                        Rectangle cropRect = CropController.ClampRect(
                            _settings.CropX, _settings.CropY,
                            _settings.CropWidth, _settings.CropHeight,
                            clonedBmp.Width, clonedBmp.Height);
                        cropBmp = clonedBmp.Clone(cropRect, clonedBmp.PixelFormat);
                    }

                    Bitmap finalBmp = cropBmp ?? clonedBmp;
                    ApplyOverlays(finalBmp, false);
                    error = FileManager.SaveImage(finalBmp, _currentDirectory, fileName, format, _settings.JpegQuality);
                }
                finally
                {
                    if (cropBmp != null) cropBmp.Dispose();
                }

                if (error != null)
                {
                    MessageBox.Show(error, "PowerShot - 保存エラー",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }

            Saved = true;
            _fileNameComposer.WriteSessionState(_session);
            _session.LastDirectory = _currentDirectory;

            _window.Close();
        }

        // --- Settings dialog ---

        private void SettingsToolButton_Click(object sender, RoutedEventArgs e)
        {
            Window settingsWindow = XamlLoader.LoadWindow(_scriptDir, "SettingsWindow");
            if (settingsWindow == null)
            {
                MessageBox.Show("SettingsWindow.xaml が見つかりません。", "PowerShot", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            settingsWindow.Owner = _window;

            var controller = new SettingsWindowController(settingsWindow, _settings, _settingsPath, _fileNameComposer.GetDigits());
            if (!controller.ShowDialog()) return;

            string projectRoot = Path.GetDirectoryName(_scriptDir);
            string newRoot = Path.GetFullPath(Path.Combine(projectRoot, _settings.SaveFolder));
            _rootBoundary = NormalizePath(newRoot);

            if (!Directory.Exists(_currentDirectory) ||
                !(NormalizePath(_currentDirectory) + Path.DirectorySeparatorChar).StartsWith(_rootBoundary + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                NavigateToDirectory(newRoot);
            }
            else
            {
                _fileNameComposer.Refresh();
            }
        }

        // --- Crop / Overlay coordination ---

        private void OnCropEnabledToggled(bool enabled)
        {
            _settings.CropEnabled = enabled;
            _cropController.SetActive(enabled);
            SaveSettings();
            RedrawPreview();
        }

        private void OnCropChanged()
        {
            SaveSettings();
            RedrawPreviewDebounced();
        }

        // --- Preview rendering ---

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

        private void ApplyOverlays(Bitmap bmp, bool isPreview)
        {
            if (!_settings.OverlayEnabled) return;

            Rectangle bounds = (isPreview && _settings.CropEnabled)
                ? CropController.ClampRect(_settings.CropX, _settings.CropY, _settings.CropWidth, _settings.CropHeight, bmp.Width, bmp.Height)
                : new Rectangle(0, 0, bmp.Width, bmp.Height);

            OverlayRenderer.Apply(bmp, _settings, bounds);
        }

        // --- Window-level events ---

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                _window.Close();
            }
            else if (e.Key == Key.Delete)
            {
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
            _fileNameComposer.WriteSessionState(_session);
            _session.LastDirectory = _currentDirectory;
        }

        // --- Helpers ---

        private void SaveSettings()
        {
            SettingsManager.Save(_settingsPath, _settings);
        }

        private static string NormalizePath(string path)
        {
            return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        private static BitmapSource ConvertBitmapToImageSource(Bitmap bitmap)
        {
            var bmpData = bitmap.LockBits(
                new Rectangle(0, 0, bitmap.Width, bitmap.Height),
                System.Drawing.Imaging.ImageLockMode.ReadOnly,
                System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            try
            {
                int stride = Math.Abs(bmpData.Stride);
                byte[] pixels = new byte[bitmap.Height * stride];
                Marshal.Copy(bmpData.Scan0, pixels, 0, pixels.Length);
                var source = BitmapSource.Create(
                    bitmap.Width, bitmap.Height,
                    96, 96,
                    PixelFormats.Bgra32,
                    null, pixels, stride);
                source.Freeze();
                return source;
            }
            finally
            {
                bitmap.UnlockBits(bmpData);
            }
        }
    }
}
