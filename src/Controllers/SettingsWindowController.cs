﻿using System;
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
}
