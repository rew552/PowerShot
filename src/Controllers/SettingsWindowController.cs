using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PowerShot.Models;


namespace PowerShot.Controllers
{
    public class SettingsWindowController
    {
        private Window _window;
        private AppSettings _settings;
        private string _settingsPath;

        private TextBox _saveFolderTextBox;
        private Button _browseFolderButton;

        private Slider _jpegQualitySlider;
        private TextBlock _jpegQualityValueLabel;

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

            _jpegQualitySlider = (Slider)_window.FindName("JpegQualitySlider");
            _jpegQualityValueLabel = (TextBlock)_window.FindName("JpegQualityValueLabel");

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
        }

        private void Initialize()
        {
            _saveFolderTextBox.Text = _settings.SaveFolder;
            _jpegQualitySlider.Value = _settings.JpegQuality;
            _jpegQualityValueLabel.Text = _settings.JpegQuality.ToString();
        }

        private void BrowseFolderButton_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                dialog.Description = "保存先フォルダを選択してください";
                if (!string.IsNullOrEmpty(_saveFolderTextBox.Text))
                {
                    try
                    {
                        dialog.SelectedPath = Path.GetFullPath(
                            Path.Combine(Path.GetDirectoryName(_settingsPath), _saveFolderTextBox.Text));
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("  [Warn] 保存先フォルダの解決に失敗しました: " + ex.Message);
                    }
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
}
