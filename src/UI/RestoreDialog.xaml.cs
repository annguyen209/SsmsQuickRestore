using System;
using System.Windows;
using System.Windows.Controls;
using Microsoft.VisualStudio.Shell;
using SsmsRestoreDrop.Commands;
using SsmsRestoreDrop.Logging;
using SsmsRestoreDrop.Services;
using Task = System.Threading.Tasks.Task;
using MessageBox = System.Windows.MessageBox;

namespace SsmsRestoreDrop.UI
{
    public partial class RestoreDialog : Window
    {
        private readonly AsyncPackage           _package;
        private readonly RestoreDialogViewModel _vm;

        public RestoreDialog(AsyncPackage package,
                             SsmsConnectionService connService,
                             string[] initialFiles)
        {
            _package    = package;
            _vm         = new RestoreDialogViewModel(connService, package, initialFiles);
            DataContext = _vm;

            InitializeComponent();

            OutputBox.TextChanged += (_, __) => OutputBox.ScrollToEnd();

            Loaded += async (_, __) => await _vm.RefreshBackupInfoAsync();
        }

        // ── File management ───────────────────────────────────────────────────
        private void AddFile_Click(object sender, RoutedEventArgs e)
        {
            var paths = RestoreFromFileCommand.PickBackupFiles();
            if (paths == null) return;
            foreach (var p in paths)
                if (!_vm.BackupFiles.Contains(p))
                    _vm.BackupFiles.Add(p);

            _ = _vm.RefreshBackupInfoAsync();
        }

        private void RemoveFile_Click(object sender, RoutedEventArgs e)
        {
            if (FileListBox.SelectedItem is string sel)
                _vm.BackupFiles.Remove(sel);
        }

        // ── Connect ad-hoc ────────────────────────────────────────────────────
        private void Connect_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new ConnectDialog();
            if (dlg.ShowDialog() == true && !string.IsNullOrWhiteSpace(dlg.ConnectionString))
            {
                _vm.AddConnection(dlg.ConnectionString);
                _ = _vm.RefreshBackupInfoAsync();
            }
        }

        // ── Refresh ───────────────────────────────────────────────────────────
        private void Refresh_Click(object sender, RoutedEventArgs e)
            => _ = _vm.RefreshBackupInfoAsync();

        // ── Script ────────────────────────────────────────────────────────────
        private void Script_Click(object sender, RoutedEventArgs e)
        {
            if (!_vm.Validate(out var err))
            {
                MessageBox.Show(err, "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            try
            {
                var opts   = _vm.BuildOptions(scriptOnly: true);
                var runner = new RestoreRunner(opts);
                _vm.OutputText  = runner.GenerateScript();
                _vm.StatusMessage = "Script generated.";
            }
            catch (Exception ex)
            {
                Logger.Error("Script generation failed", ex);
                _vm.OutputText    = $"-- Script generation failed:{Environment.NewLine}-- {ex.Message}";
                _vm.StatusMessage = "Script generation failed.";
            }
        }

        // ── Restore ───────────────────────────────────────────────────────────
        private async void Restore_Click(object sender, RoutedEventArgs e)
        {
            if (!_vm.Validate(out var err))
            {
                MessageBox.Show(err, "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            IsEnabled         = false;
            _vm.IsBusy        = true;
            _vm.PercentComplete = 0;
            _vm.StatusMessage = "Restoring...";
            _vm.OutputText    = string.Empty;

            var opts = _vm.BuildOptions();
            _vm.AppendOutputLine($"[{DateTime.Now:HH:mm:ss}] Starting restore of '{opts.TargetDatabase}'.");

            void OnProgress(RestoreProgress p)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    if (p.Percent >= 0)
                    {
                        _vm.PercentComplete = p.Percent;
                        _vm.StatusMessage   = $"{p.Percent}% - {p.Message}";
                        _vm.AppendOutputLine($"[{DateTime.Now:HH:mm:ss}] {p.Percent,3}%  {p.Message}");
                    }
                    else
                    {
                        _vm.StatusMessage = p.Message;
                        _vm.AppendOutputLine($"[{DateTime.Now:HH:mm:ss}]       {p.Message}");
                    }
                });
            }

            try
            {
                var runner = new RestoreRunner(opts);
                await Task.Run(() => runner.Execute(OnProgress));

                _vm.PercentComplete = 100;
                _vm.StatusMessage   = "Restore complete.";
                _vm.AppendOutputLine($"[{DateTime.Now:HH:mm:ss}] Restore complete.");

                var result = MessageBox.Show(
                    $"Database '{opts.TargetDatabase}' restored successfully.\n\n" +
                    "Close this window?\n\n" +
                    "Yes  - close the restore window.\n" +
                    "No   - keep the window open to review the log.",
                    "SSMS Quick Restore",
                    MessageBoxButton.YesNo, MessageBoxImage.Information,
                    MessageBoxResult.Yes);

                if (result == MessageBoxResult.Yes)
                {
                    DialogResult = true;
                    Close();
                }
            }
            catch (RestoreException rex)
            {
                _vm.StatusMessage = "Restore failed.";
                _vm.AppendOutputLine($"[{DateTime.Now:HH:mm:ss}] ERROR: {rex.Message}");
                MessageBox.Show(rex.Message, "Restore Failed",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                Logger.Error("Unexpected restore error", ex);
                _vm.StatusMessage = "Restore failed.";
                _vm.AppendOutputLine($"[{DateTime.Now:HH:mm:ss}] ERROR: {ex.Message}");
                MessageBox.Show($"Unexpected error:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _vm.IsBusy = false;
                IsEnabled  = true;
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void CopyOutput_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_vm.OutputText)) return;
            try
            {
                System.Windows.Clipboard.SetText(_vm.OutputText);
                _vm.StatusMessage = "Output copied to clipboard.";
            }
            catch (Exception ex)
            {
                Logger.Warn($"Clipboard copy failed: {ex.Message}");
            }
        }

        private void ClearOutput_Click(object sender, RoutedEventArgs e)
            => _vm.OutputText = string.Empty;
    }
}
