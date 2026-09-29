using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using System.Text;
using System.IO;
using Itim.TRS.InstallerLib;
using Itim.TRS.InstallerLib.Properties;

namespace Itim.TRS.InstallerLib
{
    class DirectoryCopier
    {
        #region Constants
        const string FixManifest = "fix.manifest";
        const string InstallationFeatures = "FeaturesTemp.wxs";
        #endregion

        #region Instance Variables
        private DirectoryInfo _sourceDir;
        private DirectoryInfo _destDir;
        private List<string> _newFilePaths;
        private DirectoryInfo _backupDir;
        private string _searchPattern;
        private List<string> _featureFiles;

        #endregion

        public string BackupDirPath { get; set; }

        #region Constructors
        public DirectoryCopier(string sourcePath, string destPath)
        {
            _sourceDir = new DirectoryInfo(sourcePath);
            if (!Directory.Exists(destPath))
                _destDir = Directory.CreateDirectory(destPath);
            else
                _destDir = new DirectoryInfo(destPath);

            _newFilePaths = new List<string>();
            _searchPattern = "*";//wild card: search for all

            if (!string.IsNullOrEmpty(Installer._features))
            {
                XDocument featuresDoc = XDocument.Parse(Installer._features);
                _featureFiles = featuresDoc.Root.Elements("feature")
                              .Select(element => element.Value)
                              .ToList();
            }
        }

        public DirectoryCopier(string sourcePath, string destPath, string fileSearchPattern)
            : this(sourcePath, destPath)
        {
            _searchPattern = fileSearchPattern;
        }
        #endregion

        #region Public Methods
        public bool SaveBackup(string dirToCopy)
        {
            GetTempBackupDirectory().MoveTo(dirToCopy);
            return true;
        }

        public int Copy()
        {
            return Copy(true);
        }

        public int Copy(bool recursive)
        {
            DirectoryInfo backupDir = GetTempBackupDirectory();
            if (recursive)
                return CopyRecursive(_sourceDir, _destDir, backupDir);
            else
                return CopyFiles(_sourceDir, _destDir, backupDir);
        }

        public void Rollback()
        {
            foreach (string path in _newFilePaths)
            {
                if (File.Exists(path))
                    File.Delete(path);
            }

            CopyRecursive(GetTempBackupDirectory(), _destDir, null);
        }


        #endregion

        #region Private Methods
        private int CopyRecursive(DirectoryInfo sourceDir, DirectoryInfo destDir, DirectoryInfo backupDir)
        {
            int fileCount = CopyFiles(sourceDir, destDir, backupDir);

            foreach (DirectoryInfo sourceSubDirectory in sourceDir.GetDirectories())
            {
                string destSubDirectoryPath = Path.Combine(destDir.FullName, sourceSubDirectory.Name);
                DirectoryInfo destSubDir = null;

                if (!Directory.Exists(destSubDirectoryPath))
                {
                    destSubDir = Directory.CreateDirectory(destSubDirectoryPath);
                }
                else
                {
                    destSubDir = new DirectoryInfo(destSubDirectoryPath);
                }

                DirectoryInfo backupSubDir = null;
                if (backupDir != null)
                {
                    string backupSubDirPath = Path.Combine(backupDir.FullName, Path.GetFileName(destSubDirectoryPath));
                    backupSubDir = Directory.CreateDirectory(backupSubDirPath);
                }

                fileCount += CopyRecursive(sourceSubDirectory, destSubDir, backupSubDir);
            }

            return fileCount;
        }

        private bool IsFeatureInstalled(FileInfo sourcefile, string destFilePath)
        {
            bool exists = true;

            if (_featureFiles != null)
            {
                if (_featureFiles.Contains(sourcefile.Name))
                {
                    if (!File.Exists(destFilePath))
                        exists = false;
                }
            }
            return exists;
        }

        private int CopyFiles(DirectoryInfo sourceDir, DirectoryInfo destDir, DirectoryInfo backupDir)
        {
            int fileCount = 0;


            if (destDir.FullName.EndsWith("wwwroot\\dist"))
            {
                foreach (FileInfo destinationfile in destDir.GetFiles(_searchPattern))
                {
                    string backupFilePath = Path.Combine(backupDir.FullName, destinationfile.Name);
                    string destFilePath = Path.Combine(destDir.FullName, destinationfile.Name);
                    FileInfo destFile = new FileInfo(destFilePath);

                    RemoveReadonly(destFile);
                    destFile.CopyTo(backupFilePath, true);
                }

                foreach (FileInfo destinationfile in destDir.GetFiles(_searchPattern))
                {
                    string backupFilePath = Path.Combine(backupDir.FullName, destinationfile.Name);
                    string destFilePath = Path.Combine(destDir.FullName, destinationfile.Name);
                    FileInfo destFile = new FileInfo(destFilePath);

                    RemoveReadonly(destFile);
                    destFile.Delete();
                }

            }


            foreach (FileInfo sourcefile in sourceDir.GetFiles(_searchPattern))
            {
                if (sourcefile.Name.Equals(FixManifest))
                    continue;
                if (sourcefile.Name.Equals(InstallationFeatures))
                    continue;

                string destFilePath = Path.Combine(destDir.FullName, sourcefile.Name);

                //Check if feature is installed. If not then donot copy the file.
                if (!IsFeatureInstalled(sourcefile, destFilePath))
                    continue;

                if (File.Exists(destFilePath) && backupDir != null)
                {
                    FileInfo destFile = new FileInfo(destFilePath);
                    RemoveReadonly(destFile);
                    string backupFilePath = Path.Combine(backupDir.FullName, destFile.Name);
                    destFile.CopyTo(backupFilePath, true);
                }
                else
                    _newFilePaths.Add(destFilePath);


                sourcefile.CopyTo(destFilePath, true);
                if (EventLoggingCallback != null)
                    EventLoggingCallback(NotificationType.Info, String.Format(Resources.INF_FILE_COPIED, destFilePath));
                fileCount++;
            }

            return fileCount;
        }

        private static void RemoveReadonly(FileInfo file)
        {
            file.Attributes &= ~FileAttributes.ReadOnly;//remove the readonly attribute
        }

        private DirectoryInfo GetTempBackupDirectory()
        {
            if (_backupDir == null)
            {
                string backupDir = String.IsNullOrEmpty(this.BackupDirPath) ?
                        String.Format(Environment.CurrentDirectory, _sourceDir.Parent.Name + DateTime.Now.ToString("yyyymmdd")) : this.BackupDirPath;
                _backupDir = Directory.CreateDirectory(backupDir);
            }
            return _backupDir;
        }

        #endregion
        public delegate void LoggingCallbackDelegate(NotificationType eventType, string message);
        public LoggingCallbackDelegate EventLoggingCallback
        { set; get; }



    }
}
