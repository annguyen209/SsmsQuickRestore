using System;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using SsmsRestoreDrop.Commands;
using SsmsRestoreDrop.DropHandler;
using SsmsRestoreDrop.Logging;
using SsmsRestoreDrop.UI;
using Task = System.Threading.Tasks.Task;

namespace SsmsRestoreDrop
{
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [ProvideBindingPath]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [ProvideToolWindow(typeof(ProgressToolWindow),
        Style      = VsDockStyle.Tabbed,
        Orientation= ToolWindowOrientation.Bottom,
        Transient  = false)]
    // Auto-load as soon as the shell is ready, so the drag-drop handler is wired up
    // without the user having to click the toolbar button first.
    [ProvideAutoLoad(VSConstants.UICONTEXT.ShellInitialized_string, PackageAutoLoadFlags.BackgroundLoad)]
    [Guid(PackageGuids.SsmsRestoreDropPackageString)]
    public sealed class SsmsRestoreDropPackage : AsyncPackage
    {
        protected override async Task InitializeAsync(
            CancellationToken cancellationToken,
            IProgress<ServiceProgressData> progress)
        {
            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            Logger.Initialize(this);
            Logger.Info("SsmsQuickRestore package initializing");

            await RestoreFromFileCommand.InitializeAsync(this);
            await BakFileDropTarget.InitializeAsync(this);

            Logger.Info("SsmsQuickRestore package initialized");
        }
    }
}
