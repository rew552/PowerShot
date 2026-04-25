using System;
using System.Windows;
using System.Windows.Controls;

namespace PowerShot
{
    internal class OverlayController
    {
        private readonly CheckBox _enableBox;
        private readonly CheckBox _embedSysInfoBox;
        private readonly ComboBox _sysInfoPositionBox;
        private readonly TextBox _textBox;
        private readonly ComboBox _textPositionBox;
        private readonly Button _updatePreviewButton;
        private readonly AppSettings _settings;
        private readonly Action _onSettingsSaved;
        private readonly Action _onPreviewRequested;

        private bool _suppress;

        public OverlayController(CheckBox enableBox, CheckBox embedSysInfoBox,
            ComboBox sysInfoPositionBox, TextBox textBox, ComboBox textPositionBox,
            Button updatePreviewButton, AppSettings settings,
            Action onSettingsSaved, Action onPreviewRequested)
        {
            _enableBox = enableBox;
            _embedSysInfoBox = embedSysInfoBox;
            _sysInfoPositionBox = sysInfoPositionBox;
            _textBox = textBox;
            _textPositionBox = textPositionBox;
            _updatePreviewButton = updatePreviewButton;
            _settings = settings;
            _onSettingsSaved = onSettingsSaved;
            _onPreviewRequested = onPreviewRequested;
        }

        public void Initialize()
        {
            if (_settings == null) return;

            _suppress = true;
            if (_enableBox != null) _enableBox.IsChecked = _settings.OverlayEnabled;
            if (_embedSysInfoBox != null) _embedSysInfoBox.IsChecked = _settings.EmbedSysInfo;
            if (_textBox != null) _textBox.Text = _settings.OverlayText;
            ComboBoxHelper.SetSelectedByTag(_sysInfoPositionBox, _settings.SysInfoPosition);
            ComboBoxHelper.SetSelectedByTag(_textPositionBox, _settings.OverlayTextPosition);
            _suppress = false;

            if (_enableBox != null) _enableBox.Click += OnSettingsChanged;
            if (_embedSysInfoBox != null) _embedSysInfoBox.Click += OnSettingsChanged;
            if (_sysInfoPositionBox != null) _sysInfoPositionBox.SelectionChanged += OnSettingsChanged;
            if (_textPositionBox != null) _textPositionBox.SelectionChanged += OnSettingsChanged;
            if (_updatePreviewButton != null) _updatePreviewButton.Click += OnUpdatePreviewClicked;
        }

        private void OnSettingsChanged(object sender, RoutedEventArgs e)
        {
            if (_suppress || _settings == null) return;

            if (_enableBox != null) _settings.OverlayEnabled = _enableBox.IsChecked ?? false;
            if (_embedSysInfoBox != null) _settings.EmbedSysInfo = _embedSysInfoBox.IsChecked ?? false;
            if (_textBox != null) _settings.OverlayText = _textBox.Text;

            string sysTag = ComboBoxHelper.GetSelectedTag(_sysInfoPositionBox);
            if (sysTag != null) _settings.SysInfoPosition = sysTag;

            string txtTag = ComboBoxHelper.GetSelectedTag(_textPositionBox);
            if (txtTag != null) _settings.OverlayTextPosition = txtTag;

            if (_onSettingsSaved != null) _onSettingsSaved();
        }

        private void OnUpdatePreviewClicked(object sender, RoutedEventArgs e)
        {
            if (_settings == null) return;
            if (_textBox != null) _settings.OverlayText = _textBox.Text;
            if (_onSettingsSaved != null) _onSettingsSaved();
            if (_onPreviewRequested != null) _onPreviewRequested();
        }
    }
}
