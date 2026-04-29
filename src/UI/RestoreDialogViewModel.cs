using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data.SqlClient;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using SsmsRestoreDrop.Logging;
using SsmsRestoreDrop.Services;

namespace SsmsRestoreDrop.UI
{
    public sealed class ConnectionItem
    {
        public string DisplayName      { get; set; } = string.Empty;
        public string ConnectionString { get; set; } = string.Empty;
        public override string ToString() => DisplayName;
    }

    public sealed class RelocationRow : INotifyPropertyChanged
    {
        private string _physicalPath = string.Empty;

        public string LogicalName  { get; set; } = string.Empty;
        public string FileType     { get; set; } = string.Empty;

        public string PhysicalPath
        {
            get => _physicalPath;
            set { _physicalPath = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? p = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
    }

    public sealed class RestoreDialogViewModel : INotifyPropertyChanged
    {
        private readonly SsmsConnectionService _connService;
        private readonly IServiceProvider      _serviceProvider;

        // ── Author / version (shown in dialog footer) ─────────────────────────
        public string Version { get; } =
            typeof(RestoreDialogViewModel).Assembly.GetName().Version?.ToString(3) ?? "1.1.0";

        // ── Source files ──────────────────────────────────────────────────────
        public ObservableCollection<string> BackupFiles { get; } = new ObservableCollection<string>();

        // ── Connection ────────────────────────────────────────────────────────
        public ObservableCollection<ConnectionItem> Connections { get; } = new ObservableCollection<ConnectionItem>();

        private ConnectionItem? _selectedConnection;
        public ConnectionItem? SelectedConnection
        {
            get => _selectedConnection;
            set
            {
                _selectedConnection = value;
                OnPropertyChanged();
                _ = RefreshBackupInfoAsync();
            }
        }

        // ── Backup header ─────────────────────────────────────────────────────
        public ObservableCollection<BackupHeaderInfo> BackupSets { get; } = new ObservableCollection<BackupHeaderInfo>();

        private BackupHeaderInfo? _selectedBackupSet;
        public BackupHeaderInfo? SelectedBackupSet
        {
            get => _selectedBackupSet;
            set
            {
                _selectedBackupSet = value;
                OnPropertyChanged();
                _ = RefreshFileListAsync();
            }
        }

        // ── Target database ───────────────────────────────────────────────────
        private string _targetDatabase = string.Empty;
        public string TargetDatabase
        {
            get => _targetDatabase;
            set { _targetDatabase = value; OnPropertyChanged(); }
        }

        // ── File relocation ───────────────────────────────────────────────────
        public ObservableCollection<RelocationRow> RelocateFiles { get; } = new ObservableCollection<RelocationRow>();

        // ── Options ───────────────────────────────────────────────────────────
        private bool _replace = true;
        public bool Replace
        {
            get => _replace;
            set { _replace = value; OnPropertyChanged(); }
        }

        private bool _noRecovery;
        public bool NoRecovery
        {
            get => _noRecovery;
            set { _noRecovery = value; OnPropertyChanged(); if (value) { Standby = false; } }
        }

        private bool _standby;
        public bool Standby
        {
            get => _standby;
            set { _standby = value; OnPropertyChanged(); if (value) { NoRecovery = false; } }
        }

        private bool _keepCdc;
        public bool KeepCdc
        {
            get => _keepCdc;
            set { _keepCdc = value; OnPropertyChanged(); }
        }

        private bool _closeConnections = true;
        public bool CloseConnections
        {
            get => _closeConnections;
            set { _closeConnections = value; OnPropertyChanged(); }
        }

        private bool _takeTailLog;
        public bool TakeTailLog
        {
            get => _takeTailLog;
            set { _takeTailLog = value; OnPropertyChanged(); }
        }

        // ── Status ────────────────────────────────────────────────────────────
        private string _statusMessage = string.Empty;
        public string StatusMessage
        {
            get => _statusMessage;
            set { _statusMessage = value; OnPropertyChanged(); }
        }

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set { _isBusy = value; OnPropertyChanged(); }
        }

        private int _percentComplete;
        public int PercentComplete
        {
            get => _percentComplete;
            set { _percentComplete = value; OnPropertyChanged(); }
        }

        private string _outputText = string.Empty;
        public string OutputText
        {
            get => _outputText;
            set { _outputText = value; OnPropertyChanged(); }
        }

        public void AppendOutputLine(string line)
        {
            OutputText = string.IsNullOrEmpty(OutputText)
                ? line
                : OutputText + Environment.NewLine + line;
        }

        // ── Construction ──────────────────────────────────────────────────────
        public RestoreDialogViewModel(SsmsConnectionService connService,
                                      IServiceProvider serviceProvider,
                                      string[] initialFiles)
        {
            _connService     = connService;
            _serviceProvider = serviceProvider;

            foreach (var f in initialFiles)
                BackupFiles.Add(f);

            PopulateConnections();
        }

        // ── Connection loading ────────────────────────────────────────────────
        private void PopulateConnections()
        {
            Connections.Clear();

            // Active Object Explorer connection first
            var active = _connService.GetActiveConnection();
            if (active != null)
            {
                Connections.Add(new ConnectionItem
                {
                    DisplayName      = $"{active.DataSource} (Object Explorer)",
                    ConnectionString = active.ConnectionString
                });
            }

            // All open OE connections
            var all = _connService.GetAllObjectExplorerConnections();
            foreach (var c in all)
            {
                if (active != null && c.DataSource == active.DataSource) continue;
                Connections.Add(new ConnectionItem
                {
                    DisplayName      = c.DataSource,
                    ConnectionString = c.ConnectionString
                });
            }

            if (Connections.Count > 0)
                SelectedConnection = Connections[0];
        }

        public void AddConnection(string connectionString)
        {
            var csb = new SqlConnectionStringBuilder(connectionString);
            var item = new ConnectionItem
            {
                DisplayName      = csb.DataSource,
                ConnectionString = connectionString
            };
            Connections.Add(item);
            SelectedConnection = item;
        }

        // ── Backup info loading ───────────────────────────────────────────────
        public async Task RefreshBackupInfoAsync()
        {
            if (SelectedConnection == null || BackupFiles.Count == 0) return;
            IsBusy        = true;
            StatusMessage = "Reading backup header...";
            try
            {
                var inspector = new BackupInspector(SelectedConnection.ConnectionString);
                var files     = BackupFiles.ToList();

                var headers = await Task.Run(() => inspector.ReadHeader(files));

                Application.Current.Dispatcher.Invoke(() =>
                {
                    BackupSets.Clear();
                    foreach (var h in headers)
                        BackupSets.Add(h);

                    SelectedBackupSet = BackupSets.FirstOrDefault();
                    if (string.IsNullOrEmpty(TargetDatabase) && SelectedBackupSet != null)
                        TargetDatabase = SelectedBackupSet.DatabaseName;

                    StatusMessage = $"{headers.Count} backup set(s) found.";
                });
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error reading header: {ex.Message}";
                Logger.Error("RefreshBackupInfoAsync failed", ex);
            }
            finally
            {
                IsBusy = false;
            }
        }

        public async Task RefreshFileListAsync()
        {
            if (SelectedConnection == null || SelectedBackupSet == null || BackupFiles.Count == 0)
                return;

            IsBusy        = true;
            StatusMessage = "Reading file list...";
            try
            {
                var inspector = new BackupInspector(SelectedConnection.ConnectionString);
                var files     = BackupFiles.ToList();
                int position  = SelectedBackupSet.Position;

                var (fileList, dataPath, logPath) = await Task.Run(() =>
                {
                    var fl = inspector.ReadFileList(files, position);
                    var (dp, lp) = inspector.GetDefaultPaths();
                    return (fl, dp, lp);
                });

                var reloc = inspector.BuildDefaultRelocation(fileList, dataPath, logPath);

                Application.Current.Dispatcher.Invoke(() =>
                {
                    RelocateFiles.Clear();
                    foreach (var r in reloc)
                        RelocateFiles.Add(new RelocationRow
                        {
                            LogicalName  = r.LogicalName,
                            FileType     = r.FileType,
                            PhysicalPath = r.PhysicalPath
                        });

                    if (string.IsNullOrEmpty(TargetDatabase) && SelectedBackupSet != null)
                        TargetDatabase = SelectedBackupSet.DatabaseName;

                    StatusMessage = $"{fileList.Count} file(s) in backup set.";
                });
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error reading file list: {ex.Message}";
                Logger.Error("RefreshFileListAsync failed", ex);
            }
            finally
            {
                IsBusy = false;
            }
        }

        // ── Build RestoreOptions ──────────────────────────────────────────────
        public RestoreOptions BuildOptions(bool scriptOnly = false)
        {
            var opts = new RestoreOptions
            {
                ConnectionString  = SelectedConnection?.ConnectionString ?? string.Empty,
                TargetDatabase    = TargetDatabase,
                BackupSetPosition = SelectedBackupSet?.Position ?? 1,
                Replace           = Replace,
                NoRecovery        = NoRecovery,
                Standby           = Standby,
                KeepCdc           = KeepCdc,
                CloseConnections  = CloseConnections,
                TakeTailLog       = TakeTailLog,
                ScriptOnly        = scriptOnly
            };

            foreach (var f in BackupFiles)
                opts.BackupFiles.Add(f);

            foreach (var r in RelocateFiles)
                opts.RelocateFiles.Add(new RelocateFileEntry
                {
                    LogicalName  = r.LogicalName,
                    FileType     = r.FileType,
                    PhysicalPath = r.PhysicalPath
                });

            return opts;
        }

        // ── Validation ────────────────────────────────────────────────────────
        public bool Validate(out string error)
        {
            if (BackupFiles.Count == 0)       { error = "No backup files selected.";         return false; }
            if (SelectedConnection == null)   { error = "No server connection selected.";    return false; }
            if (string.IsNullOrWhiteSpace(TargetDatabase))
                                               { error = "Target database name is required."; return false; }
            error = string.Empty;
            return true;
        }

        // ── INotifyPropertyChanged ────────────────────────────────────────────
        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? p = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
    }
}
