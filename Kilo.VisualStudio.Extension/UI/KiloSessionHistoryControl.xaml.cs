using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using Kilo.VisualStudio.Contracts.Models;

namespace Kilo.VisualStudio.Extension.UI
{
    public partial class KiloSessionHistoryControl : UserControl
    {
        private IReadOnlyList<KiloSessionSummary> _sessions = new List<KiloSessionSummary>();

        public KiloSessionHistoryControl()
        {
            InitializeComponent();
        }

        public void SetSessions(IReadOnlyList<KiloSessionSummary> sessions)
        {
            _sessions = sessions;
            SessionListBox.ItemsSource = sessions;
            SessionCountLabel.Text = $"{sessions.Count} sessions";
            UpdateButtonState();
        }

        private void UpdateButtonState()
        {
            var hasSelection = SessionListBox.SelectedItem != null;
            DeleteButton.IsEnabled = hasSelection;
            OpenButton.IsEnabled = hasSelection;
        }

        private void SessionListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateButtonState();
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            RefreshRequested?.Invoke(this, EventArgs.Empty);
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (SessionListBox.SelectedItem is KiloSessionSummary session)
            {
                DeleteRequested?.Invoke(this, session.SessionId);
            }
        }

        private void OpenButton_Click(object sender, RoutedEventArgs e)
        {
            if (SessionListBox.SelectedItem is KiloSessionSummary session)
            {
                OpenRequested?.Invoke(this, session.SessionId);
            }
        }

        public event EventHandler? RefreshRequested;
        public event EventHandler<string>? DeleteRequested;
        public event EventHandler<string>? OpenRequested;
    }
}