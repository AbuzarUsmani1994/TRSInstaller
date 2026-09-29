#region Modification History
// *******************************************************************************
// Module       : OpenExeService.cs 
// Created By   : Muhammad Ahsan Mustafa 
// Created On   : 10/14/2012 
// Description  : Host a WCF Service in a Managed Windows Service for C# language 
// **************************** Modification History ***************************
// Who         Date        Description
// *******************************************************************************
// mshafqat    15/01/2013  Modified exceptions


#endregion


using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using System.ServiceModel;
using System.ServiceProcess;
using System.Configuration;
using System.Configuration.Install;
using System.Diagnostics;
using Microsoft.Win32;
using System.IO;
using TRSPatch = Itim.TRS.InstallerLib;


namespace Itim.TRS.FixInstaller.Service
{
    /// <summary>
    /// Define a service contract.
    /// </summary>
    [ServiceContract]
    public interface ITRSFixService
    {
        [OperationContract]
        List<string> InstallFix(string filepath, string features);


    }
    #region Classes
    /// <summary>
    /// Implement the IOpenExeService service contract in a service class.
    /// </summary>
    public class TRSFixService : ITRSFixService
    {
        #region Instance Member Variables
        private log4net.ILog _log;
        private List<string> _logMessages;
        #endregion

        #region Constructors
        public TRSFixService()
        {
            _log = null;
        }
        #endregion

        #region Methods
        
        /// <summary>
        /// Deploy fix locally
        /// </summary>
        /// <param name="source">fix source directory</param>
        /// <param name="logMessages">log Messages</param>
        /// <returns></returns>
        public List<string> InstallFix(string source, string features)
        {   
            _log = log4net.LogManager.GetLogger(typeof(TRSFixService));
            _logMessages = new List<string>();
            
            try
            {   
                string patchFolder = Path.Combine(source, "TempFix");
                
                if (!Directory.Exists(patchFolder))
                    throw new Exception("Installation folder not found.");

                _log.Info(String.Format("Installation Path {0}", TRSPatch.Utils.GetTRSInstalledPath()));
				TRSPatch.Installer._features = features;
                TRSPatch.Installer trsDeployer = new TRSPatch.Installer(patchFolder, TRSPatch.Utils.GetTRSInstalledPath());                
                trsDeployer.OnProgressChanged += new EventHandler<TRSPatch.ProgressEventArgs>(trsDeployer_OnProgressChanged);
                _log.Info("Applying");
                trsDeployer.ApplyPatch();
            }
            catch (Exception ex)
            {
                _logMessages.Add(ex.Message);
                if (!(ex.Message.Contains("already installed")) && !(ex.Message.Contains("not available")))
                {
                    _logMessages.Add(String.Format("Failed to install on {0}, please check log file for further details.", System.Net.Dns.GetHostName()));
                    _log.Info("An error has occured.");
                    _log.Error(ex);
                    _log.Error(ex.InnerException);
                }
                else
                    _logMessages.Add(String.Format("Please check log file for further details on {0}.", System.Net.Dns.GetHostName()));
            }
            return _logMessages;
        }

        void trsDeployer_OnProgressChanged(object sender, TRSPatch.ProgressEventArgs e)
        {
            _logMessages.Add(e.Description);
        }

        #endregion
    }

    public class TRSFixWindowsService : ServiceBase
    {
        #region Instance Member Variables
        private ServiceHost _serviceHost;
        private log4net.ILog _log;
        #endregion
        #region Constuctors
        public TRSFixWindowsService()
        {
            // Name the Windows Service
            ServiceName = "TRSFix Installer Service";
            _serviceHost = null;
            _log = null;
        }
        #endregion

        #region Methods
        /// <summary>
        /// Run OpenExeWindowsService
        /// </summary>
        public static void Main()
        {
            ServiceBase.Run(new TRSFixWindowsService());
        }


        /// <summary>
        /// Start the Windows service.
        /// </summary>
        /// <param name="args"></param>
        protected override void OnStart(string[] args)
        {
            _log = log4net.LogManager.GetLogger(typeof(TRSFixService));

            try
            {
                // Create a ServiceHost for the OpenExeService type and 
                // provide the base address.
                _serviceHost = new ServiceHost(typeof(TRSFixService));
                // Open the ServiceHostBase to create listeners and start 
                // listening for messages.
                _serviceHost.Open();
                _log.Info("Service started successfully");
            }
            catch (Exception ex)
            {
                _log.Info("An exception has occured");
                _log.Error(ex);
            }
        }
        /// <summary>
        /// Stop the windows service
        /// </summary>
        protected override void OnStop()
        {
            _log.Info("Service stopped successfully");
        }
        #endregion
    }

    /// <summary>
    /// Provide the ProjectInstaller class which allows 
    /// the service to be installed by the Installutil.exe tool
    /// </summary>
    [RunInstaller(true)]
    public class ProjectInstaller : Installer
    {
        #region Instance Member Variables
        private ServiceProcessInstaller _process;
        private ServiceInstaller _service;
        #endregion

        #region Constuctors
        public ProjectInstaller()
        {
            _process = new ServiceProcessInstaller();
            _process.Account = ServiceAccount.LocalSystem;
            _service = new ServiceInstaller();
            _service.ServiceName = "TRSFix Installer Service";
            _service.Description = "Deploy trs patch/fix on local server.";
            Installers.Add(_process);
            Installers.Add(_service);
        }
        #endregion
    }
    #endregion
}