using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using Microsoft.SqlServer.Management.Common;
using Microsoft.SqlServer.Management.Smo;
using SsmsRestoreDrop.Logging;

namespace SsmsRestoreDrop.Services
{
    public sealed class RestoreRunner
    {
        private readonly RestoreOptions _options;

        public RestoreRunner(RestoreOptions options)
        {
            _options = options;
        }

        /// <summary>
        /// Executes the restore operation, streaming progress via the supplied callback.
        /// Runs synchronously — call from a background thread.
        /// </summary>
        public void Execute(Action<RestoreProgress> onProgress)
        {
            var server = Connect();

            if (_options.CloseConnections)
                KillConnections(server, _options.TargetDatabase);

            if (_options.TakeTailLog)
                TakeTailLogBackup(server, _options.TargetDatabase, onProgress);

            var restore = BuildRestore();

            restore.PercentComplete += (s, e) =>
                onProgress(new RestoreProgress(e.Percent, e.Message));

            restore.Information += (s, e) =>
                onProgress(new RestoreProgress(-1, e.Error?.Message ?? e.ToString()));

            try
            {
                Logger.Info($"Starting restore of '{_options.TargetDatabase}' on [{_options.ConnectionString.Split(';')[0]}]");
                onProgress(new RestoreProgress(0, $"Starting restore of database '{_options.TargetDatabase}'..."));
                if (_options.KeepCdc)
                {
                    // SMO has no KEEP_CDC property; execute the generated T-SQL directly.
                    var script = GenerateScript();
                    server.ConnectionContext.ExecuteNonQuery(script);
                }
                else
                {
                    restore.SqlRestore(server);
                }
                onProgress(new RestoreProgress(100, $"Restore of '{_options.TargetDatabase}' completed successfully."));
                Logger.Info($"Restore of '{_options.TargetDatabase}' completed");
            }
            catch (Exception ex)
            {
                Logger.Error("Restore failed", ex);
                var msg = BuildErrorMessage(ex);
                onProgress(new RestoreProgress(-1, msg, isError: true));
                throw new RestoreException(msg, ex);
            }
        }

        /// <summary>
        /// Generates the equivalent RESTORE T-SQL without executing it.
        /// </summary>
        public string GenerateScript()
        {
            var sb = new StringBuilder();

            if (_options.TakeTailLog)
            {
                sb.AppendLine("-- Take tail-log backup before restore");
                sb.AppendLine($"BACKUP LOG [{_options.TargetDatabase}]");
                sb.AppendLine($"    TO DISK = N'{EscapeStr(GetTailLogPath())}'");
                sb.AppendLine("    WITH NO_TRUNCATE, NORECOVERY;");
                sb.AppendLine();
            }

            sb.AppendLine("RESTORE DATABASE " + QuoteName(_options.TargetDatabase));
            sb.AppendLine("FROM");

            var devices = _options.BackupFiles
                .Select(f => $"    DISK = N'{EscapeStr(f)}'")
                .ToList();
            sb.AppendLine(string.Join("," + Environment.NewLine, devices));

            sb.AppendLine("WITH");

            var opts = new List<string>();
            opts.Add($"    FILE = {_options.BackupSetPosition}");

            foreach (var r in _options.RelocateFiles)
                opts.Add($"    MOVE N'{EscapeStr(r.LogicalName)}' TO N'{EscapeStr(r.PhysicalPath)}'");

            if (_options.Replace)       opts.Add("    REPLACE");
            if (_options.NoRecovery)    opts.Add("    NORECOVERY");
            else if (_options.Standby)  opts.Add($"    STANDBY = N'{EscapeStr(_options.StandbyFile)}'");
            else                         opts.Add("    RECOVERY");
            if (_options.KeepCdc)       opts.Add("    KEEP_CDC");
            opts.Add("    STATS = 5");

            sb.AppendLine(string.Join("," + Environment.NewLine, opts));
            sb.AppendLine(";");

            return sb.ToString();
        }

        private Restore BuildRestore()
        {
            var restore = new Restore
            {
                Action           = RestoreActionType.Database,
                Database         = _options.TargetDatabase,
                ReplaceDatabase  = _options.Replace,
                NoRecovery       = _options.NoRecovery,
                FileNumber       = _options.BackupSetPosition
            };

            if (_options.Standby && !_options.NoRecovery)
                restore.StandbyFile = _options.StandbyFile;

            // SMO has no KeepCdc property; handled below via raw T-SQL if needed.

            foreach (var f in _options.BackupFiles)
                restore.Devices.AddDevice(f, DeviceType.File);

            foreach (var r in _options.RelocateFiles)
                restore.RelocateFiles.Add(new RelocateFile(r.LogicalName, r.PhysicalPath));

            return restore;
        }

        private Server Connect()
        {
            var csb = new SqlConnectionStringBuilder(_options.ConnectionString);
            ServerConnection sc = csb.IntegratedSecurity
                ? new ServerConnection(csb.DataSource)
                : new ServerConnection(csb.DataSource, csb.UserID, csb.Password);
            sc.ConnectTimeout = 60;
            return new Server(sc);
        }

        private static void KillConnections(Server server, string database)
        {
            try
            {
                server.KillAllProcesses(database);
                Logger.Info($"Killed all connections to '{database}'");
            }
            catch (Exception ex)
            {
                Logger.Warn($"KillAllProcesses failed: {ex.Message}");
            }
        }

        private void TakeTailLogBackup(Server server, string database,
            Action<RestoreProgress> onProgress)
        {
            onProgress(new RestoreProgress(-1, $"Taking tail-log backup of '{database}'..."));
            var backup = new Backup
            {
                Action        = BackupActionType.Log,
                Database      = database,
                NoRecovery    = true,
                Incremental   = false
            };
            backup.Devices.AddDevice(GetTailLogPath(), DeviceType.File);
            try
            {
                backup.SqlBackup(server);
                onProgress(new RestoreProgress(-1, "Tail-log backup completed."));
                Logger.Info($"Tail-log backup of '{database}' completed");
            }
            catch (Exception ex)
            {
                Logger.Error("Tail-log backup failed", ex);
                throw;
            }
        }

        private string GetTailLogPath()
        {
            var dir = System.IO.Path.GetDirectoryName(_options.BackupFiles.FirstOrDefault() ?? "C:\\");
            return System.IO.Path.Combine(dir ?? "C:\\", $"{_options.TargetDatabase}_tail_{DateTime.Now:yyyyMMddHHmmss}.trn");
        }

        private static string BuildErrorMessage(Exception ex)
        {
            if (ex is FailedOperationException foe && foe.InnerException != null)
                return $"Restore failed: {foe.InnerException.Message}\n\n{foe.Operation} on '{foe.FailedObject}'";
            return $"Restore failed: {ex.Message}";
        }

        private static string QuoteName(string name) => $"[{name.Replace("]", "]]")}]";
        private static string EscapeStr(string s)    => s.Replace("'", "''");
    }

    public sealed class RestoreException : Exception
    {
        public RestoreException(string message, Exception inner) : base(message, inner) { }
    }
}
