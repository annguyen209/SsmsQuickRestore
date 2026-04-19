using System;
using System.IO;
using System.Linq;
using System.Windows;
using Microsoft.VisualStudio.Shell;
using SsmsRestoreDrop.Logging;
using SsmsRestoreDrop.Services;
using SsmsRestoreDrop.UI;
using Task = System.Threading.Tasks.Task;

namespace SsmsRestoreDrop.DropHandler
{
    /// <summary>
    /// Intercepts .bak / .trn file drops on the SSMS main window.
    ///
    /// Uses WPF PreviewDrop / PreviewDragOver (tunneling events, root → target) so our
    /// handler fires BEFORE SSMS's own editor drop handler that opens files as plain text.
    /// Setting e.Handled = true prevents SSMS from processing the drop further.
    /// </summary>
    internal sealed class BakFileDropTarget : IDisposable
    {
        private readonly AsyncPackage   _package;
        private Window?                 _mainWindow;
        private DragEventHandler?       _dropHandler;

        private BakFileDropTarget(AsyncPackage package) => _package = package;

        public static async Task InitializeAsync(AsyncPackage package)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);
            try
            {
                var handler = new BakFileDropTarget(package);
                handler.Attach();
                Logger.Info("BakFileDropTarget attached via WPF PreviewDrop");
            }
            catch (Exception ex)
            {
                Logger.Warn($"BakFileDropTarget.InitializeAsync failed: {ex.Message}");
            }
        }

        private void Attach()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            _mainWindow = Application.Current?.MainWindow;
            if (_mainWindow == null)
            {
                Logger.Warn("BakFileDropTarget: MainWindow not available - drag-drop disabled");
                return;
            }

            _mainWindow.AllowDrop = true;
            _mainWindow.PreviewDragOver += OnPreviewDragOver;

            _dropHandler = OnPreviewDrop;
            _mainWindow.PreviewDrop += _dropHandler;
        }

        private static void OnPreviewDragOver(object sender, DragEventArgs e)
        {
            if (GetBakFiles(e.Data) != null)
            {
                e.Effects = DragDropEffects.Copy;
                e.Handled = true;
            }
        }

        private void OnPreviewDrop(object sender, DragEventArgs e)
        {
            var files = GetBakFiles(e.Data);
            if (files == null) return;

            e.Handled = true;

            ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                try
                {
                    var connService = new SsmsConnectionService(_package);
                    var dlg = new RestoreDialog(_package, connService, files);
                    dlg.Owner = _mainWindow;
                    dlg.ShowDialog();
                }
                catch (Exception ex)
                {
                    Logger.Error("PreviewDrop handler failed", ex);
                }
            });
        }

        private static string[]? GetBakFiles(IDataObject? data)
        {
            if (data == null) return null;
            if (!data.GetDataPresent(DataFormats.FileDrop)) return null;
            var files = data.GetData(DataFormats.FileDrop) as string[];
            var accepted = files?
                .Where(f => string.Equals(Path.GetExtension(f), ".bak", StringComparison.OrdinalIgnoreCase)
                         || string.Equals(Path.GetExtension(f), ".trn", StringComparison.OrdinalIgnoreCase))
                .ToArray();
            return accepted?.Length > 0 ? accepted : null;
        }

        public void Dispose()
        {
            if (_mainWindow != null)
            {
                _mainWindow.PreviewDragOver -= OnPreviewDragOver;
                if (_dropHandler != null)
                    _mainWindow.PreviewDrop -= _dropHandler;
            }
        }
    }
}
