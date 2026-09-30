using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Itim.TRS.InstallerLib;
using Itim.TRS.InstallerLib.Properties;
using System.IO;
using Itim.TRS.InstallerLib.Events;
using Itim.TRS.InstallerLib.Data;
using Itim.TRS.InstallerLib.Configuration;

namespace Itim.TRS.InstallerLib
{
    public class HotfixInstaller : InstallerBase
    {

        #region Constants
        const string ShutdownExe = "Shutdown.exe";
        const string RFSDLLExe = "rfsdll.exe";
        const string RFGRExe = "rfgr.exe";
        const string RFSInstExe = "RfsInst.exe";
        /// <summary>
        /// Maximum number of files that should be passed as parameters to RFSDLL or rfgr, if the number of files is greater then this value
        /// then execute the command for all. This is done to avoid the command line from getting longer then the allowed limit.
        /// </summary>
        const int ExeFileArgsThreshold = 15;
        #endregion

        #region Private Members
        private InstalledHotfix _hotfixDetails;
        private string _installedReleaseVersion;
       
        #endregion

        public HotfixInstaller(string source, string destination)
            : base(source, destination)
        {
        }

        #region Properties
        public ServerMode ReflexInstallationMode { get; set; }
        #endregion

        #region Protected Methods
        protected override void FillFixDetails()
        {
            _hotfixDetails = new InstalledHotfix();
            _hotfixDetails.HotfixVersion = base.PatchConfig.Version;
            _hotfixDetails.Description = base.PatchConfig.Description;
            _hotfixDetails.ReleaseDate = DateTime.Parse(base.PatchConfig.ReleaseDate);
            _hotfixDetails.DeploymentDate = DateTime.Now;
            
            LogEvent(NotificationType.Info, String.Format(Resources.INF_BEGIN_HOTFIX_INSTALL, _hotfixDetails.HotfixVersion, _hotfixDetails.Description));
        }

        protected override bool VerifyPreRequisites()
        {   
            VerifyReleaseCompatibility();
            VerifyPrerequisiteHotfixes();
            VerifySupportedClients();
            return true;
        }

        protected override void ExecuteDatabaseScript()
        {
            //if the entry for this hotfix already exists then assume that the database script has already been executed on this machine.            
            if (IsHotfixInstalledOnDB())
            {
                LogEvent(NotificationType.Info, String.Format(Environment.NewLine + Resources.INF_HOTFIX_ALREADY_INSTALL, _hotfixDetails.HotfixVersion, "Database"));
                return;
            }
            if (base.PatchConfig.SqlScripts.Count > 0)
                _hotfixDetails.IsForDBServer = true;
            else
                _hotfixDetails.IsForDBServer = false;

            base.ExecuteDatabaseScript();
        }

        protected override void SaveFixDetails()
        {
            
            List<string> newSqlScriptsList = new List<string>();
            List<string> AppFilesList = new List<string>();
            List<string> WebFilesList = new List<string>();
            if (this.PatchConfig.SqlScripts.Count() > 0)
            {
                newSqlScriptsList = PatchConfig.SqlScripts.ToList();
                _hotfixDetails.IsForDBServer = true;
            }
                  
            else
                _hotfixDetails.IsForDBServer = false;

            if (Directory.Exists(Path.Combine(SourceDir, "Web")) && this.PatchConfig.WebArtifacts.Count() > 0)

            {
                WebFilesList = PatchConfig.WebArtifacts
                .Where(file => !file.Equals("Web/TRSUI", StringComparison.OrdinalIgnoreCase) &&
                   !file.Equals("Web/TRSWebAPI", StringComparison.OrdinalIgnoreCase))
                    .ToList();
                _hotfixDetails.IsForWebServer = true;
            }
            else
                _hotfixDetails.IsForWebServer = false;

            if (Directory.Exists(Path.Combine(SourceDir, "Applications")) && this.PatchConfig.AppArtifacts.Count() > 0)
                {
                    AppFilesList = PatchConfig.AppArtifacts
                    .Where(file => file.Contains("."))
                                            .ToList();
                    _hotfixDetails.IsForAppServer = true;
                }

                else
                    _hotfixDetails.IsForAppServer = false;

            // IIS Applications are Web-tier only, so they're recorded in the same WebFilesList
            // bucket (IsForWebServer) as WebArtifacts above - InstalledHotfixDetails.Artifacts has
            // no dedicated columns, so WebsiteName/PhysicalPath/Pool are packed into one string per entry.
            if (this.PatchConfig.IISApplications != null && this.PatchConfig.IISApplications.Count() > 0)
            {
                foreach (IISApplicationConfigElement application in this.PatchConfig.IISApplications)
                {
                    string websiteName = !string.IsNullOrEmpty(application.Path) ? application.Path.TrimStart('/') : application.Path;
                    WebFilesList.Add(String.Format("WebsiteName={0};PhysicalPath={1};Pool={2}",
                        websiteName, application.PhysicalPath, application.ApplicationPool));
                }
                _hotfixDetails.IsForWebServer = true;
            }

            DBManager.SaveHotfix(_hotfixDetails, newSqlScriptsList,AppFilesList, WebFilesList);
        }

        protected override void DetectInstallMode()
        {
            base.DetectInstallMode();
            DetectReflexInstallMode();
        }

        protected override void CopyFixContents()
        {
            LogEvent(NotificationType.Info, Resources.INF_VERIFY_PREREQ);
            VerifyPrerequisiteHotfixes();
            CompareFileAndDatabaseVersion();
            CopyHotfixContents();
        }

        #endregion

        #region Private Methods
        private void CopyHotfixContents()
        {
            int fileCopiedCount = 0;
            int fileCopiedCountOnRoot = 0;
            LogEvent(NotificationType.Info, Resources.INF_COPY_FILES);

            if (ReflexInstallationMode == (TRSInstallationMode & ServerMode.Database))
            {
                fileCopiedCount += InstallReflexComponents();
            }
            if (Utils.HasServerRole(ServerMode.SingleMachine, TRSInstallationMode))
            {
                fileCopiedCount = CopyPatchContents(base.SourceDir, InstallationDir);
            }
            else
            {
                if (Utils.HasServerRole(ServerMode.Web, TRSInstallationMode))
                {
                    string webPatchFolder = Path.Combine(base.SourceDir, "Web");
                    if (Directory.Exists(webPatchFolder))
                    {
                        fileCopiedCount += CopyPatchContents(webPatchFolder, Path.Combine(InstallationDir, "Web"));
                        fileCopiedCountOnRoot+= CopyPatchContentsToRoot(base.SourceDir, InstallationDir);
                    }

                    if (ReflexInstallationMode == ServerMode.Application)
                    {
                        string reflexPatchFolder = Path.Combine(base.SourceDir, "Reflex");
                        string reflexInstallationFolder = Path.Combine(InstallationDir, "Reflex");
                        string system32PatchFolder = Path.Combine(reflexPatchFolder, "System32");

                        if (Directory.Exists(reflexPatchFolder))
                        {
                            fileCopiedCount += CopyPatchContents(reflexPatchFolder, reflexInstallationFolder);
                            fileCopiedCount += base.CopyDirectoryFiles(Path.Combine(reflexPatchFolder, "Executables"),
                                                                                    Path.Combine(reflexInstallationFolder, "Executables"),
                                                                                    RFSInstExe
                                                                                    );                            
                        }
                    }
                }
                else if (Utils.HasServerRole(ServerMode.Application, TRSInstallationMode))
                {
                    string applicationPatchFolder = Path.Combine(base.SourceDir, "Applications");
					string databasePatchFolder = Path.Combine(SourceDir, "Database");

                    if (Directory.Exists(applicationPatchFolder))
                    {
                        fileCopiedCount += CopyPatchContents(applicationPatchFolder, Path.Combine(InstallationDir, "Applications"));
						if (Directory.Exists(databasePatchFolder))
							fileCopiedCount += CopyPatchContents(databasePatchFolder, Path.Combine(InstallationDir, "Database"));
                        fileCopiedCountOnRoot += CopyPatchContentsToRoot(base.SourceDir, InstallationDir);
                    }
                }
           }

            LogEvent(NotificationType.Info, String.Format(Resources.INF_COPY_COMPLETE, fileCopiedCount, fileCopiedCountOnRoot));
        }

        private int InstallReflexComponents()
        {
            int fileCopiedCount = 0;
            string reflexPatchFolder = Path.Combine(base.SourceDir, "Reflex");
            string system32PatchFolder = Path.Combine(reflexPatchFolder, "System32");
            string reflexInstallationFolder = Path.Combine(InstallationDir, "Reflex");
            string reflexExecutablesFolder = Path.Combine(reflexInstallationFolder, "Executables");
            string reflexPatchBinariesFolder = Path.Combine(reflexPatchFolder, "Binaries");
            string reflexPatchReportsFolder = Path.Combine(reflexPatchFolder, "Reports");

            if (Directory.Exists(reflexPatchFolder))
            {
                //Check Reflex retired? i.e No Shutdown.exe available...
                if (!File.Exists(Path.Combine(reflexExecutablesFolder, ShutdownExe)))
                {
                    fileCopiedCount += CopyPatchContents(reflexPatchFolder, reflexInstallationFolder);
                }
                else
                {
                    LogEvent(NotificationType.Info, Resources.INF_SHUTDOWN_REFLEX);
                    Utils.RunExecutable(Path.Combine(reflexExecutablesFolder, ShutdownExe), reflexExecutablesFolder, "/norestart");

                    fileCopiedCount += CopyPatchContents(reflexPatchFolder, reflexInstallationFolder);

                    if (Directory.Exists(system32PatchFolder))
                    {
                        fileCopiedCount += CopyPatchContents(system32PatchFolder, Environment.SystemDirectory);
                    }

                    if (Directory.Exists(reflexPatchBinariesFolder))
                        RegisterReflexCOMPlusComponents(reflexPatchBinariesFolder, reflexInstallationFolder);

                    if (Directory.Exists(reflexPatchReportsFolder))
                        RegisterReflexReports(reflexPatchReportsFolder, reflexInstallationFolder);

                    LogEvent(NotificationType.Info, Resources.INF_RESTART_REFLEX);
                    Utils.RunExecutable(Path.Combine(reflexExecutablesFolder, ShutdownExe), reflexExecutablesFolder, String.Empty);
                }
            }
            return fileCopiedCount;
        }


        private void RegisterReflexCOMPlusComponents(string reflexPatchBinariesFolder, string reflexInstallationFolder)
        {
            string arguments;
            string reflexBinariesFolder = Path.Combine(reflexInstallationFolder, "Binaries");
            string reflexExecutablesFolder = Path.Combine(reflexInstallationFolder, "Executables");
            StringBuilder argumentBuilder = new StringBuilder("/install ");

            string[] reflexDlls = Directory.GetFiles(reflexPatchBinariesFolder);
            if (reflexDlls.Length <= ExeFileArgsThreshold)
            {
                foreach (string DllName in reflexDlls)
                {
                    argumentBuilder.Append(DllName);
                    argumentBuilder.Append(" ");
                }
            }
            else
                argumentBuilder.Append("\"Reflex Components\" \"Reflex Transaction Support\"");

            arguments = argumentBuilder.ToString();

            LogEvent(NotificationType.Info, Resources.INF_RUN_RFSDLL);
            LogEvent(NotificationType.Debug, "rfsdll" + arguments);
            Utils.RunExecutable(Path.Combine(reflexExecutablesFolder, RFSDLLExe), reflexBinariesFolder, argumentBuilder.ToString());

        }

        private void RegisterReflexReports(string reflexPatchReportsFolder, string reflexInstallationFolder)
        {
            string arguments;
            string reflexReportsFolder = Path.Combine(reflexInstallationFolder, "Reports");
            string reflexExecutablesFolder = Path.Combine(reflexInstallationFolder, "Executables");
            StringBuilder argumentBuilder = new StringBuilder("/p=all ");

            string[] reflexReports = Directory.GetFiles(reflexPatchReportsFolder);
            if (reflexReports.Length > ExeFileArgsThreshold)
            {
                foreach (string reportName in reflexReports)
                {
                    argumentBuilder.Append(reportName);
                    argumentBuilder.Append(" ");
                }
            }
            else
                argumentBuilder.AppendFormat("*.rpt");

            arguments = argumentBuilder.ToString();
            LogEvent(NotificationType.Info, Resources.INF_RUN_RFGR);
            LogEvent(NotificationType.Debug, "rfsdll" + arguments);
            Utils.RunExecutable(Path.Combine(reflexExecutablesFolder, RFGRExe), reflexReportsFolder, argumentBuilder.ToString());

        }

        private void DetectReflexInstallMode()
        {
            if (Utils.HasServerRole(ServerMode.SingleMachine, TRSInstallationMode))
                ReflexInstallationMode = ServerMode.SingleMachine;
            else if (Utils.HasServerRole(ServerMode.Web, TRSInstallationMode))
            {
                ReflexInstallationMode = (Directory.Exists(Path.Combine(InstallationDir, "Reflex\\Spool"))) ? ServerMode.Web : ServerMode.Application;
            }
            else if (Utils.HasServerRole(ServerMode.Application, TRSInstallationMode))
            {
                ReflexInstallationMode = (Directory.Exists(Path.Combine(InstallationDir, "Reflex"))) ? ServerMode.Application : ServerMode.Web;
            }

            LogEvent(NotificationType.Info, String.Format(Resources.INF_REFLEX_INSTALL_MODE, ReflexInstallationMode));
        }

        private void VerifyReleaseCompatibility()
        {
            LogEvent(NotificationType.Info, Resources.INF_CHECK_RELEASE_COMPAT);
            _installedReleaseVersion = DBManager.LoadInstalledReleaseVersion();

            List<string> compatibleReleases = base.PatchConfig.CompatibleReleases;

            if (!compatibleReleases.Contains(_installedReleaseVersion))
            {
                string bulletedList = Environment.NewLine + "- " + String.Join(Environment.NewLine + "- ", compatibleReleases.ToArray());
                //TODO : Message needs to be changed for hotfix
                throw new Exception(String.Format(Resources.ERR_MISSING_REQUIRED_RELEASES, _hotfixDetails.HotfixVersion, _installedReleaseVersion, bulletedList, "Hotfix"));
            }
        }

        private bool IsHotfixInstalledOnDB()
        {
            List<InstalledHotfix> dbInstalledHotfixes = DBManager.LoadRequiredHotfixes(new List<string>() { _hotfixDetails.HotfixVersion }, TRSInstallationMode);
            if (dbInstalledHotfixes.Count > 0)
            {
                _hotfixDetails = dbInstalledHotfixes[0];
                return true;
            }
            return false;
        }

        private void VerifyPrerequisiteHotfixes()
        {
            List<InstalledHotfix> dbInstalledHotfixes;

            LogEvent(NotificationType.Info, Resources.INF_CHECK_HOTFIX_COMPAT);

            string versionToInstall = _hotfixDetails.HotfixVersion;
            List<string> hotfixesToLoad = new List<string>(base.PatchConfig.PreRequisites);

            //add current hotfix version to the prerequisite list, so that all the required data can be loaded in a single call
            hotfixesToLoad.Add(versionToInstall);

            //Check if hotfix prerequisite is available for current installation mode.
            if (!DBManager.VerifyPrerequisiteForServer(hotfixesToLoad, TRSInstallationMode))
            {
                dbInstalledHotfixes = DBManager.LoadRequiredHotfixes(hotfixesToLoad, TRSInstallationMode);
            }
            else
            {
                dbInstalledHotfixes = DBManager.LoadRequiredHotfixesForServer(hotfixesToLoad, TRSInstallationMode);
            }
            
            //default is null in this case.
            InstalledHotfix currentHotfix = dbInstalledHotfixes.SingleOrDefault(hotfix => hotfix.HotfixVersion == versionToInstall);
            if (currentHotfix != null)
            {
                // FER - 9476
                //if ((TRSInstallationMode == ServerMode.Application && !currentHotfix.IsForAppServer)
                //            || (TRSInstallationMode == ServerMode.Web && !currentHotfix.IsForWebServer))
                //{
                //    //base.FinalizeInstallation();
                //    //If checking prerequisite for database then donot return.
                //    if (!base.DBFlag)
                //        throw new Exception(String.Format("Hotfix {0} not available for this server.", versionToInstall));
                //    else
                //        return;
                //}
                //else
                    _hotfixDetails = currentHotfix;
            }

            // Check whether hotfix is already installed on server
            string[] fileSystemHotfixes = GetHotfixListFromStateFile();
            if (fileSystemHotfixes.Contains(versionToInstall))
            {
                if (!base.DBFlag)
                    throw new Exception(String.Format(Resources.INF_HOTFIX_ALREADY_INSTALL, versionToInstall, System.Net.Dns.GetHostName()));
                else
                    return;
            }

            //Check if prerequisite is installed or not
            if (!base.DBFlag)
            {
                foreach (string s in PatchConfig.PreRequisites)
                {
                    if (!fileSystemHotfixes.Contains(s))
                    {
                        throw new Exception(String.Format(Resources.ERR_MISSING_PREREQUISITE_HOTFIXES, versionToInstall, s));
                    }
                }
            }

            //at this point we know that the installedHotfixes list contains only prerequisites
            string[] missingHotfixes = base.PatchConfig.PreRequisites.Except(dbInstalledHotfixes.Select(installedHotfix => installedHotfix.HotfixVersion)).ToArray();
            if (missingHotfixes.Length > 0)
            {
                string bulletedList = Environment.NewLine + "- " + String.Join(Environment.NewLine + "- ", missingHotfixes);
                throw new Exception(String.Format(Resources.ERR_MISSING_PREREQUISITE_HOTFIXES, versionToInstall, bulletedList));
            }
        }

        private string[] GetHotfixListFromStateFile()
        {
            string stateFilePath = Path.Combine(InstallationDir, base.HotfixStateFileName);
            CreateEmptyFile(stateFilePath);

            string[] fileSystemHotfixes = File.ReadAllLines(stateFilePath);
            return fileSystemHotfixes;
        }

        private void VerifySupportedClients()
        {
            if (base.PatchConfig.SupportedClients.Count > 0)
            {
                LogEvent(NotificationType.Info, Resources.INF_CHECK_SUPPORTED_CLIENTS);

                List<string> supportedClients = base.PatchConfig.SupportedClients;

                string customerName = DBManager.GetCustomerName();

                if (!supportedClients.Contains(customerName))
                    throw new Exception(String.Format(Resources.ERR_CUSTOMER_NOT_SUPPORTED, customerName, "Hotfix"));
            }
        }

        private void CompareFileAndDatabaseVersion()
        {
            LogEvent(NotificationType.Info, Resources.INF_CHECK_FS_DB_SYNCH);

            bool isOutOfSync = false;            
            string[] dbHotfixes = DBManager.LoadAllHotfixesForServerRole(Utils.HasServerRole(ServerMode.Web, TRSInstallationMode),
                                                                            Utils.HasServerRole(ServerMode.Application, TRSInstallationMode)
                ).Where(h => h.HotfixVersion != _hotfixDetails.HotfixVersion).Select(h => h.HotfixVersion).ToArray();
            
                        
            string[] fileSystemHotfixes = GetHotfixListFromStateFile();

            IEnumerable<string> missingHotfixesOnDb = fileSystemHotfixes.Except(dbHotfixes);
            IEnumerable<string> missingHotfixesOnFileSystem = dbHotfixes.Except(fileSystemHotfixes);

            StringBuilder messageBuffer = new StringBuilder(Resources.WRN_DB_FILE_OUTOFSYNC);
            messageBuffer.AppendLine();
            if (missingHotfixesOnDb.Count() > 0)
            {
                isOutOfSync = true;
                messageBuffer.AppendLine(Resources.WRN_HOTFIX_NOT_ON_DB);
                foreach (string version in missingHotfixesOnDb)
                {
                    messageBuffer.AppendLine(version);
                }
            }

            messageBuffer.AppendLine();
            if (missingHotfixesOnFileSystem.Count() > 0)
            {
                isOutOfSync = true;
                messageBuffer.AppendLine(Resources.WRN_HOTFIX_NOT_ON_FS);
                foreach (string version in missingHotfixesOnFileSystem)
                {
                    messageBuffer.AppendLine(version);
                }
            }

            if (isOutOfSync)
                base.LogEvent(NotificationType.Warning , messageBuffer.ToString());
        }

        private static void CreateEmptyFile(string stateFilePath)
        {
            if (!File.Exists(stateFilePath))
            {
                using (StreamWriter writer = File.CreateText(stateFilePath))
                {
                    writer.Close();
                }
            }
        }


        #endregion
    }
}
