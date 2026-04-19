using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using Microsoft.SqlServer.Management.Common;
using Microsoft.SqlServer.Management.Smo;
using SsmsRestoreDrop.Logging;

namespace SsmsRestoreDrop.Services
{
    public sealed class BackupInspector
    {
        private readonly string _connectionString;

        public BackupInspector(string connectionString)
        {
            _connectionString = connectionString;
        }

        /// <summary>
        /// Runs RESTORE HEADERONLY against all supplied backup files and returns
        /// a list of backup sets found (one file = one striped set counts as one entry).
        /// </summary>
        public List<BackupHeaderInfo> ReadHeader(IEnumerable<string> backupFiles)
        {
            var server  = Connect();
            var restore = BuildRestore(backupFiles);

            DataTable dt;
            try
            {
                dt = restore.ReadBackupHeader(server);
            }
            catch (Exception ex)
            {
                Logger.Error("RESTORE HEADERONLY failed", ex);
                throw;
            }

            var result = new List<BackupHeaderInfo>();
            foreach (DataRow row in dt.Rows)
            {
                result.Add(new BackupHeaderInfo
                {
                    Position         = SafeInt(row, "Position"),
                    DatabaseName     = SafeStr(row, "DatabaseName"),
                    BackupType       = MapBackupType(SafeInt(row, "BackupType")),
                    ServerName       = SafeStr(row, "ServerName"),
                    BackupStartDate  = SafeStr(row, "BackupStartDate"),
                    BackupFinishDate = SafeStr(row, "BackupFinishDate"),
                    Description      = SafeStr(row, "BackupDescription")
                });
            }
            return result;
        }

        /// <summary>
        /// Runs RESTORE FILELISTONLY for the specified position within the backup.
        /// </summary>
        public List<BackupFileInfo> ReadFileList(IEnumerable<string> backupFiles, int position = 1)
        {
            var server  = Connect();
            var restore = BuildRestore(backupFiles);
            restore.FileNumber = position;

            DataTable dt;
            try
            {
                dt = restore.ReadFileList(server);
            }
            catch (Exception ex)
            {
                Logger.Error($"RESTORE FILELISTONLY failed (position={position})", ex);
                throw;
            }

            var result = new List<BackupFileInfo>();
            foreach (DataRow row in dt.Rows)
            {
                result.Add(new BackupFileInfo
                {
                    LogicalName  = SafeStr(row, "LogicalName"),
                    PhysicalName = SafeStr(row, "PhysicalName"),
                    FileType     = SafeStr(row, "Type")
                });
            }
            return result;
        }

        /// <summary>
        /// Queries the target server for its default data and log directories.
        /// </summary>
        public (string dataPath, string logPath) GetDefaultPaths()
        {
            var server = Connect();
            var data   = server.Settings.DefaultFile;
            var log    = server.Settings.DefaultLog;

            if (string.IsNullOrWhiteSpace(data))
                data = server.MasterDBPath;
            if (string.IsNullOrWhiteSpace(log))
                log  = server.MasterDBLogPath;

            return (data, log);
        }

        /// <summary>
        /// Builds the auto-filled relocation list: takes the logical file list and
        /// redirects each file to the server's default data/log paths, preserving
        /// the original file name.
        /// </summary>
        public List<RelocateFileEntry> BuildDefaultRelocation(
            IEnumerable<BackupFileInfo> fileList,
            string dataPath,
            string logPath)
        {
            return fileList.Select(f => new RelocateFileEntry
            {
                LogicalName  = f.LogicalName,
                FileType     = f.FileType,
                PhysicalPath = Path.Combine(
                    f.FileType == "L" ? logPath : dataPath,
                    Path.GetFileName(f.PhysicalName))
            }).ToList();
        }

        private Server Connect()
        {
            var csb  = new SqlConnectionStringBuilder(_connectionString);
            ServerConnection sc;
            if (csb.IntegratedSecurity)
            {
                sc = new ServerConnection(csb.DataSource);
            }
            else
            {
                sc = new ServerConnection(csb.DataSource, csb.UserID, csb.Password);
            }
            sc.ConnectTimeout = 30;
            return new Server(sc);
        }

        private static Restore BuildRestore(IEnumerable<string> files)
        {
            var restore = new Restore();
            foreach (var f in files)
                restore.Devices.AddDevice(f, DeviceType.File);
            return restore;
        }

        private static string SafeStr(DataRow row, string col)
        {
            if (!row.Table.Columns.Contains(col)) return string.Empty;
            var val = row[col];
            return val == DBNull.Value ? string.Empty : val?.ToString() ?? string.Empty;
        }

        private static int SafeInt(DataRow row, string col)
        {
            if (!row.Table.Columns.Contains(col)) return 0;
            var val = row[col];
            return val == DBNull.Value ? 0 : Convert.ToInt32(val);
        }

        private static string MapBackupType(int type) => type switch
        {
            1 => "Full",
            2 => "Differential",
            3 => "Log",
            4 => "File",
            5 => "Diff File",
            6 => "Partial",
            7 => "Diff Partial",
            _ => $"Type {type}"
        };
    }
}
