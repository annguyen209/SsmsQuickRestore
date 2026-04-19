using System;
using System.ComponentModel.Design;
using System.Windows.Forms;
using Microsoft.VisualStudio.Shell;
using SsmsRestoreDrop.Logging;
using SsmsRestoreDrop.Services;
using SsmsRestoreDrop.UI;
using Task = System.Threading.Tasks.Task;

namespace SsmsRestoreDrop.Commands
{
    internal sealed class RestoreFromFileCommand
    {
        private readonly AsyncPackage _package;

        private RestoreFromFileCommand(AsyncPackage package, OleMenuCommandService commandService)
        {
            _package = package;
            var menuCommandID = new CommandID(
                PackageGuids.SsmsRestoreDropCmdSet,
                PackageIds.RestoreFromFileCommandId);
            commandService.AddCommand(new MenuCommand(Execute, menuCommandID));
        }

        public static async Task InitializeAsync(AsyncPackage package)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);

            var commandService = await package.GetServiceAsync(typeof(IMenuCommandService))
                as OleMenuCommandService
                ?? throw new InvalidOperationException("IMenuCommandService not available.");

            new RestoreFromFileCommand(package, commandService);
        }

        private void Execute(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            try
            {
                var paths = PickBackupFiles();
                if (paths == null || paths.Length == 0) return;

                var connService = new SsmsConnectionService(_package);
                var dlg = new RestoreDialog(_package, connService, paths);
                dlg.Owner = System.Windows.Application.Current?.MainWindow;
                dlg.ShowDialog();
            }
            catch (Exception ex)
            {
                Logger.Error("Error launching restore dialog", ex);
                MessageBox.Show(
                    $"Failed to open restore dialog:\n{ex.Message}",
                    "SSMS Quick Restore",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        internal static string[]? PickBackupFiles(string? initialPath = null)
        {
            using var dlg = new OpenFileDialog
            {
                Title            = "Select backup file(s)",
                Filter           = "Backup files (*.bak;*.trn)|*.bak;*.trn|All files (*.*)|*.*",
                Multiselect      = true,
                CheckFileExists  = true,
                InitialDirectory = initialPath
                    ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
            };

            return dlg.ShowDialog() == DialogResult.OK ? dlg.FileNames : null;
        }
    }
}
