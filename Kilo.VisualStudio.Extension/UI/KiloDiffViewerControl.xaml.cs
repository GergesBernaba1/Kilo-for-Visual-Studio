using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using Kilo.VisualStudio.Contracts.Models;

namespace Kilo.VisualStudio.Extension.UI
{
    public partial class KiloDiffViewerControl : UserControl
    {
        private IReadOnlyList<KiloFileDiff> _diffs = new List<KiloFileDiff>();
        private int _currentIndex;

        public KiloDiffViewerControl()
        {
            InitializeComponent();
            UpdateNavigationState();
        }

        public void SetDiffs(IReadOnlyList<KiloFileDiff> diffs)
        {
            _diffs = diffs;
            _currentIndex = 0;
            ShowCurrentDiff();
            UpdateNavigationState();
        }

        private void ShowCurrentDiff()
        {
            if (_diffs == null || _diffs.Count == 0 || _currentIndex < 0 || _currentIndex >= _diffs.Count)
            {
                ClearView();
                return;
            }

            var diff = _diffs[_currentIndex];
            LeftFileLabel.Text = diff.FilePath + " (Original)";
            RightFileLabel.Text = diff.FilePath + " (Modified)";
            LeftContentTextBox.Text = diff.Before ?? string.Empty;
            RightContentTextBox.Text = diff.After ?? string.Empty;

            var additions = diff.Additions;
            var deletions = diff.Deletions;
            StatusLabel.Text = $"{additions} additions, {deletions} deletions";
            FileCountLabel.Text = $"{_currentIndex + 1} of {_diffs.Count}";
        }

        private void ClearView()
        {
            LeftFileLabel.Text = "Original";
            RightFileLabel.Text = "Modified";
            LeftContentTextBox.Text = string.Empty;
            RightContentTextBox.Text = string.Empty;
            StatusLabel.Text = "No changes";
            FileCountLabel.Text = "0 of 0";
        }

        private void UpdateNavigationState()
        {
            PreviousButton.IsEnabled = _currentIndex > 0;
            NextButton.IsEnabled = _currentIndex < _diffs.Count - 1;
            ApplyButton.IsEnabled = _diffs.Count > 0;
            RevertButton.IsEnabled = _diffs.Count > 0;
        }

        private void PreviousButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentIndex > 0)
            {
                _currentIndex--;
                ShowCurrentDiff();
                UpdateNavigationState();
            }
        }

        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentIndex < _diffs.Count - 1)
            {
                _currentIndex++;
                ShowCurrentDiff();
                UpdateNavigationState();
            }
        }

        private void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            ApplyRequested?.Invoke(this, _diffs);
        }

        private void RevertButton_Click(object sender, RoutedEventArgs e)
        {
            RevertRequested?.Invoke(this, System.EventArgs.Empty);
        }

        public event System.EventHandler<IReadOnlyList<KiloFileDiff>>? ApplyRequested;
        public event System.EventHandler? RevertRequested;
    }
}