using EnvDTE;
using EnvDTE80;
using Microsoft;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using NLog;
using NLog.Config;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using Task = System.Threading.Tasks.Task;

namespace VSIX_Test
{
    /// <summary>
    /// This is the class that implements the package exposed by this assembly.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The minimum requirement for a class to be considered a valid package for Visual Studio
    /// is to implement the IVsPackage interface and register itself with the shell.
    /// This package uses the helper classes defined inside the Managed Package Framework (MPF)
    /// to do it: it derives from the Package class that provides the implementation of the
    /// IVsPackage interface and uses the registration attributes defined in the framework to
    /// register itself and its components with the shell. These attributes tell the pkgdef creation
    /// utility what data to put into .pkgdef file.
    /// </para>
    /// <para>
    /// To get loaded into VS, the package must be referred by <Asset Type="Microsoft.VisualStudio.VsPackage" .../> in .vsixmanifest file.
    /// </para>
    /// </remarks>
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [ProvideAutoLoad(UIContextGuids.SolutionExists, PackageAutoLoadFlags.BackgroundLoad)]
    [Guid(PackageGuidString)]
    public sealed class VSIX_TestPackage : AsyncPackage
    {
        /// <summary>
        /// VSIX_TestPackage GUID string.
        /// </summary>
        public const string PackageGuidString = "9e80e988-de20-4561-b2ff-9c9436df40b1";

        public static Logger logger;

        #region Package Members

        /// <summary>
        /// Initialization of the package; this method is called right after the package is sited, so this is the place
        /// where you can put all the initialization code that rely on services provided by VisualStudio.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token to monitor for initialization cancellation, which can occur when VS is shutting down.</param>
        /// <param name="progress">A provider for progress updates.</param>
        /// <returns>A task representing the async work of package initialization, or an already completed task if there is none. Do not return null from this method.</returns>
        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            progress.Report(new ServiceProgressData("Configuring NLog..."));

            ConfigureNLog();

            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
        }

        private void ConfigureNLog()
        {
            ConfigurationItemFactory.Default.Targets.RegisterDefinition("OutputWindow", typeof(LogWindow));

            var config = new LoggingConfiguration();
            NLog.Targets.AsyncTaskTarget outputWindowTarget = new LogWindow(this);
            outputWindowTarget.Layout = "${longdate}|${level:uppercase=true}|${callsite:captureStackTrace:False}|${message}|${exception:format=@:innerFormat=@:maxInnerExceptionLevel=1}";
            
            config.AddRule(LogLevel.Trace, LogLevel.Fatal, outputWindowTarget);

            LogManager.Configuration = config;

            logger = LogManager.GetLogger("OutputWindow");
        }

        #endregion
    }
}
