using System;

namespace SsmsRestoreDrop.Commands
{
    internal static class PackageGuids
    {
        public const string SsmsRestoreDropPackageString  = "A4B5C6D7-E8F9-4A1B-8C3D-4E5F6A7B8C9D";
        public const string SsmsRestoreDropCmdSetString   = "B5C6D7E8-F9A0-4B2C-9D4E-5F6A7B8C9D0E";
        public const string ProgressToolWindowString      = "D7E8F9A0-B1C2-4D3E-BF5A-7B8C9D0E1F2A";

        public static readonly Guid SsmsRestoreDropPackage = new Guid(SsmsRestoreDropPackageString);
        public static readonly Guid SsmsRestoreDropCmdSet  = new Guid(SsmsRestoreDropCmdSetString);
        public static readonly Guid ProgressToolWindow     = new Guid(ProgressToolWindowString);
    }

    internal static class PackageIds
    {
        public const int RestoreFromFileCommandId   = 0x0100;
        public const int ProgressToolWindowCmd      = 0x0101;
        public const int RestoreMenuGroup           = 0x1020;
        public const int RestoreToolbarGroup        = 0x1021;
        public const int RestoreToolbar             = 0x1030;
    }
}
