using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media.Imaging;
using PowerShot.Core;
using PowerShot.Models;

namespace PowerShot.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private Window _window;
        private Bitmap _capturedBitmap;
        private string _scriptDir;
        private string _rootBoundary;
        private SessionState _session;

        public bool Saved { get; private set; }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        // --- UI Bindings ---

        private BitmapSource _previewImageSource;
        public BitmapSource PreviewImageSource
        {
            get => _previewImageSource;
            set { _previewImageSource = value; OnPropertyChanged(nameof(PreviewImageSource)); }
        }

        private ObservableCollection<ExplorerItem> _explorerItems;
        public ObservableCollection<ExplorerItem> ExplorerItems
        {
            get => _explorerItems;
            set { _explorerItems = value; OnPropertyChanged(nameof(ExplorerItems)); }
        }

        private ExplorerItem _selectedExplorerItem;
        public ExplorerItem SelectedExplorerItem
        {
            get => _selectedExplorerItem;
            set
            {
                _selectedExplorerItem = value;
                OnPropertyChanged(nameof(SelectedExplorerItem));
                DeleteCommand.RaiseCanExecuteChanged();
                ParseAndPopulateFromSelectedItem();
            }
        }

        private string _pathDisplay;
        public string PathDisplay
        {
            get => _pathDisplay;
            set { _pathDisplay = value; OnPropertyChanged(nameof(PathDisplay)); }
        }

        private string _prefix;
        public string Prefix
        {
            get => _prefix;
            set
            {
                if (_prefix != value)
                {
                    _prefix = value;
                    OnPropertyChanged(nameof(Prefix));
                    UpdateSequenceAndPreview();
                }
            }
        }

        private string _sequence;
        public string Sequence
        {
            get => _sequence;
            set
            {
                if (_sequence != value)
                {
                    _sequence = value;
                    OnPropertyChanged(nameof(Sequence));
                    UpdateFileNamePreview();
                }
            }
        }

        private int _sequenceMaxLength;
        public int SequenceMaxLength
        {
            get => _sequenceMaxLength;
            set { _sequenceMaxLength = value; OnPropertyChanged(nameof(SequenceMaxLength)); }
        }

        private int _selectedDigitsIndex;
        public int SelectedDigitsIndex
        {
            get => _selectedDigitsIndex;
            set
            {
                if (_selectedDigitsIndex != value)
                {
                    _selectedDigitsIndex = value;
                    OnPropertyChanged(nameof(SelectedDigitsIndex));
                    UpdateSequenceMaxLength();
                    UpdateSequenceAndPreview();
                }
            }
        }

        private int _selectedFormatIndex;
        public int SelectedFormatIndex
        {
            get => _selectedFormatIndex;
            set
            {
                if (_selectedFormatIndex != value)
                {
                    _selectedFormatIndex = value;
                    OnPropertyChanged(nameof(SelectedFormatIndex));
                    UpdateFileNamePreview();
                }
            }
        }

        private string _fileNamePreview;
        public string FileNamePreview
        {
            get => _fileNamePreview;
            set { _fileNamePreview = value; OnPropertyChanged(nameof(FileNamePreview)); }
        }

        private bool _isSystemInfoVisible;
        public bool IsSystemInfoVisible
        {
            get => _isSystemInfoVisible;
            set
            {
                _isSystemInfoVisible = value;
                OnPropertyChanged(nameof(IsSystemInfoVisible));
                _session.IsSystemInfoVisible = value;
            }
        }

        private string _currentDirectory;

        // Folders
        private Visibility _newFolderPanelVisibility = Visibility.Collapsed;
        public Visibility NewFolderPanelVisibility
        {
            get => _newFolderPanelVisibility;
            set { _newFolderPanelVisibility = value; OnPropertyChanged(nameof(NewFolderPanelVisibility)); }
        }

        private string _newFolderName;
        public string NewFolderName
        {
            get => _newFolderName;
            set { _newFolderName = value; OnPropertyChanged(nameof(NewFolderName)); }
        }

        // Commands
        public ICommand BackCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand CloseCommand { get; }
        public ICommand DragMoveCommand { get; }
        public RelayCommand DeleteCommand { get; }
        public ICommand ShowNewFolderPanelCommand { get; }
        public ICommand HideNewFolderPanelCommand { get; }
        public ICommand CreateFolderCommand { get; }
        public ICommand ItemDoubleClickCommand { get; }

        private bool _suppressSequenceUpdate = false;

        public MainViewModel(Window window, Bitmap capturedBitmap, string scriptDir, string rootBoundary, SessionState session)
        {
            _window = window;
            _capturedBitmap = capturedBitmap;
            _scriptDir = scriptDir;
            _rootBoundary = NormalizePath(rootBoundary);
            _session = session;
            Saved = false;

            // Setup Commands
            BackCommand = new RelayCommand(_ => Back());
            SaveCommand = new RelayCommand(_ => Save());
            CloseCommand = new RelayCommand(_ => _window.Close());
            DragMoveCommand = new RelayCommand(_ => _window.DragMove());
            DeleteCommand = new RelayCommand(_ => DeleteSelectedItems(), _ => SelectedExplorerItem != null);

            ShowNewFolderPanelCommand = new RelayCommand(_ =>
            {
                NewFolderPanelVisibility = Visibility.Visible;
                NewFolderName = "新しいフォルダー";
            });
            HideNewFolderPanelCommand = new RelayCommand(_ => NewFolderPanelVisibility = Visibility.Collapsed);
            CreateFolderCommand = new RelayCommand(_ => CreateFolder());
            ItemDoubleClickCommand = new RelayCommand(obj =>
            {
                if (obj is ExplorerItem item)
                    HandleItemDoubleClick(item);
            });

            // Init Image
            if (_capturedBitmap != null)
            {
                PreviewImageSource = ConvertBitmapToImageSource(_capturedBitmap);
            }

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

            InitializeState();
        }

        private void InitializeState()
        {
            _suppressSequenceUpdate = true;

            Prefix = _session.LastPrefix ?? "";

            if (_session.LastSequenceDigits == -1)
                SelectedDigitsIndex = 6;
            else if (_session.LastSequenceDigits >= 1 && _session.LastSequenceDigits <= 6)
                SelectedDigitsIndex = _session.LastSequenceDigits - 1;
            else
                SelectedDigitsIndex = 3; // Default 4 digits

            UpdateSequenceMaxLength();

            IsSystemInfoVisible = _session.IsSystemInfoVisible;
            SelectedFormatIndex = (_session.LastFormat == "png") ? 1 : 0;

            _suppressSequenceUpdate = false;

            NavigateToDirectory(_currentDirectory);
        }

        private void UpdateSequenceMaxLength()
        {
            int digits = GetSelectedDigits();
            SequenceMaxLength = (digits > 0) ? digits : 20;
        }

        private void NavigateToDirectory(string path)
        {
            string normalizedTarget = NormalizePath(path);
            if (!normalizedTarget.StartsWith(_rootBoundary, StringComparison.OrdinalIgnoreCase))
                return; // Block access above root boundary

            if (!Directory.Exists(path))
                return;

            _currentDirectory = path;
            _session.LastDirectory = path;

            // Update path display (relative to root)
            string root = NormalizePath(_rootBoundary).TrimEnd('\\');
            string current = NormalizePath(_currentDirectory).TrimEnd('\\');

            if (current.Equals(root, StringComparison.OrdinalIgnoreCase))
            {
                PathDisplay = @".\";
            }
            else if (current.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                string relative = current.Substring(root.Length).TrimStart('\\', '/');
                PathDisplay = string.IsNullOrEmpty(relative) ? @".\" : @".\" + relative;
            }
            else
            {
                PathDisplay = Path.GetFileName(current);
            }

            RefreshExplorer();
            UpdateSequenceAndPreview();
        }

        private void RefreshExplorer()
        {
            var items = new ObservableCollection<ExplorerItem>();
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

                ExplorerItems = items;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"フォルダの読み込みに失敗しました:\n{ex.Message}", "PowerShot", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void UpdateSequenceAndPreview()
        {
            if (_suppressSequenceUpdate) return;

            int digits = GetSelectedDigits();
            if (digits == -1) // "日時" case
            {
                Sequence = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            }
            else
            {
                int maxLen = digits > 0 ? digits : 4;

                // Use cached ExplorerItems to find max sequence to avoid Directory.GetFiles() overhead
                var fileNames = ExplorerItems?.Where(x => !x.IsDirectory).Select(x => x.Name) ?? new List<string>();
                int seq = SequenceManager.GetNextSequenceFromList(fileNames, Prefix, "");

                Sequence = seq.ToString("D" + maxLen);
            }

            UpdateFileNamePreview();
        }

        private void UpdateFileNamePreview()
        {
            string format = GetSelectedFormat();
            FileNamePreview = FileManager.GenerateFileName(Prefix, "", Sequence, format);
        }

        private int GetSelectedDigits()
        {
            if (SelectedDigitsIndex == 6) return -1;
            return SelectedDigitsIndex + 1;
        }

        private string GetSelectedFormat()
        {
            if (SelectedFormatIndex == 1) return "png";
            return "jpg";
        }

        private void Back()
        {
            string parent = Path.GetDirectoryName(_currentDirectory);
            if (!string.IsNullOrEmpty(parent))
            {
                NavigateToDirectory(parent);
            }
        }

        private void ParseAndPopulateFromSelectedItem()
        {
            if (SelectedExplorerItem == null || SelectedExplorerItem.IsDirectory) return;

            string fileName = SelectedExplorerItem.Name;
            string nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);

            // Patterns for timestamp: yyyyMMdd-HHmmss
            var matchTimestamp = Regex.Match(nameWithoutExt, @"^(.+?)_(\d{8}-\d{6})$");
            var matchTimestampOnly = Regex.Match(nameWithoutExt, @"^(\d{8}-\d{6})$");
            // Pattern for sequence: prefix_NNN
            var matchSimple = Regex.Match(nameWithoutExt, @"^(.+?)_(\d+)$");

            _suppressSequenceUpdate = true;

            if (matchTimestamp.Success)
            {
                Prefix = matchTimestamp.Groups[1].Value;
                SelectedDigitsIndex = 6; // 日時
            }
            else if (matchTimestampOnly.Success)
            {
                Prefix = "";
                SelectedDigitsIndex = 6; // 日時
            }
            else if (matchSimple.Success)
            {
                Prefix = matchSimple.Groups[1].Value;
                int len = matchSimple.Groups[2].Length;
                if (len >= 1 && len <= 6)
                    SelectedDigitsIndex = len - 1;
            }
            else
            {
                Prefix = nameWithoutExt;
            }

            _suppressSequenceUpdate = false;
            UpdateSequenceAndPreview();
        }

        private void HandleItemDoubleClick(ExplorerItem item)
        {
            if (item.IsDirectory)
            {
                NavigateToDirectory(item.FullPath);
            }
            else
            {
                OpenPreviewWindow(item.FullPath);
            }
        }

        private void CreateFolder()
        {
            string folderName = NewFolderName?.Trim();
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

            NewFolderPanelVisibility = Visibility.Collapsed;
        }

        private void DeleteSelectedItems()
        {
            var item = SelectedExplorerItem;
            if (item == null) return;

            string msg = $"「{item.Name}」を完全に削除してもよろしいですか？\nこの操作は元に戻せません。";
            var result = MessageBox.Show(msg, "PowerShot - 削除の確認", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    if (item.IsDirectory)
                    {
                        Directory.Delete(item.FullPath, true);
                    }
                    else
                    {
                        File.Delete(item.FullPath);
                    }
                    RefreshExplorer();
                    UpdateSequenceAndPreview();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("削除に失敗しました:\n" + ex.Message, "PowerShot", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void Save()
        {
            if (_capturedBitmap == null)
            {
                MessageBox.Show("保存する画像がありません。", "PowerShot", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string prefixError = FileManager.ValidateName(Prefix);
            if (prefixError != null)
            {
                MessageBox.Show(prefixError, "PowerShot - 入力エラー", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (IsSystemInfoVisible)
            {
                EmbedSystemInfo(_capturedBitmap);
            }

            string format = GetSelectedFormat();
            string fileName = FileManager.GenerateFileName(Prefix, "", Sequence, format);

            string error = FileManager.SaveImage(_capturedBitmap, _currentDirectory, fileName, format, 80L);
            if (error != null)
            {
                MessageBox.Show(error, "PowerShot - 保存エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            Saved = true;

            // Update session state
            _session.LastPrefix = Prefix;
            _session.LastDirectory = _currentDirectory;
            _session.LastSequenceDigits = GetSelectedDigits();
            _session.LastFormat = GetSelectedFormat();

            _window.Close();
        }

        private void EmbedSystemInfo(Bitmap bmp)
        {
            string info = GetSystemInfoString();
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

                using (Font font = new Font("Segoe UI", 12, System.Drawing.FontStyle.Regular))
                {
                    SizeF textSize = g.MeasureString(info, font);
                    float padding = 8;
                    float rectW = textSize.Width + padding * 2;
                    float rectH = textSize.Height + padding * 2;
                    float x = 10;
                    float y = bmp.Height - rectH - 10;

                    if (x < 0) x = 0;
                    if (y < 0) y = 0;

                    using (System.Drawing.SolidBrush brush = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(160, 0, 0, 0)))
                    {
                        g.FillRectangle(brush, x, y, rectW, rectH);
                    }

                    using (System.Drawing.SolidBrush textBrush = new System.Drawing.SolidBrush(System.Drawing.Color.White))
                    {
                        g.DrawString(info, font, textBrush, x + padding, y + padding);
                    }
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

        private void OpenPreviewWindow(string filePath)
        {
            string ext = Path.GetExtension(filePath).ToLowerInvariant();
            if (ext != ".png" && ext != ".jpg" && ext != ".jpeg" && ext != ".bmp" && ext != ".gif")
                return;

            try
            {
                string xamlPath = Path.Combine(_scriptDir, "PreviewWindow.xaml");

                if (!File.Exists(xamlPath))
                {
                    MessageBox.Show("PreviewWindow.xaml が見つかりません。", "PowerShot", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                var previewTitle = (System.Windows.Controls.TextBlock)previewWindow.FindName("PreviewTitle");

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

                previewWindow.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"プレビューの表示に失敗しました:\n{ex.Message}", "PowerShot", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
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
