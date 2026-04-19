using System.Collections.Generic;

namespace SsmsRestoreDrop.Services
{
    public sealed class BackupHeaderInfo
    {
        public int    Position        { get; set; }
        public string DatabaseName    { get; set; } = string.Empty;
        public string BackupType      { get; set; } = string.Empty;
        public string ServerName      { get; set; } = string.Empty;
        public string BackupStartDate { get; set; } = string.Empty;
        public string BackupFinishDate{ get; set; } = string.Empty;
        public string Description     { get; set; } = string.Empty;

        public override string ToString() =>
            $"[{Position}] {BackupType} - {DatabaseName} ({BackupStartDate})";
    }

    public sealed class BackupFileInfo
    {
        public string LogicalName  { get; set; } = string.Empty;
        public string PhysicalName { get; set; } = string.Empty;
        public string FileType     { get; set; } = string.Empty; // "D" or "L"
    }

    public sealed class RelocateFileEntry
    {
        public string LogicalName  { get; set; } = string.Empty;
        public string PhysicalPath { get; set; } = string.Empty;
        public string FileType     { get; set; } = string.Empty;
    }

    public sealed class RestoreOptions
    {
        public List<string>             BackupFiles       { get; } = new List<string>();
        public string                   ConnectionString  { get; set; } = string.Empty;
        public string                   TargetDatabase    { get; set; } = string.Empty;
        public int                      BackupSetPosition { get; set; } = 1;
        public List<RelocateFileEntry>  RelocateFiles     { get; } = new List<RelocateFileEntry>();
        public bool                     Replace           { get; set; }
        public bool                     NoRecovery        { get; set; }
        public bool                     Standby           { get; set; }
        public string                   StandbyFile       { get; set; } = string.Empty;
        public bool                     KeepCdc           { get; set; }
        public bool                     CloseConnections  { get; set; }
        public bool                     TakeTailLog       { get; set; }
        public bool                     ScriptOnly        { get; set; }
    }

    public sealed class RestoreProgress
    {
        public int    Percent { get; }
        public string Message { get; }
        public bool   IsError { get; }

        public RestoreProgress(int percent, string message, bool isError = false)
        {
            Percent = percent;
            Message = message;
            IsError = isError;
        }
    }
}
