﻿//------------------------------------------------------------------------------
// <copyright file="NuGetDeployCommand.cs" company="Microsoft">
//     Copyright (c) Microsoft.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Xml;
using CnSharp.VisualStudio.Extensions;
using CnSharp.VisualStudio.Extensions.Projects;
using CnSharp.VisualStudio.NuPack.NuGet;
using CnSharp.VisualStudio.NuPack.Util;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Package = Microsoft.VisualStudio.Shell.Package;

namespace CnSharp.VisualStudio.NuPack.Commands
{
    /// <summary>
    /// Command handler
    /// </summary>
    internal sealed class NuGetDeployCommand
    {
        /// <summary>
        /// Command ID.
        /// </summary>
        public const int CommandId = 256;

        /// <summary>
        /// Command menu group (command set GUID).
        /// </summary>
        public static readonly Guid CommandSet = new Guid("eadcfc27-6b6c-4ea4-bd28-13f77827468d");

        private  Project _project;
        /// <summary>
        /// VS Package that provides this command, not null.
        /// </summary>
        private readonly Package package;
        private  NuGet.Package _nupack;
        private XmlDocument _xmlDoc;
        private string _dir;
        private string _nuspecFile;
        private  ProjectAssemblyInfo _assemblyInfo;

        /// <summary>
        /// Initializes a new instance of the <see cref="NuGetDeployCommand"/> class.
        /// Adds our command handlers for menu (commands must exist in the command table file)
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        private NuGetDeployCommand(Package package)
        {
            if (package == null)
            {
                throw new ArgumentNullException("package");
            }

            this.package = package;

            OleMenuCommandService commandService = this.ServiceProvider.GetService(typeof(IMenuCommandService)) as OleMenuCommandService;
            if (commandService != null)
            {
                var menuCommandID = new CommandID(CommandSet, CommandId);
                var menuItem = new MenuCommand(this.MenuItemCallback, menuCommandID);
                commandService.AddCommand(menuItem);
            }
        }

        /// <summary>
        /// Gets the instance of the command.
        /// </summary>
        public static NuGetDeployCommand Instance
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the service provider from the owner package.
        /// </summary>
        private IServiceProvider ServiceProvider
        {
            get
            {
                return this.package;
            }
        }

        /// <summary>
        /// Initializes the singleton instance of the command.
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        public static void Initialize(Package package)
        {
            Instance = new NuGetDeployCommand(package);
        }

        /// <summary>
        /// This function is the callback used to execute the command when the menu item is clicked.
        /// See the constructor to see how the menu item is associated with this function using
        /// OleMenuCommandService service and MenuCommand class.
        /// </summary>
        /// <param name="sender">Event sender.</param>
        /// <param name="e">Event args.</param>
        private void MenuItemCallback(object sender, EventArgs e)
        {
            var dte = Host.Instance.Dte2;
            _project = dte.GetActiveProejct();
            Common.CheckTfs(_project);

            _dir = _project.GetDirectory();
            _nuspecFile = Path.Combine(_dir, NuGetDomain.NuSpecFileName);
            if (!File.Exists(_nuspecFile))//todo:maybe a .net core prj.
            {
                var dr = VsShellUtilities.ShowMessageBox(this.ServiceProvider, $"Miss {NuGetDomain.NuSpecFileName} file,would you add it now?","Warning", 
                    OLEMSGICON.OLEMSGICON_WARNING, OLEMSGBUTTON.OLEMSGBUTTON_YESNO, OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
                if(dr != 6)
                    return;
                new AddNuSpecCommand().Execute();
            }

            _xmlDoc = new XmlDocument();
            _xmlDoc.Load(_nuspecFile);
            var xml = _xmlDoc.InnerXml;
            _nupack = XmlSerializerHelper.LoadObjectFromXmlString<NuGet.Package>(xml);


            try
            {
                _assemblyInfo = _project.GetProjectAssemblyInfo();

                if (_nupack.Metadata.Id.IsEmptyOrPlaceHolder() && !string.IsNullOrWhiteSpace(_assemblyInfo.Title))
                {
                    _nupack.Metadata.Id = _assemblyInfo.Title;
                }
                if (_nupack.Metadata.Title.IsEmptyOrPlaceHolder() && !string.IsNullOrWhiteSpace(_assemblyInfo.Title))
                {
                    _nupack.Metadata.Title = _assemblyInfo.Title;
                }
                if (_nupack.Metadata.Authors.IsEmptyOrPlaceHolder() && !string.IsNullOrWhiteSpace(_assemblyInfo.Company))
                {
                    _nupack.Metadata.Authors = _assemblyInfo.Company;
                }
                if (_nupack.Metadata.Owners.IsEmptyOrPlaceHolder() && !string.IsNullOrWhiteSpace(_assemblyInfo.Company))
                {
                    _nupack.Metadata.Owners = _assemblyInfo.Company;
                }
                if (_nupack.Metadata.Description.IsEmptyOrPlaceHolder() && !string.IsNullOrWhiteSpace(_assemblyInfo.Description))
                {
                    _nupack.Metadata.Description = _assemblyInfo.Description;
                }

            }
            catch (FileNotFoundException ex)
            {
                _assemblyInfo = null;
            }
          

            //MergePackagesConfig();

            var form = new DeployWizard(_assemblyInfo,_nupack,_xmlDoc);
            if(form.ShowDialog() == DialogResult.OK)
                form.SaveAndBuild();
        }

        private void MergePackagesConfig()
        {
            var file = Path.Combine(Path.GetDirectoryName(_project.FileName), "packages.config");
            if (!File.Exists(file))
                return;
            var reader = new PackagesConfigReader(file);
            var packages = reader.GetDependencies().Where(m => !m.DevelopmentDependency).ToList();
            _nupack.Metadata.MergeDependency(packages);
        }

    }
}