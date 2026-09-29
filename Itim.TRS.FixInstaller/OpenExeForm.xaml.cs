#region Modification History
// *******************************************************************************
// Module       : OpenExeForm.xaml.cs
// Created By   : Mufaddal Shafqat
// Created On   : 27/12/2012 
// Description  : Call windows service to open an exe file 
//
// **************************** Modification History *****************************

#endregion

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using System.ServiceModel;
using System.Threading;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Threading;

using Itim.TRS.InstallerLib;
using Itim.TRS.InstallerLib.Data;
using Itim.TRS.FixInstaller.TRSFixInstallerServiceRef;
using System.Windows.Media;


namespace FixInstaller
{
    /// <summary>
    /// Interaction logic for ScriptExecuter.xaml
    /// </summary>
    public partial class MainWindow : Window
    {

        #region Instance Member Variables
        private log4net.ILog _log;
        private string _trsFixFilePath;
        private string _trsFixFileName;
        private string _installedFixDir;
        private List<string> _logMessage;
        private delegate void SetButtonDelegate();
        #endregion

        #region Constants
        private const string InstalledHotfixDir = "InstalledHotfix";
        private const string TempDir = "TempFix";
		const string InstallationFeatures = "FeaturesTemp.wxs";
        #endregion

        #region Constructor
        public MainWindow(string fileName, log4net.ILog log)
        {
			_log = log;

            InitializeComponent();
            System.Windows.Forms.Screen src = System.Windows.Forms.Screen.PrimaryScreen;

            _logMessage = new List<string>();
                LogInfo(fileName);

                _trsFixFilePath = fileName;
            _installedFixDir = Path.Combine(Utils.GetTRSInstalledPath(), @"TRSInstaller", InstalledHotfixDir);
                _trsFixFileName = fileName.Substring(fileName.LastIndexOf(@"\") + 1);

            //LogInfo(String.Format("{1}Hotfix installation path : {0}", _installedFixDir, Environment.NewLine));
            LogInfo(String.Format("{0}Ready to install.{0}", Environment.NewLine));
            LogInfo(String.Format("Click Install to continue with the installation or Cancel to exit setup.{0}{0}", Environment.NewLine));
        }
        #endregion

        #region Event Handlers


        private void btnMinimize_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        /// <summary>
        /// Enables the form to drag
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Grid_MouseDown(object sender, MouseButtonEventArgs e)
        {
            this.DragMove();
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void btnInstall_Click(object sender, RoutedEventArgs e)
        {
       
            btnCancel.Visibility = Visibility.Hidden;
            btnInstall.Visibility = Visibility.Hidden;

            InstallFix();
        }
               
        void deployer_OnUserDrivenNotify(object sender, Itim.TRS.InstallerLib.Events.UserDrivenNotificationEventArgs e)
        {
            if (System.Windows.Forms.MessageBox.Show(e.Message + Environment.NewLine + " Do you wish to continue?", "Warning", MessageBoxButtons.YesNo) == System.Windows.Forms.DialogResult.Yes)
            {
                e.CanContinue = true;
            }
        }

        void deployer_OnProgressChanged(object sender, ProgressEventArgs e)
        {
            AdvanceProgress(e);
        }

        private void txtLog_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            txtLog.ScrollToEnd();
        }

        #endregion

        #region Public Methods
        /// <summary>
        /// Copy, uncompress the patch contents on a shared location (InstalledHotfix directory) 
        /// and then applies patch
        /// </summary>
        public void InstallFix()
        {
            try
            {
               
                // If TempFix folder already exists then return
                if (Directory.Exists(Path.Combine(_installedFixDir, TempDir)))
                {
                    LogInfo("Installation already in progress", true);
                    return;
                }
                ApplyPatch();

            }
            catch (Exception ex)
            {
                LogInfo(ex.Message, true);
            }
        }
        #endregion

        #region Private Methods
        private void ApplyPatch()
        {
           
            Thread th = new Thread(new ThreadStart(ExecuteInstaller));
			th.Start();
        }

        private void ExecuteInstaller()
        {
            
            string tempDirPath = Path.Combine(_installedFixDir, TempDir);
            string zipFile = Path.Combine(_installedFixDir, TempDir + ".zip");
          
            ExtractPatch(zipFile);

                        Installer deployer = new Installer(tempDirPath, Utils.GetTRSInstalledPath());
                        deployer.OnNotify += new EventHandler<NotificationEventArgs>(deployer_OnNotify);
                        deployer.OnProgressChanged += new EventHandler<ProgressEventArgs>(deployer_OnProgressChanged);
                        deployer.OnUserDrivenNotify += new EventHandler<Itim.TRS.InstallerLib.Events.UserDrivenNotificationEventArgs>(deployer_OnUserDrivenNotify);

            try
            {
                
                string[] logMessage;

                        if (deployer.RunPreChecks())
                        {
               
                    List<Server> servers = deployer.GetTRSServers();
                            if (servers.Count == 0)
                                throw new Exception("No servers found to deploy the fix.");

                            deployer.ApplyPatchOnDB();

                            string featureXML = GetInstallationFeatures(tempDirPath);

                            foreach (Server server in servers)
                            {
                                try
                                {
                          
                            TRSFixServiceClient trsFixServClient = ConfigureTRSFixServiceProxy(server.IP);
                                    logMessage = trsFixServClient.InstallFix(Path.Combine(@"\\", System.Net.Dns.GetHostName(), InstalledHotfixDir), featureXML);
                                    LogInfo(Environment.NewLine);
                                    foreach (string msg in logMessage)
                                    {
                                        //Use StartsWith method because TRSFixService does not return Failed/Successfull error message indicator
                                        LogInfo(msg, msg.StartsWith("Failed to install on"));
                                    }

                                    AdvanceProgress(new ProgressEventArgs(60 / servers.Count(), ""));
                                }
                                catch (Exception ex)
                                {
                                    LogInfo(String.Format("TRS Fix Installer service is not running on server {0}. " +
                                                    "Please start the service. Exception :{1}", server.IP, ex.Message), true);
                                }
                            }
                        }
                        else
                            throw new Exception("Setup manifest is corrupt or contents doesn't comply with it.");
                    }
                    catch (Exception ex)
                    {
                        if (!ex.Message.Contains("SOAP action"))
                            LogInfo(String.Format("Exception : {0}", ex.Message), true);
                    }
                    finally
                    {
                        File.Delete(zipFile);
                        Directory.Delete(tempDirPath, true);
                        SetButtonDelegate del = delegate()
                        { this.btnCancel.Content = "OK"; this.btnCancel.Visibility = Visibility.Visible; };
                        this.Dispatcher.BeginInvoke(DispatcherPriority.Send, del);
                    }
                }


        void deployer_OnNotify(object sender, NotificationEventArgs e)
        {
            WriteToLog(e.Type, e.Message);
        }

        private void WriteToLog(NotificationType notificationType, string message)
        {
            if (notificationType == NotificationType.Error)
                LogInfo(message + Environment.NewLine + "Error in installation", true);

            LogInfo(message);
            //Application.DoEvents();
        }

		/// <summary>
		/// Gets Installed TRS features
		/// </summary>
		/// <returns></returns>
		private string GetInstallationFeatures(string sourcePath)
		{
			XDocument featuresDoc = new XDocument(new XElement("Features"));
			if (File.Exists(Path.Combine(sourcePath, InstallationFeatures)))
			{
				XElement featureXML = XElement.Load(Path.Combine(sourcePath, InstallationFeatures));
				XNamespace xmlNS = "http://schemas.microsoft.com/wix/2006/wi";
				List<XAttribute> files = (featureXML.Descendants(xmlNS + "File").ToList()).Attributes("Source").ToList();

				foreach (XAttribute file in files)
				{
					XElement feature = new XElement("feature");
					feature.SetValue(file.Value.Substring(file.Value.LastIndexOf(@"\") + 1));
					featuresDoc.Root.Add(feature);
				}
			}
			return featuresDoc.ToString();
		}

        private void AdvanceProgress(ProgressEventArgs e)
        {
            this.Dispatcher.Invoke((Action)(() =>
            {
                this.progBar.Value += e.IncrementAmount;
            }));
            LogInfo(e.Description);
        }

        private TRSFixServiceClient ConfigureTRSFixServiceProxy(string serverIP)
        {
            TRSFixServiceClient client = new TRSFixServiceClient();
            if (client.Endpoint == null)
                throw new Exception("End point not found");

            client.Endpoint.Address = new EndpointAddress(client.Endpoint.Address.Uri.ToString().Replace("localhost", serverIP));

            return client;
        }

        private void LogInfo(string msg, bool isError = false)
        {
			this.Dispatcher.Invoke((Action)(() =>
			{
                this.txtLog.Document.Blocks.Add(new Paragraph(new Run(msg)
                {
                    Foreground = isError ? Brushes.Red : Brushes.White
                }));
			}));
            _log.Info(msg);
        }

		private void ExtractPatch(string zipFile)
		{
            LogInfo("This is the 5th logging");
            LogInfo("Extracting fix contents.");
            LogInfo("Extracting fix contents."+ _installedFixDir);
            LogInfo("Extracting fix contents." + _trsFixFilePath);
            LogInfo("Extracting fix contents." + zipFile);
            if (!Directory.Exists(_installedFixDir))
                LogInfo("Directory Not Exixt");
            Directory.CreateDirectory(_installedFixDir);
            try
            {

                File.Copy(_trsFixFilePath, zipFile);
            }
            catch(Exception ex)
            {
                LogInfo(String.Format("Exception : {0}", ex.Message), true);

            }
            LogInfo("This is the 6th logging");
            // Extracting the patch contents in TempFix folder            
            Compression.UnzipFolder(zipFile, Path.Combine(_installedFixDir, TempDir));
		}
        #endregion        
    }
}