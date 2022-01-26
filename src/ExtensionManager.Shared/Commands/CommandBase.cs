﻿using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Windows.Forms;
using ExtensionManager.Core.Models.Interfaces;
using ExtensionManager.Core.Services.Interfaces;
using Microsoft.VisualStudio.Shell.Interop;

namespace ExtensionManager
{
    /// <summary>
    /// Defines the common events, methods, properties, and behaviors for all Commands.
    /// </summary>
    public abstract class CommandBase
    {
        /// <summary>
        /// Reference to an instance of an object that implements the
        /// <see cref="T:System.ComponentModel.Design.IMenuCommandService" /> interface.
        /// </summary>
        private readonly IMenuCommandService _commandService;

        /// <summary>
        /// Reference to an instance of an object that implements the
        /// <see cref="T:ExtensionManager.IExtensionService" /> interface.
        /// </summary>
        /// <remarks>
        /// This object is responsible for providing access to information about the set of
        /// currently-installed Visual Studio Marketplace-obtained extensions.
        /// </remarks>
        protected readonly IExtensionService _extensionService;

        /// <summary>
        /// Reference to an instance of an object that implements the
        /// <see cref="T:Microsoft.VisualStudio.Shell.Interop.IVsPackage" /> interface.
        /// </summary>
        /// <remarks>This is the VSPACKAGE in which this extension command is defined.</remarks>
        protected readonly IVsPackage _package;

        /// <summary>
        /// Initializes a new instance of an object that inherits from this class.
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
        protected CommandBase(IVsPackage package,
            IMenuCommandService commandService,
            IExtensionService extensionService)
        {
            _package = package ??
                       throw new ArgumentNullException(nameof(package));
            _commandService = commandService ??
                              throw new ArgumentNullException(
                                  nameof(commandService)
                              );
            _extensionService = extensionService ??
                                throw new ArgumentNullException(
                                    nameof(extensionService)
                                );
        }

        /// <summary>
        /// Gets a reference to an instance of an object that implements the
        /// <see cref="T:System.IServiceProvider" /> interface, which plays the role of the
        /// VSPACKAGE this extension is embedded in.
        /// </summary>
        protected IServiceProvider ServiceProvider
            => (IServiceProvider)_package;

        /// <summary>
        /// Gets a collection of instances of objects that implement
        /// <see cref="T:ExtensionManager.Core.Models.Interfaces.IExtension" /> that
        /// represent those extensions that are currently installed into Visual Studio.
        /// </summary>
        /// <remarks>
        /// Only those extensions that have been obtained from the Visual Studio
        /// Marketplace are listed.
        /// </remarks>
        protected IEnumerable<IExtension> InstalledExtensions
            => _extensionService.GetInstalledExtensions();

        /// <summary>
        /// Adds the menu command with the specified <paramref name="handler" />,
        /// <paramref name="menuGroupGuid" />, and <paramref name="commandID" /> to the
        /// Visual Studio menus.
        /// </summary>
        /// <param name="handler">
        /// (Required.) An <see cref="T:System.EventHandler" /> that
        /// specifies the code to be executed when the user chooses the command from the
        /// menu.
        /// </param>
        /// <param name="menuGroupGuid">
        /// (Required.) A <see cref="T:System.Guid" /> value
        /// (other than the Zero GUID) that references the menu group to which to add the
        /// new command.
        /// </param>
        /// <param name="commandID">
        /// (Required.) An integer ID for the new command.
        /// <para />
        /// Must be one or greater.
        /// </param>
        /// <exception cref="T:System.ArgumentNullException">
        /// Thrown if the required
        /// parameter, <paramref name="handler" />, is passed a <see langword="null" />
        /// value.
        /// </exception>
        /// <exception cref="T:System.ArgumentOutOfRangeException">
        /// Thrown if either the <paramref name="menuGroupGuid" /> is the Zero GUID, or
        /// <paramref name="commandID" /> is zero or negative.
        /// </exception>
        protected void AddCommandToVisualStudioMenus(EventHandler handler,
            Guid menuGroupGuid, int commandID)
        {
            _commandService?.AddCommand(
                CreateMenuCommandFor(
                    handler, CreateCommandIDFor(menuGroupGuid, commandID)
                )
            );
        }

        /// <summary>
        /// Creates a new instance of
        /// <see cref="T:System.ComponentModel.Design.CommandID" />, initialized with the
        /// specified <paramref name="menuGroupGuid" /> and <paramref name="commandID" />
        /// number, and returns a reference to it.
        /// </summary>
        /// <param name="menuGroupGuid">
        /// (Required.) A <see cref="T:System.Guid" /> value
        /// (other than the Zero GUID) that references the menu group to which to add the
        /// new command.
        /// </param>
        /// <param name="commandID">
        /// (Required.) An integer ID for the new command.
        /// <para />
        /// Must be one or greater.
        /// </param>
        /// <returns>
        /// If valid inputs are passed, then a reference to the
        /// newly-created-and-initialized
        /// <see cref="T:System.ComponentModel.Design.CommandID" /> instance.
        /// </returns>
        /// <exception cref="T:System.ArgumentOutOfRangeException">
        /// Thrown if either the <paramref name="menuGroupGuid" /> is the Zero GUID, or
        /// <paramref name="commandID" /> is zero or negative.
        /// </exception>
        protected static CommandID CreateCommandIDFor(Guid menuGroupGuid,
            int commandID)
        {
            if (Guid.Empty == menuGroupGuid)
                throw new ArgumentOutOfRangeException(
                    nameof(menuGroupGuid),
                    "Component GUID must not be the Zero GUID."
                );
            if (commandID <= 0)
                throw new ArgumentOutOfRangeException(
                    nameof(commandID), "ID must be 1 or greater."
                );

            return new CommandID(menuGroupGuid, commandID);
        }

        /// <summary>
        /// Creates a new instance of
        /// <see cref="T:System.ComponentModel.Design.MenuCommand" /> and initializes it
        /// with the specified event <paramref name="handler" /> and
        /// <paramref name="commandID" />.
        /// </summary>
        /// <param name="handler">
        /// (Required.) An <see cref="T:System.EventHandler" /> that
        /// specifies the code to be executed when the user chooses the command from the
        /// menu.
        /// </param>
        /// <param name="commandID">
        /// (Required.) Reference to an instance of
        /// <see cref="T:System.ComponentModel.Design.CommandID" /> that has been
        /// initialized with the
        /// <see cref="M:ExtensionManager.CommandBase.CreateCommandIDFor" /> method.
        /// </param>
        /// <returns>
        /// Reference to an instance of
        /// <see cref="T:System.ComponentModel.Design.MenuCommand" />, initialized with the
        /// specified <paramref name="handler" /> and <paramref name="commandID" />.
        /// </returns>
        /// <exception cref="T:System.ArgumentNullException">
        /// Thrown if any of the required parameters, <paramref name="handler" />, or
        /// <paramref name="commandID" />, are passed a <see langword="null" /> value.
        /// </exception>
        protected static MenuCommand CreateMenuCommandFor(EventHandler handler,
            CommandID commandID)
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));
            if (commandID == null)
                throw new ArgumentNullException(nameof(commandID));

            return new MenuCommand(handler, commandID);
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
        public abstract void Execute(object sender, EventArgs e);

        protected static bool TryGetFilePath(out string filePath)
        {
            filePath = null;

            using (var sfd = new SaveFileDialog())
            {
                sfd.DefaultExt = ".vsext";
                sfd.FileName = "extensions";
                sfd.Filter = "VSEXT File|*.vsext";

                var result = sfd.ShowDialog();

                if (result != DialogResult.OK) 
                    return false;

                filePath = sfd.FileName;
                return true;
            }
        }
    }
}