using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Itim.TRS.InstallerLib.Data;
using Itim.TRS.InstallerLib.Properties;
using System.IO;

namespace Itim.TRS.InstallerLib
{
    public class PatchInstaller : InstallerBase
    {
        #region Private Members
        private DatabaseVersion _patchDetails;
        private string _installedReleaseVersion;
        private bool _databaseInstalled;
        private bool _filesCopied = false;
        #endregion

        public PatchInstaller(string source, string destination)
            : base(source, destination)
        {
        }

        #region Protected Methods
        protected override void FillFixDetails()
        {
            _patchDetails = new DatabaseVersion();
            _patchDetails.VersionNumber = base.PatchConfig.Version;
            _patchDetails.Description = base.PatchConfig.Description;
            _patchDetails.ReleaseDate = DateTime.Parse(base.PatchConfig.ReleaseDate);
            _patchDetails.fkSystemApplicationId = 1;
            _patchDetails.DeploymentDate = DateTime.Now;

            base.LogEvent(NotificationType.Info, String.Format(Resources.INF_BEGIN_Patch_INSTALL, _patchDetails.VersionNumber, _patchDetails.Description));
        }

        protected override void SaveFixDetails()
        {
            DBManager.SavePatch(_patchDetails);
        }

        protected override bool VerifyPreRequisites()
        {
            //LogEvent(NotificationType.Info, Resources.INF_VERIFY_PREREQ);
            _databaseInstalled = VerifyReleaseCompatibility();
            return _databaseInstalled;
        }

        protected override void FinalizeInstallation()
        {
            LogEvent(NotificationType.Info, Resources.INF_FINALIZE_INSTALL);
            if (_filesCopied)
            {
                base.FinalizeInstallation();
                RefreshHotFixVersionStateFile();                
            }
        }

        protected override void ExecuteDatabaseScript()
        {
            
            //if the entry for this Patch already exists then assume that the database script has already been executed on this machine.
            if (IsHotfixInstalledOnDB())
            {
                LogEvent(NotificationType.Info, String.Format(Resources.ERR_Patch_ALREADY_INSTALLED, _patchDetails.VersionNumber));
                return;
            }

            if (_patchDetails.pkDatabaseVersionId > 0 || _databaseInstalled)
                return;
            base.ExecuteDatabaseScript();
        }

        protected override void CopyFixContents()
        {
            CopyPatchContents();
        }

        #endregion

        #region Private Methods

        private bool IsHotfixInstalledOnDB()
        {
            List<DatabaseVersion> dbInstalledHotfixes = DBManager.LoadRequiredDbVersions(_patchDetails.VersionNumber);
            if (dbInstalledHotfixes.Count > 0)
            {
                _patchDetails = dbInstalledHotfixes[0];
                return true;
            }
            return false;
        }

        private bool CheckPatchInstallation()
        {
            base.RaiseProgressEvent(10, "Verifying prerequisites in install state file.");
            string stateFilePath = Path.Combine(InstallationDir, base.PatchStateFileName);
            if (File.Exists(stateFilePath))
            {
                string[] versionsInstalled = File.ReadAllLines(stateFilePath);
                for (int index = 0; index < versionsInstalled.Length; index++)
                {
                    if (versionsInstalled[index].Trim() == _patchDetails.VersionNumber.Trim())
                        return true;
                }
            }
            return false;
        }

        private void CopyPatchContents()
        {
            if (CheckPatchInstallation())
            {
                //LogEvent(NotificationType.Info, String.Format(Resources.ERR_Patch_ALREADY_INSTALLED, _patchDetails.VersionNumber));
                throw new Exception(String.Format(Resources.ERR_Patch_ALREADY_INSTALLED, _patchDetails.VersionNumber));
            }

            int fileCopiedCount = 0;

            LogEvent(NotificationType.Info, Resources.INF_COPY_FILES);

            fileCopiedCount += InstallReflexComponents();

            if (Utils.HasServerRole(ServerMode.SingleMachine, TRSInstallationMode))
            {
                fileCopiedCount = CopyPatchContents(SourceDir, InstallationDir);
            }
            else
            {
                if (Utils.HasServerRole(ServerMode.Web, TRSInstallationMode))
                {
                    string webPatchFolder = Path.Combine(SourceDir, "Web");
                    if (Directory.Exists(webPatchFolder))
                    {
                        fileCopiedCount += base.CopyPatchContents(webPatchFolder, Path.Combine(InstallationDir, "Web"));
                    }
                }
                else if (Utils.HasServerRole(ServerMode.Application, TRSInstallationMode))
                {
                    string applicationPatchFolder = Path.Combine(SourceDir, "Applications");
					string databasePatchFolder = Path.Combine(SourceDir, "Database");

                    if (Directory.Exists(applicationPatchFolder))
                    {
                        fileCopiedCount += base.CopyPatchContents(applicationPatchFolder, Path.Combine(InstallationDir, "Applications"));
						if (Directory.Exists(databasePatchFolder))
							fileCopiedCount += CopyPatchContents(databasePatchFolder, Path.Combine(InstallationDir, "Database"));
                    }
                }
            }

            if (Utils.HasServerRole(ServerMode.Application, TRSInstallationMode) || Utils.HasServerRole(ServerMode.Web, TRSInstallationMode))
            {
                fileCopiedCount += base.CopyDirectoryFiles(SourceDir, InstallationDir);
            }

            //if files were copied in this installation mode then set the appropriate flag
            if (fileCopiedCount > 0)
            {
                _filesCopied = true;
            }

            LogEvent(NotificationType.Info, String.Format(Resources.INF_COPY_COMPLETE, fileCopiedCount));
        }

        private int InstallReflexComponents()
        {
            int fileCopiedCount = 0;
            string reflexPatchFolder = Path.Combine(SourceDir, "Reflex");
            string system32PatchFolder = Path.Combine(reflexPatchFolder, "System32");
            string reflexInstallationFolder = Path.Combine(InstallationDir, "Reflex");
            string reflexExecutablesFolder = Path.Combine(reflexInstallationFolder, "Executables");
            string reflexPatchBinariesFolder = Path.Combine(reflexPatchFolder, "Binaries");
            string reflexPatchReportsFolder = Path.Combine(reflexPatchFolder, "Reports");

            if (Utils.HasServerRole(ServerMode.SingleMachine, TRSInstallationMode) || Utils.HasServerRole(ServerMode.Application, TRSInstallationMode))
            {
                fileCopiedCount += base.CopyPatchContents(reflexPatchFolder, reflexInstallationFolder);
            }

            return fileCopiedCount;
        }

        private void RefreshHotFixVersionStateFile()
        {
            string stateFilePath = Path.Combine(InstallationDir, base.HotfixStateFileName);
            if (File.Exists(stateFilePath))
                File.Delete(stateFilePath);
        }

        private bool VerifyReleaseCompatibility()
        {
            LogEvent(NotificationType.Info, Resources.INF_CHECK_RELEASE_COMPAT);
            _installedReleaseVersion = DBManager.LoadInstalledReleaseVersion();

            string[] releaseVersion = _installedReleaseVersion.Split('.');
            string[] patchVersion = _patchDetails.VersionNumber.Split('.');

            if (Convert.ToInt32(releaseVersion[0]) == Convert.ToInt32(patchVersion[0]) &&
                Convert.ToInt32(releaseVersion[1]) <= Convert.ToInt32(patchVersion[1]) &&
                Convert.ToInt32(releaseVersion[2]) <= Convert.ToInt32(patchVersion[2])
                )
            {
            }
            else
            {
                throw new Exception("No compatible release found to apply the patch.\r\nLast Installed Release/Patch: " +
                    _installedReleaseVersion + "\r\nCurrent Patch Version: " + _patchDetails.VersionNumber);
            }

            return Convert.ToInt32(releaseVersion[0]) == Convert.ToInt32(patchVersion[0]) &&
                                     Convert.ToInt32(releaseVersion[1]) == Convert.ToInt32(patchVersion[1]) &&
                                     Convert.ToInt32(releaseVersion[2]) == Convert.ToInt32(patchVersion[2]);
        }
        #endregion
    }
}
