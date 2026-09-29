using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Itim.TRS.InstallerLib.Configuration;
using Itim.TRS.InstallerLib.Data;
using Itim.TRS.InstallerLib.Events;
using System.IO;
using System.Configuration;
using Itim.TRS.InstallerLib.Properties;
using System.Xml.Linq;
using System.Web.Configuration;
using log4net;

namespace Itim.TRS.InstallerLib
{
    public abstract class InstallerBase
    {
        #region Constants
        const string SdtPath = "Applications\\StaticDataTransfer.exe";
        const string StockAuditPath = "Applications\\StockAudit.exe";
        const string StateFileName = "PatchInstall.state";
        const string VersionStateFileName = "Install.state";
        #endregion

        #region Static Members
        private static ILog _logger = LogManager.GetLogger(typeof(Installer));
        private static bool _dbCheck;
        #endregion

        #region Private Members
        private PatchConfigurationSection _patchConfigSection;
        private List<DirectoryCopier> _copierList;
        #endregion

        #region Property
        public bool DBFlag
        {
            get { return _dbCheck; }
        }
        #endregion

        #region Event Declarations
        public event EventHandler<ProgressEventArgs> OnProgressChanged;
        public event EventHandler<NotificationEventArgs> OnNotify;
        public event EventHandler<UserDrivenNotificationEventArgs> OnUserDrivenNotify;
        #endregion

        public InstallerBase(string source, string destination)
        {
            SourceDir = source;
            InstallationDir = destination;
            _copierList = new List<DirectoryCopier>();
        }

        #region Public Methods

        /// <summary>
        /// Validates patch version
        /// </summary>
        public bool RunPreChecks()
        {
            bool isSuccess = false;
            try
            {
                DetectServerInstallationMode();

                RaiseProgressEvent(10, "Setting fix details.");
                FillFixDetails();
                DBManager.ConnectionString = LoadConnectionString();
                DBManager.CustomerConnectionString = LoadCustomerConnectionString();
                LogEvent(NotificationType.Info, String.Format(Resources.INF_CONN_STRING, DBManager.ConnectionString));

                List<string> sqlScripts = _patchConfigSection.SqlScripts;
                List<string> appArtifacts = _patchConfigSection.AppArtifacts;

                LogEvent(NotificationType.Info, String.Format("Script file Count {0}", sqlScripts.Count()));
                foreach (string File in sqlScripts)
                {

                    LogEvent(NotificationType.Info, String.Format("SQL files {0}", File));

                }

                if (sqlScripts != null && sqlScripts.Count() > 0)
                {
                    if (!Directory.Exists(Path.Combine(SourceDir, "Database")) && sqlScripts.Count > 0)
                        throw new ConfigurationErrorsException(String.Format(Resources.ERR_DATABASE_DIR_NOT_FOUND, _patchConfigSection.Type));
                    else
                    {
                        string[] strArr = { "\\" };


                        string databasePath = Path.Combine(SourceDir, "Database");
                        string[] scriptFiles = Directory.GetFiles(databasePath, "*.sql", SearchOption.AllDirectories)
                                .Select(file => Path.GetFileName(file))
                                .ToArray();

                        string[] sqlScriptFileNames = sqlScripts.Select(file => Path.GetFileName(file)).ToArray();

                        foreach (var item in sqlScriptFileNames)
                        {
                            LogEvent(NotificationType.Info, String.Format("SQL files Name {0}", item));
                        }


                        LogEvent(NotificationType.Info, String.Format("Script file Count {0}", scriptFiles.Count()));
                        LogEvent(NotificationType.Info, String.Format("SQL file Count {0}", sqlScripts.Count));

                        if (scriptFiles.Count() == sqlScripts.Count)
                        {
                            foreach (string sql in sqlScriptFileNames)
                            {
                                if (!scriptFiles.Contains(sql))
                                    throw new Exception(String.Format("Script file {0} not found in the {1}.", sql, _patchConfigSection.Type));
                            }
                        }
                        else
                            throw new Exception("Script files list count in manifest does not match with the actual script file count.");
                    }
                }
                if (appArtifacts != null && appArtifacts.Count() > 0)
                {
                    if (!Directory.Exists(Path.Combine(SourceDir, "Applications")) && appArtifacts.Count > 0)
                        throw new ConfigurationErrorsException(String.Format(Resources.ERR_APP_DIR_NOT_FOUND, _patchConfigSection.Type));
                    else
                    {
                        string[] strArr = { "\\" };


                        string appPath = Path.Combine(SourceDir, "Applications");
                        string[] appFiles = Directory.GetFiles(appPath, "*", SearchOption.AllDirectories)
                               .Where(file => Path.GetFileName(file).Contains("."))
                               .Select(file => Path.GetFileName(file))
                               .ToArray();

                        string[] appFileNames = appArtifacts.Select(file => Path.GetFileName(file)).ToArray();

                        foreach (var item in appFileNames)
                        {
                            LogEvent(NotificationType.Info, String.Format("APP files Name {0}", item));
                        }



                        if (appArtifacts.Count() == appFiles.Count())
                        {
                            foreach (string app in appFileNames)
                            {
                                if (!appFiles.Select(a => a.Trim()).Contains(app.Trim()))
                                    throw new Exception(String.Format("App file {0} not found in the {1}.", app, _patchConfigSection.Type));
                            }
                        }
                        else
                            throw new Exception("App files list count in manifest does not match with the actual applications file count.");
                    }
                }



                //if (appArtifacts != null && appArtifacts.Count() > 0)
                //{
                //    string appPath = Path.Combine(SourceDir, "Applications");
                //    string reflexPath = Path.Combine(SourceDir, "Reflex");

                //    if (!Directory.Exists(appPath) && appArtifacts.Count > 0)
                //        throw new ConfigurationErrorsException(String.Format(Resources.ERR_APP_DIR_NOT_FOUND, _patchConfigSection.Type));

                //    Get all application files
                //    string[] appFiles = Directory.GetFiles(appPath, "*", SearchOption.AllDirectories)
                //           .Where(file => Path.GetFileName(file).Contains("."))
                //           .Select(file => Path.GetFileName(file))
                //           .ToArray();

                //    Check if Reflex folder exists, if yes, get all files
                //    if (Directory.Exists(reflexPath))
                //    {
                //        string[] reflexFiles = Directory.GetFiles(reflexPath, "*", SearchOption.AllDirectories)
                //               .Where(file => Path.GetFileName(file).Contains("."))
                //               .Select(file => Path.GetFileName(file))
                //               .ToArray();

                //        Merge Reflex files into appFiles
                //       appFiles = appFiles.Concat(reflexFiles).ToArray();
                //    }

                //    string[] appFileNames = appArtifacts.Select(file => Path.GetFileName(file)).ToArray();

                //    foreach (var item in appFileNames)
                //    {
                //        LogEvent(NotificationType.Info, String.Format("APP files Name {0}", item));
                //    }

                //    if (appArtifacts.Count() == appFiles.Count())
                //    {
                //        foreach (string app in appFileNames)
                //        {
                //            if (!appFiles.Select(a => a.Trim()).Contains(app.Trim()))
                //                throw new Exception(String.Format("App file {0} not found in the {1}.", app, _patchConfigSection.Type));
                //        }
                //    }
                //    else
                //        throw new Exception("App files list count in manifest does not match with the actual applications file count.");
                //}


                isSuccess = true;
            }
            catch (Exception ex)
            {
                LogEvent(NotificationType.Error, ex.Message);
            }
            return isSuccess;
        }

        /// <summary>
        /// Deploy patch contents on App/Web server
        /// </summary>
        public void ApplyPatch()
        {
            try
            {
                string serverName = System.Net.Dns.GetHostName();
                RaiseProgressEvent(0, String.Format("Starting installation on {0}", serverName));

                RaiseProgressEvent(5, "Detecting installation mode.");
                DetectInstallMode();

                DBManager.ConnectionString = LoadConnectionString();
                LogEvent(NotificationType.Info, String.Format(Resources.INF_CONN_STRING, DBManager.ConnectionString));

                RaiseProgressEvent(5, "Verifying installation directory.");
                VerifyInstallDir();

                RaiseProgressEvent(10, "Reading configuration file.");
                FillFixDetails();
                RaiseProgressEvent(40, String.Format("Copying {0} contents.", _patchConfigSection.Type));
                CopyFixContents();
                RaiseProgressEvent(10, "Finalizing installation.");
                FinalizeInstallation();
                RaiseProgressEvent(10, String.Format("{1} applied successfully on {0}", serverName, _patchConfigSection.Type));

                LogEvent(NotificationType.Info, Resources.INF_INSTALL_COMPLETE);
            }
            catch (Exception ex)
            {
                LogEvent(NotificationType.Error, ex.Message);
                RollbackInstallation();
                RaiseProgressEvent(0, "Rollback");
                throw ex;
            }
        }

        /// <summary>
        /// Run database scripts
        /// </summary>
        public void ApplyPatchOnDB()
        {
            RaiseProgressEvent(10, "Verifying prerequisites.");
            _dbCheck = true;
            VerifyPreRequisites();
            _dbCheck = false;

            RaiseProgressEvent(20, "Executing database scripts.");
            ExecuteDatabaseScript();

            SaveFixDetails();
        }


        public void NotifyUser(string message)
        {
            UserDrivenNotificationEventArgs eventArgs = new UserDrivenNotificationEventArgs(NotificationType.Warning, message);
            OnUserDrivenNotify(this, eventArgs);
            if (!eventArgs.CanContinue)
                throw new Exception(Resources.ERR_USER_ABORT);
        }

        public void LogEvent(NotificationType eventType, string message)
        {
            if (eventType > NotificationType.Debug && OnNotify != null)
                OnNotify(this, new NotificationEventArgs(eventType, message));

            switch (eventType)
            {
                case NotificationType.Debug:
                    _logger.Debug(message);
                    break;
                case NotificationType.Info:
                    _logger.Info(message);
                    break;
                case NotificationType.Warning:
                    _logger.Warn(message);
                    break;
                case NotificationType.Error:
                    _logger.Error(message);
                    break;
            }
        }

        public static void LogEvent(string message)
        {
            _logger.Debug(message);
        }

        #endregion

        #region abstract Methods
        /// <summary>
        /// Fill patch details 
        /// </summary>
        protected abstract void FillFixDetails();

        /// <summary>
        /// Persists fix information into database
        /// </summary>
        protected abstract void SaveFixDetails();

        /// <summary>
        /// Verify Pre-requisites
        /// </summary>
        /// <returns></returns>
        protected abstract bool VerifyPreRequisites();

        /// <summary>
        /// Copies fix files/directories locally
        /// </summary>
        /// <returns></returns>
        protected abstract void CopyFixContents();

        #endregion

        #region Properties

        public string PatchStateFileName
        {
            get { return StateFileName; }
        }

        public string HotfixStateFileName
        {
            get { return VersionStateFileName; }
        }

        public PatchConfigurationSection PatchConfig
        {
            get { return _patchConfigSection; }
            set { _patchConfigSection = value; }
        }

        public string SourceDir { get; set; }
        public string InstallationDir { get; set; }
        public ServerMode TRSInstallationMode { get; set; }
        #endregion

        #region Private Methods
        private void VerifyInstallDir()
        {
            LogEvent(NotificationType.Info, Resources.INF_VERIFY_INSTALLDIR);
            if (!Directory.Exists(InstallationDir))
            {
                throw new Exception(Resources.ERR_INSTALL_DIR_NOT_FOUND);
            }

            if (!File.Exists(Path.Combine(InstallationDir, "UpdateConfig.exe")))
                throw new Exception(Resources.ERR_INSTALLATION_CORRUPT);
        }

        private void DetectServerInstallationMode()
        {
            RaiseProgressEvent(10, InstallationDir);
            TRSInstallationMode = ServerMode.None;
            LogEvent(NotificationType.Info, String.Format("Installation Directory {0}", InstallationDir));
            LogEvent(NotificationType.Info, String.Format("SdtPath {0}", SdtPath));

            if (Directory.Exists(Path.Combine(InstallationDir, "Web")))
                TRSInstallationMode = ServerMode.Web;

            if (File.Exists(Path.Combine(InstallationDir, SdtPath)))
            {
                TRSInstallationMode |= ServerMode.Application;
            }

            if (TRSInstallationMode == ServerMode.None)
                throw new Exception(Resources.ERR_NONE_INSTALLATION_MODE);

            LogEvent(NotificationType.Info, String.Format(Resources.INF_INSTALL_MODE, TRSInstallationMode));
        }

        private string LoadCustomerConnectionString()
        {
            LogEvent(NotificationType.Info, Resources.INF_SEARCH_CON_STRING);
            System.Configuration.Configuration configurationFile=null;
            LogEvent(NotificationType.Info, String.Format("Application Mode {0}", TRSInstallationMode.ToString()));

            if (Utils.HasServerRole(ServerMode.Application, TRSInstallationMode))
            {
                string exeFullPath = Path.Combine(InstallationDir, StockAuditPath);
                LogEvent(NotificationType.Info, String.Format("Reading connection string from {0}", exeFullPath));
                configurationFile = ConfigurationManager.OpenExeConfiguration(exeFullPath);
            }
            else
            {
                LogEvent(NotificationType.Info, String.Format("Web Mode"));
                LogEvent(NotificationType.Info, String.Format(Resources.INF_CONFIG_FILE, "/IR/web.config"));
                try
                {
                    configurationFile = WebConfigurationManager.OpenWebConfiguration("/IR");
                    this.LogEvent(NotificationType.Info, string.Format("WebConfigPath {0}", configurationFile.ToString()));
                }
                catch (Exception)
                {


                }
                //  this.ConfigFilePath = System.Configuration.ConfigurationManager.AppSettings["WebConfigPath"];

                if (configurationFile == null || configurationFile?.FilePath == null)
                {
                    LogEvent(NotificationType.Info, "Falling back to manual configuration path.");
                    var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
                    this.ConfigFilePath = config.AppSettings.Settings["WebConfigPath"]?.Value;

                    if (string.IsNullOrEmpty(this.ConfigFilePath))
                    {
                        LogEvent(NotificationType.Warning, "WebConfigPath is null or empty. Using default path.");
                        this.ConfigFilePath = "F:\\Program Files\\Itim\\The Retail Suite\\Web\\IR\\web.config";
                    }

                    LogEvent(NotificationType.Info, $"Using configuration file path: {this.ConfigFilePath}");
                    configurationFile = OpenConfigFile(this.ConfigFilePath);
                }
            }

            if (configurationFile.ConnectionStrings == null)
                throw new Exception(String.Format(Resources.ERR_MISSING_CONNECTION_STRING_SECTION, configurationFile.FilePath));

            if (configurationFile.ConnectionStrings.ConnectionStrings == null || configurationFile.ConnectionStrings.ConnectionStrings.Count == 0)
                throw new Exception(String.Format(Resources.ERR_MISSING_CATALOGUE_CONNECTION_STRING, configurationFile.FilePath));

            ConnectionStringSettings settings = configurationFile.ConnectionStrings.ConnectionStrings["Customer"];

            if (settings == null)
                throw new Exception(String.Format(Resources.ERR_MISSING_CATALOGUE_CONNECTION_STRING, configurationFile.FilePath));

            return settings.ConnectionString;
        }

        public static System.Configuration.Configuration OpenConfigFile(string configPath)
        {
            var configFile = new FileInfo(configPath);
            var vdm = new VirtualDirectoryMapping(configFile.DirectoryName, true, configFile.Name);
            var wcfm = new WebConfigurationFileMap();
            wcfm.VirtualDirectories.Add("/", vdm);
            return WebConfigurationManager.OpenMappedWebConfiguration(wcfm, "/");
        }

        private string LoadConnectionString()
        {
            LogEvent(NotificationType.Info, String.Format("Installation Mode"));
            LogEvent(NotificationType.Info, Resources.INF_SEARCH_CON_STRING);
            System.Configuration.Configuration configurationFile = null;
            LogEvent(NotificationType.Info, String.Format("Application Mode {0}", TRSInstallationMode.ToString()));

            if (Utils.HasServerRole(ServerMode.Application, TRSInstallationMode))
            {
                string exeFullPath = Path.Combine(InstallationDir, StockAuditPath);
                LogEvent(NotificationType.Info, String.Format("Reading connection string from {0}", exeFullPath));
                configurationFile = ConfigurationManager.OpenExeConfiguration(exeFullPath);
            }
            else
            {
                LogEvent(NotificationType.Info, String.Format("Web Mode"));
                LogEvent(NotificationType.Info, String.Format(Resources.INF_CONFIG_FILE, "/IR/web.config"));
                try
                {
                    configurationFile = WebConfigurationManager.OpenWebConfiguration("/IR");
                    this.LogEvent(NotificationType.Info, string.Format("WebConfigPath {0}", configurationFile.ToString()));
                }
                catch (Exception)
                {


                }
                //  this.ConfigFilePath = System.Configuration.ConfigurationManager.AppSettings["WebConfigPath"];

                if (configurationFile == null || configurationFile?.FilePath == null)
                {
                    LogEvent(NotificationType.Info, "Falling back to manual configuration path.");
                    var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
                    this.ConfigFilePath = config.AppSettings.Settings["WebConfigPath"]?.Value;

                    if (string.IsNullOrEmpty(this.ConfigFilePath))
                    {
                        LogEvent(NotificationType.Warning, "WebConfigPath is null or empty. Using default path.");
                        this.ConfigFilePath = "F:\\Program Files\\Itim\\The Retail Suite\\Web\\IR\\web.config";
                    }

                    LogEvent(NotificationType.Info, $"Using configuration file path: {this.ConfigFilePath}");
                    configurationFile = OpenConfigFile(this.ConfigFilePath);
                }

            }

            if (configurationFile.ConnectionStrings == null)
                throw new Exception(String.Format(Resources.ERR_MISSING_CONNECTION_STRING_SECTION, configurationFile.FilePath));

            if (configurationFile.ConnectionStrings.ConnectionStrings == null || configurationFile.ConnectionStrings.ConnectionStrings.Count == 0)
                throw new Exception(String.Format(Resources.ERR_MISSING_CATALOGUE_CONNECTION_STRING, configurationFile.FilePath));

            ConnectionStringSettings settings = configurationFile.ConnectionStrings.ConnectionStrings["Catalogue"];

            if (settings == null)
                throw new Exception(String.Format(Resources.ERR_MISSING_CATALOGUE_CONNECTION_STRING, configurationFile.FilePath));

            return settings.ConnectionString;
        }

        private int CopyDirectories(DirectoryCopier copier)
        {
            copier.EventLoggingCallback = LogEvent;
            copier.BackupDirPath = GetBackupDirPath(string.Empty);
            int fileCount = copier.Copy(false);
            _copierList.Add(copier);
            return fileCount;
        }
        #endregion

        #region Protected Methods
        protected virtual void DetectInstallMode()
        {
            LogEvent(NotificationType.Info, Resources.INF_DETECT_INSTALL_MODE);
            DetectServerInstallationMode();
        }

        protected virtual void FinalizeInstallation()
        {
            LogEvent(NotificationType.Info, Resources.INF_FINALIZE_INSTALL);
            WriteVersionToStateFile(PatchConfig.Type == "Patch" ? StateFileName : HotfixStateFileName);
        }

        private void WriteVersionToStateFile(string stateFileName)
        {
            string stateFilePath = Path.Combine(InstallationDir, stateFileName);
            using (StreamWriter writer = File.AppendText(stateFilePath))
            {
                writer.WriteLine(_patchConfigSection.Version);
                writer.Close();
            }
        }

        protected virtual void ExecuteDatabaseScript()
        {


            var sqlScripts = _patchConfigSection.SqlScripts;

            if (sqlScripts == null || sqlScripts.Count == 0)
            {
                LogEvent(NotificationType.Warning, "No SQL scripts found.");
                return;
            }

            string databasePatchDir = Path.Combine(SourceDir, "Database");
            LogEvent(NotificationType.Info, String.Format("databasePatchDir: {0}", databasePatchDir));

            // Normalize paths and filter valid scripts
            var normalizedScripts = sqlScripts
                .Where(script => !string.IsNullOrEmpty(script)) // Remove null or empty entries
                .Select(script => script.Replace("\\", "/")) // Normalize paths
                .ToList();


            // Ensure valid grouping
            var groupedScripts = normalizedScripts
                .Where(script => script.Contains('/')) // Only include scripts with a slash
                .GroupBy(script => script.Split('/')[0]) // Group by database name
                .OrderBy(group => group.Key);

            // Define execution order for script types
            var executionOrder = new[] { "PreInstall", "Triggers", "Functions", "SPs", "PostInstall" };

            foreach (var group in groupedScripts)
            {
                string databaseName = group.Key;

                foreach (var type in executionOrder)
                {
                    LogEvent(NotificationType.Info, String.Format("Execution Order: {0}", type));

                    // Filter scripts for the current type
                    var scripts = group
                        .Where(script =>
                        {
                            var parts = script.Split('/');
                            return parts.Length > 1 && parts[1].IndexOf(type, StringComparison.OrdinalIgnoreCase) >= 0;
                        })
                        .OrderBy(script => script); // Optional: Ensure scripts of the same type are sorted

                    LogEvent(NotificationType.Info, String.Format("Execution Filter Order: {0}", type));

                    foreach (var script in scripts)
                    {
                        LogEvent(NotificationType.Info, string.Format(Resources.INF_EXECUTING_SQLSCRIPT, script));

                        // Execute script
                        string scriptPath = Path.Combine(databasePatchDir, script);
                        LogEvent(NotificationType.Info, $"Script Path: {scriptPath}, Database Name: {databaseName}");

                        try
                        {
                            DBManager.ExecuteScript(scriptPath, databaseName);
                            LogEvent(NotificationType.Info, string.Format(Resources.INF_SQLSCRIPT_COMPLETED, script));
                        }
                        catch (Exception ex)
                        {
                            LogEvent(NotificationType.Error, $"Error executing script: {script}. Exception: {ex.Message}");
                            throw;
                        }
                    }
                }
            }
        }





        protected int CopyDirectoryFiles(string patchPath, string installationPath, string fileSearchPattern)
        {
            return CopyDirectories(new DirectoryCopier(patchPath, installationPath, fileSearchPattern));
        }

        protected int CopyDirectoryFiles(string patchPath, string installationPath)
        {
            return CopyDirectories(new DirectoryCopier(patchPath, installationPath));
        }

        private string GetBackupDirPath(string sourcePath)
        {
            string folder = string.Empty;
            string backupPath = string.Empty;

            backupPath = Path.Combine(InstallationDir, String.Format(@"TRSInstaller\InstalledHotfix\{0}\{1}",
                _patchConfigSection.Version, DateTime.Now.ToString("yyyyMMdd hhmmss")));

            if (sourcePath != string.Empty)
            {
                folder = sourcePath.Substring(sourcePath.LastIndexOf(@"\") + 1);
                if (folder != "The Retail Suite")
                    backupPath = Path.Combine(backupPath, folder);
            }
            return backupPath;
        }

        protected int CopyPatchContents(string patchPath, string installationPath)
        {
            CopyPatchContentsToRoot(patchPath, installationPath);
            LogEvent(NotificationType.Info, String.Format("patchPath {0}", patchPath));
            LogEvent(NotificationType.Info, String.Format("installationPath {0}", installationPath));

            DirectoryInfo hotfixPath;
            DirectoryInfo directoryPath;

            if (patchPath.Contains("\\Web"))
            {
                hotfixPath = new DirectoryInfo(patchPath + "\\TRSUI");
                directoryPath = new DirectoryInfo(installationPath + "\\TRSUI");
            }
            else
            {
                hotfixPath = new DirectoryInfo(patchPath + "\\Web\\TRSUI");
                directoryPath = new DirectoryInfo(installationPath + "\\Web\\TRSUI");
            }

            //if (Directory.Exists(hotfixPath.FullName) && Directory.Exists(directoryPath.FullName))
            //{
            //    foreach (DirectoryInfo hotfixPathdirectories in hotfixPath.GetDirectories())
            //    {
            //        foreach (DirectoryInfo directoryPathdirectories in directoryPath.GetDirectories())
            //        {
            //            if (hotfixPathdirectories.Name.ToLower() == directoryPathdirectories.Name.ToLower())
            //            {
            //                if (File.Exists(hotfixPath + "\\" + directoryPathdirectories.Name + "\\Views\\Home\\Index.cshtml"))
            //                {
            //                    DirectoryInfo directoryPathtobedeleted = new DirectoryInfo(directoryPath + "\\" + directoryPathdirectories.Name + "\\wwwroot\\dist");
            //                    files = directoryPathtobedeleted.GetFiles();

            //                }
            //            }
            //        }
            //    }
            //}
            DirectoryCopier copier = new DirectoryCopier(patchPath, installationPath);
            copier.BackupDirPath = GetBackupDirPath(installationPath);
            copier.EventLoggingCallback = LogEvent;
            int fileCount = copier.Copy();
            _copierList.Add(copier);


            return fileCount;
        }

        // This function copies the files on Root level of Retail Suite.
        protected int CopyPatchContentsToRoot(string patchPath, string installationPath)
        {
            int RootFileCount = 0;
            DirectoryInfo hotfixPath;
            DirectoryInfo directoryPath;
            LogEvent(NotificationType.Info, String.Format("patchPath {0}", patchPath));
            LogEvent(NotificationType.Info, String.Format("installationPath {0}", installationPath));
            hotfixPath = new DirectoryInfo(patchPath);
            directoryPath = new DirectoryInfo(installationPath);

            var RootfileList = Directory
                     .GetFiles(patchPath, "*", SearchOption.TopDirectoryOnly)
                      .Select(Path.GetFileName);


            foreach (var item in RootfileList)
            {

                string destFilePath = Path.Combine(directoryPath.FullName, item);
                string CopyFilePath = Path.Combine(hotfixPath.FullName, item);

                if (item == "fix.manifest")
                {
                    continue;
                }

                else if (File.Exists(destFilePath))
                {
                    FileInfo destFile = new FileInfo(destFilePath);
                    File.Delete(destFilePath);
                    File.Copy(CopyFilePath, destFilePath);
                    RootFileCount++;
                }
                else
                {
                    FileInfo destFile = new FileInfo(destFilePath);
                    //File.Delete(destFilePath);
                    File.Copy(CopyFilePath, destFilePath);
                    RootFileCount++;
                }

            }


            return RootFileCount;
        }

        protected void RollbackInstallation()
        {
            if (_copierList.Count > 0)
            {
                LogEvent(NotificationType.Info, Resources.INF_ROLLBACK_START);
                LogEvent(NotificationType.Info, Resources.INF_ROLLBACK_FILE_COPY);
                foreach (DirectoryCopier copier in _copierList)
                {
                    copier.Rollback();
                }
                LogEvent(NotificationType.Info, Resources.INF_ROLLBACK_END);
            }
        }

        protected void RaiseProgressEvent(int incrementPercentage, string progressMessage)
        {
            if (OnProgressChanged != null)
                OnProgressChanged(this, new ProgressEventArgs(incrementPercentage, progressMessage));
        }
        #endregion

        public string ConfigFilePath { get; set; }
    }
}
