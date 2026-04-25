using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PowerShot
{
    /// <summary>
    /// Owns the file-name input fields (prefix / sequence / digits / format) and the
    /// preview label, and keeps the auto-tracked sequence number in sync with the
    /// current directory.
    /// </summary>
    internal class FileNameComposer
    {
        // ComboBox index reserved for the "日時" (yyyyMMdd-HHmmss) entry.
        private const int DigitsTimestampIndex = 6;

        // Fixed timestamp format used by the 日時 mode.
        private const string TimestampFormat = "yyyyMMdd_HHmmss";

        private readonly TextBox _prefixBox;
        private readonly TextBox _sequenceBox;
        private readonly ComboBox _digitsBox;
        private readonly ComboBox _formatBox;
        private readonly TextBlock _previewLabel;
        private readonly AppSettings _settings;
        private readonly Func<string> _currentDirectory;

        private bool _suppress;

        public FileNameComposer(TextBox prefixBox, TextBox sequenceBox,
            ComboBox digitsBox, ComboBox formatBox, TextBlock previewLabel,
            AppSettings settings, Func<string> currentDirectoryProvider)
        {
            _prefixBox = prefixBox;
            _sequenceBox = sequenceBox;
            _digitsBox = digitsBox;
            _formatBox = formatBox;
            _previewLabel = previewLabel;
            _settings = settings;
            _currentDirectory = currentDirectoryProvider;
        }

        public void Initialize(SessionState session)
        {
            _suppress = true;

            _prefixBox.Text = session.LastPrefix ?? "";

            if (_digitsBox != null)
            {
                if (session.LastSequenceDigits == -1)
                {
                    _digitsBox.SelectedIndex = DigitsTimestampIndex;
                }
                else if (session.LastSequenceDigits >= 1 && session.LastSequenceDigits <= 6)
                {
                    _digitsBox.SelectedIndex = session.LastSequenceDigits - 1;
                }
            }

            if (_formatBox != null)
            {
                _formatBox.SelectedIndex = (session.LastFormat == "png") ? 1 : 0;
            }

            _suppress = false;

            BindEvents();
            UpdateInputInterlock();
        }

        private void BindEvents()
        {
            _prefixBox.TextChanged += OnPrefixChanged;
            _sequenceBox.TextChanged += OnSequenceChanged;
            _sequenceBox.PreviewTextInput += OnSequencePreviewInput;
            DataObject.AddPastingHandler(_sequenceBox, OnSequencePasting);

            if (_digitsBox != null) _digitsBox.SelectionChanged += OnDigitsChanged;
            if (_formatBox != null) _formatBox.SelectionChanged += OnFormatChanged;
        }

        /// <summary>Recompute sequence + preview + interlock after a directory change or settings change.</summary>
        public void Refresh()
        {
            UpdateInputInterlock();
            UpdateSequence();
            UpdateFileNamePreview();
        }

        /// <summary>Populate prefix/digits from a clicked file's name. Sequence is left auto-tracked.</summary>
        public void ApplyFromFileName(string fileName)
        {
            string nameNoExt = Path.GetFileNameWithoutExtension(fileName);
            var matchTimestamp = Regex.Match(nameNoExt, @"^(.+?)_(\d{8}_\d{6})$");
            var matchTimestampOnly = Regex.Match(nameNoExt, @"^(\d{8}_\d{6})$");
            var matchSimple = Regex.Match(nameNoExt, @"^(.+?)_(\d+)$");

            _suppress = true;

            if (matchTimestamp.Success)
            {
                _prefixBox.Text = matchTimestamp.Groups[1].Value;
                if (_digitsBox != null) _digitsBox.SelectedIndex = DigitsTimestampIndex;
            }
            else if (matchTimestampOnly.Success)
            {
                _prefixBox.Text = "";
                if (_digitsBox != null) _digitsBox.SelectedIndex = DigitsTimestampIndex;
            }
            else if (matchSimple.Success)
            {
                _prefixBox.Text = matchSimple.Groups[1].Value;
                int len = matchSimple.Groups[2].Length;
                if (_digitsBox != null && len >= 1 && len <= 6)
                    _digitsBox.SelectedIndex = len - 1;
            }
            else
            {
                _prefixBox.Text = nameNoExt;
            }

            _suppress = false;

            UpdateInputInterlock();
            Refresh();
        }

        public string GetPrefix()
        {
            return _prefixBox.Text;
        }

        public string GetFormat()
        {
            return (_formatBox != null && _formatBox.SelectedIndex == 1) ? "png" : "jpg";
        }

        public int GetDigits()
        {
            if (_digitsBox != null && _digitsBox.SelectedIndex >= 0)
            {
                if (_digitsBox.SelectedIndex == DigitsTimestampIndex) return -1;
                return _digitsBox.SelectedIndex + 1;
            }
            return 3;
        }

        public string GetFileName()
        {
            return FileManager.GenerateFileName(
                _prefixBox.Text, "", _sequenceBox.Text, GetFormat());
        }

        public void WriteSessionState(SessionState session)
        {
            session.LastPrefix = _prefixBox.Text;
            session.LastSequenceDigits = GetDigits();
            session.LastFormat = GetFormat();
        }

        // --- Private helpers ---

        private void UpdateInputInterlock()
        {
            bool hasPrefix = !string.IsNullOrWhiteSpace(_prefixBox.Text);
            _sequenceBox.IsEnabled = hasPrefix;
            if (_digitsBox != null) _digitsBox.IsEnabled = hasPrefix;
            if (_formatBox != null) _formatBox.IsEnabled = true;
        }

        private void UpdateSequence()
        {
            int digits = GetDigits();

            if (digits == -1)
            {
                _sequenceBox.MaxLength = 50;
                _sequenceBox.Text = DateTime.Now.ToString(TimestampFormat);
                return;
            }

            int maxLen = digits > 0 ? digits : 4;
            _sequenceBox.MaxLength = maxLen;

            string effectivePrefix = _prefixBox.Text;

            int seq = SequenceManager.GetNextSequence(_currentDirectory(), effectivePrefix, "");
            _sequenceBox.Text = seq.ToString("D" + maxLen);
        }

        private void UpdateFileNamePreview()
        {
            if (_previewLabel == null) return;
            _previewLabel.Text = GetFileName();
        }

        private void OnPrefixChanged(object sender, TextChangedEventArgs e)
        {
            if (_suppress) return;
            UpdateInputInterlock();
            Refresh();
        }

        private void OnSequenceChanged(object sender, TextChangedEventArgs e)
        {
            if (_suppress) return;
            UpdateFileNamePreview();
        }

        private void OnSequencePreviewInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !Regex.IsMatch(e.Text, @"^\d+$");
        }

        private void OnSequencePasting(object sender, DataObjectPastingEventArgs e)
        {
            if (e.DataObject.GetDataPresent(DataFormats.Text))
            {
                string text = (string)e.DataObject.GetData(DataFormats.Text);
                if (!Regex.IsMatch(text, @"^\d+$"))
                    e.CancelCommand();
            }
            else
            {
                e.CancelCommand();
            }
        }

        private void OnDigitsChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppress) return;
            Refresh();

            int digits = GetDigits();
            _sequenceBox.MaxLength = (digits > 0) ? digits : 20;
        }

        private void OnFormatChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateFileNamePreview();
        }
    }
}
