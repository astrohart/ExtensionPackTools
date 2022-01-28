﻿using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using EnvDTE;
using ExtensionManager.Core.Models.Interfaces;
using ExtensionManager.Core.Services.Interfaces;
using Microsoft;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.ExtensionManager;
using Microsoft.VisualStudio.Setup.Configuration;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;
using Process = System.Diagnostics.Process;
using Task = System.Threading.Tasks.Task;

namespace ExtensionManager
{
    internal sealed class ImportCommand : CommandBase

    {
        private int _currentCount;

        /// <summary>
        /// Constructs a new instance of
        /// <see cref="T:ExtensionManager.ImportCommand" /> and returns a reference
        /// to it.
        /// </summary>
        /// <param name="package">
        /// (Required.) Reference to an instance of an object that implements the
        /// <see cref="T:Microsoft.VisualStudio.Shell.Interop.IVsPackage" /> interface.
        /// </param>
        /// <param name="commandService">
        /// (Required.) Reference to an instance of an object that implements the
        /// <see cref="T:System.ComponentModel.Design.IMenuCommandService" /> interface.
        /// </param>
        /// <param name="extensionService">
        /// (Required.) Reference to an instance of an object that implements the
        /// <see cref="T:ExtensionManager.IExtensionService" /> interface.
        /// </param>
        /// null
        /// <exception cref="T:System.ArgumentNullException">
        /// Thrown if the any of the
        /// required parameters, <paramref name="package" />,
        /// <paramref name="commandService" />, or <paramref name="extensionService" />,
        /// are passed a <see langword="null" /> value.
        /// </exception>
        private ImportCommand(IVsPackage package,
            IMenuCommandService commandService,
            IExtensionService extensionService) : base(
            package, commandService, extensionService
        )
        {
            /*
             * Call the base class to perform the initialization
             * because similar steps are necessary for all
             * commands.
             */

            AddCommandToVisualStudioMenus(
                Execute, PackageGuids.guidExportPackageCmdSet,
                PackageIds.ImportCmd,
                /* supported = */ false
            );
        }

        public static ImportCommand Instance { get; private set; }

        private int EntriesCount { get; set; }

        /// <summary>
        /// Creates and initializes a new instance of
        /// <see cref="T:ExtensionManager.ImportCommand" /> and sets the
        /// <see cref="P:ExtensionManager.ImportCommand.Instance" /> property to a
        /// reference to it.
        /// </summary>
        /// <param name="package">
        /// (Required.) Reference to an instance of an object that implements the
        /// <see cref="T:Microsoft.VisualStudio.Shell.Interop.IVsPackage" /> interface.
        /// </param>
        /// <param name="commandService">
        /// (Required.) Reference to an instance of an object that implements the
        /// <see cref="T:System.ComponentModel.Design.IMenuCommandService" /> interface.
        /// </param>
        /// <param name="extensionService">
        /// (Required.) Reference to an instance of an object that implements the
        /// <see cref="T:ExtensionManager.IExtensionService" /> interface.
        /// </param>
        /// null
        /// <exception cref="T:System.ArgumentNullException">
        /// Thrown if the any of the
        /// required parameters, <paramref name="package" />,
        /// <paramref name="commandService" />, or <paramref name="extensionService" />,
        /// are passed a <see langword="null" /> value.
        /// </exception>
        public static void Initialize(IVsPackage package,
            IMenuCommandService commandService,
            IExtensionService extensionService)
        {
            Instance = new ImportCommand(
                package, commandService, extensionService
            );
        }

        /// <summary>
        /// Supplies code that is to be executed when the user chooses this command from
        /// menus or toolbars.
        /// </summary>
        /// <param name="sender">Reference to the sender of the event.</param>
        /// <param name="e">
        /// A <see cref="T:System.EventArgs" /> that contains the event
        /// data.
        /// </param>
        public override void Execute(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (!TryGetFilePath(out var filePath)) return;

            var manifest = Manifest.FromFile(filePath);
            manifest.MarkSelected(_extensionService.GetInstalledExtensions());

            var dialog = ImportWindow.Open(manifest.Extensions, Purpose.Import);

            if (dialog.DialogResult != true ||
                !dialog.SelectedExtensions.Any()) return;

            var installSystemWide = dialog.InstallSystemWide;
            var toInstall = dialog.SelectedExtensions.Select(ext => ext.ID)
                                  .ToList();

            var repository =
                ServiceProvider.GetService(typeof(SVsExtensionRepository)) as
                    IVsExtensionRepository;
            if (repository == null) return;
            Assumes.Present(repository);

            var marketplaceEntries = repository
                                     .GetVSGalleryExtensions<GalleryEntry>(
                                         toInstall, 1033, false
                                     )
                                     .Where(x => x.DownloadUrl != null);
            var tempDir = PrepareTempDir();

            var dte = ServiceProvider.GetService(typeof(DTE)) as DTE;
            Assumes.Present(dte);

            dte.StatusBar.Text = "Downloading extensions...";

            HasRootSuffix(out var rootSuffix);
            ThreadHelper.JoinableTaskFactory.Run(
                () => DownloadExtensionsAsync(
                    installSystemWide, marketplaceEntries, tempDir, dte,
                    rootSuffix
                )
            );
        }

        private async Task DownloadExtensionsAsync(bool installSystemWide,
            IEnumerable<GalleryEntry> marketplaceEntries, string tempDir,
            DTE dte, string rootSuffix)
        {
            ServicePointManager.DefaultConnectionLimit = 100;
            EntriesCount = marketplaceEntries.Count();
            _currentCount = 0;
            await DownloadExtensionAsync(marketplaceEntries, tempDir, dte);

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            dte.StatusBar.Text =
                "Extensions downloaded. Starting VSIX Installer...";
            InvokeVsixInstaller(tempDir, rootSuffix, installSystemWide);
        }

        public static bool TryGetFilePath(out string filePath)
        {
            filePath = null;

            using (var sfd = new OpenFileDialog())
            {
                sfd.DefaultExt = ".vsext";
                sfd.FileName = "extensions";
                sfd.Filter = "VSEXT File|*.vsext";

                var result = sfd.ShowDialog();

                if (result == DialogResult.OK)
                {
                    filePath = sfd.FileName;
                    return true;
                }
            }

            return false;
        }

        private static string PrepareTempDir()
        {
            var tempDir = Path.Combine(
                Path.GetTempPath(), nameof(ExtensionManager)
            );

            try
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);

                if (!Directory.Exists(tempDir))
                    Directory.CreateDirectory(tempDir);
            }
            catch
            {
                // ignored
            }

            return tempDir;
        }

        private static void InvokeVsixInstaller(string tempDir,
            string rootSuffix, bool installSystemWide)
        {
            var process = Process.GetCurrentProcess();
            if (!File.Exists(process.MainModule.FileName)) return;

            var dir = Path.GetDirectoryName(process.MainModule.FileName);
            if (!Directory.Exists(dir)) return;

            var exe = Path.Combine(dir, "VSIXInstaller.exe");
            var configuration = new SetupConfiguration() as ISetupConfiguration;
            var adminSwitch = installSystemWide ? "/admin" : string.Empty;
            var instance = configuration.GetInstanceForCurrentProcess();
            var vsixFiles = Directory.EnumerateFiles(tempDir, "*.vsix")
                                     .Select(Path.GetFileName);

            var start = new ProcessStartInfo {
                FileName = exe,
                Arguments =
                    $"{string.Join(" ", vsixFiles)} /instanceIds:{instance.GetInstanceId()} {adminSwitch}",
                WorkingDirectory = tempDir,
                UseShellExecute = false
            };

            /*
             * Are we being asked to install this into the Visual Studio
             * Experimental Instance?  If so, then ensure that this occurs by
             * using the /rootSuffix switch of the VSIX installer.
             */
            if (!string.IsNullOrEmpty(rootSuffix))
                start.Arguments += $" /rootSuffix:{rootSuffix}";

            Process.Start(start);
        }

        private Task DownloadExtensionAsync(IEnumerable<IGalleryEntry> entries,
            string dir, DTE dte)
        {
            return Task.WhenAll(
                entries.Select(
                           entry => Task.Run(
                               () => DownloadExtensionFileAsync(entry, dir, dte)
                           )
                       )
                       .ToArray()
            );
        }

        private async Task DownloadExtensionFileAsync(IGalleryEntry entry,
            string dir, DTE dte)
        {
            if (entry == null) return;
            if (string.IsNullOrWhiteSpace(dir)) return;

            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var localPath = Path.Combine(
                dir, GetMD5HashOf(entry.DownloadUrl) + ".vsix"
            );

            try
            {
                using (var client = new WebClient())
                {
                    await client.DownloadFileTaskAsync(
                        entry.DownloadUrl, localPath
                    );
                }
            }
            catch
            {
                return;
            }

            await UpdateProgressAsync(dte);
        }

        private async Task UpdateProgressAsync(DTE dte)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            dte.StatusBar.Text =
                $"Downloaded {Interlocked.Increment(ref _currentCount)} of {EntriesCount} extensions...";
            await TaskScheduler.Default;
        }

        private static string GetMD5HashOf(string input)
        {
            var result = Guid.NewGuid()
                             .ToString("N");
            try
            {
                using (var md5 = MD5.Create())
                {
                    result = BitConverter.ToString(
                                           md5.ComputeHash(
                                               Encoding.ASCII.GetBytes(input)
                                           )
                                       )
                                       .Replace("-", string.Empty)
                                       .ToLower();
                }
            }
            catch (Exception)
            {
                // ignored

                result = Guid.NewGuid()
                             .ToString("N");
            }
            
            return null;
        }

        public static bool HasRootSuffix(out string rootSuffix)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var result = false;

            rootSuffix = string.Empty;

            try
            {
                if (!(Microsoft.VisualStudio.Shell.ServiceProvider.GlobalProvider
                               .GetService(typeof(SVsAppCommandLine)) is
                        IVsAppCommandLine appCommandLine)) return false;
                if (ErrorHandler.Succeeded(
                        appCommandLine.GetOption(
                            "rootsuffix", out var hasRootSuffix, out rootSuffix
                        )
                    ))
                    result = hasRootSuffix != 0;
            }
            catch (Exception ex)
            {
                result = false;

                rootSuffix = string.Empty;
            }

            return result;
        }
    }
}