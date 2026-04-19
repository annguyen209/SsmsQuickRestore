using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using SsmsRestoreDrop.Commands;
using SsmsRestoreDrop.Services;

namespace SsmsRestoreDrop.UI
{
    [Guid(PackageGuids.ProgressToolWindowString)]
    public sealed class ProgressToolWindow : ToolWindowPane
    {
        private readonly ProgressToolWindowControl _control;

        public ProgressToolWindow() : base(null)
        {
            Caption  = "SSMS Quick Restore - Progress";
            _control = new ProgressToolWindowControl();
            Content  = _control;
        }

        public void Reset(string databaseName)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            _control.Reset(databaseName);
        }

        public void AppendProgress(RestoreProgress progress)
        {
            _control.AppendProgress(progress);
        }

        public void Hide()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            (Frame as IVsWindowFrame)?.Hide();
        }
    }
}
