using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;
using SsmsRestoreDrop.Services;

namespace SsmsRestoreDrop.UI
{
    public sealed class LogEntry
    {
        public string Text       { get; set; } = string.Empty;
        public Brush  Foreground { get; set; } = Brushes.Black;
    }

    public partial class ProgressToolWindowControl : System.Windows.Controls.UserControl
    {
        private readonly ObservableCollection<LogEntry> _entries = new ObservableCollection<LogEntry>();

        public ProgressToolWindowControl()
        {
            InitializeComponent();
            MessageList.ItemsSource = _entries;
        }

        public void Reset(string databaseName)
        {
            Dispatcher.Invoke(() =>
            {
                _entries.Clear();
                DbNameText.Text   = databaseName;
                PercentText.Text  = "0%";
                ProgressBar.Value = 0;
            });
        }

        public void AppendProgress(RestoreProgress progress)
        {
            Dispatcher.Invoke(() =>
            {
                if (progress.Percent >= 0)
                {
                    ProgressBar.Value = progress.Percent;
                    PercentText.Text  = $"{progress.Percent}%";
                }

                if (!string.IsNullOrWhiteSpace(progress.Message))
                {
                    var brush = progress.IsError ? Brushes.Red
                              : progress.Percent == 100 ? Brushes.DarkGreen
                              : (Brush)SystemColors.ControlTextBrush;

                    _entries.Add(new LogEntry
                    {
                        Text       = $"[{DateTime.Now:HH:mm:ss}] {progress.Message}",
                        Foreground = brush
                    });

                    // Auto-scroll to the bottom
                    if (_entries.Count > 0)
                        MessageList.ScrollIntoView(_entries[_entries.Count - 1]);
                }
            });
        }

        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            _entries.Clear();
            ProgressBar.Value = 0;
            PercentText.Text  = string.Empty;
        }

        private void CopyAll_Click(object sender, RoutedEventArgs e)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var entry in _entries)
                sb.AppendLine(entry.Text);
            if (sb.Length > 0)
                Clipboard.SetText(sb.ToString());
        }
    }
}
