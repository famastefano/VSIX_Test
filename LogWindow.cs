using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using NLog;
using System;
using System.Threading;

namespace VSIX_Test
{
    public class LogWindow : NLog.Targets.AsyncTaskTarget
    {
        public static readonly Guid guid = new Guid("E249D774-77F2-4037-A48A-48BD9191E50F");

        AsyncPackage package;

        static IVsOutputWindowPane pane;
        static IVsOutputWindow window;

        public LogWindow(AsyncPackage package)
        {
            this.package = package;

            package.JoinableTaskFactory.Run(InitOutputWindowAsync);
        }

        protected override async System.Threading.Tasks.Task WriteAsyncTask(LogEventInfo logEvent, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
                return;

            //await package.JoinableTaskFactory.SwitchToMainThreadAsync();
            pane.OutputStringThreadSafe(Layout.Render(logEvent));
        }

        private async System.Threading.Tasks.Task InitOutputWindowAsync()
        {
            await package.JoinableTaskFactory.SwitchToMainThreadAsync();

            window = await VS.Windows.GetOutputWindowAsync();
            window.CreatePane(guid, "VSIX Log", 1, 1);
            window.GetPane(guid, out pane);
            pane.Activate();
        }
    }
}
