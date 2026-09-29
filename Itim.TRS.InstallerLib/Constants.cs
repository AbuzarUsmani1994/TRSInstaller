using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.IO;
using Itim.TRS.InstallerLib.Properties;
using Microsoft.Win32;

namespace Itim.TRS.InstallerLib
{
    [Flags]
    public enum NotificationType
    {
        Debug=0,
        Info=1,
        Warning=2,
        Error=4
    }

    [Flags]
    public enum ServerMode
    {
        None=0,
        Database=1,
        Web=2,
        Application=4,
        SingleMachine = Web | Application
    }

    public static class Utils
    {
        public static bool HasServerRole(ServerMode serverRole, ServerMode installationMode)
        {
            return ((installationMode & serverRole) == serverRole);
        }

        public static void RunExecutable(string executableFilePath, string workingDirectory, string arguments)
        {
            Process shellProcess = new Process();

            if (!File.Exists(executableFilePath))
                throw new Exception(String.Format(Resources.ERR_EXE_NOT_FOUND,executableFilePath));

            shellProcess.StartInfo.UseShellExecute = false;
            
            shellProcess.StartInfo.WorkingDirectory = workingDirectory;
            shellProcess.StartInfo.FileName = executableFilePath;            
            shellProcess.StartInfo.Arguments = arguments;            
            shellProcess.Start();
            shellProcess.WaitForExit();
        }

        /// <summary>
        /// Get best guessed installation path...
        /// </summary>
        /// <returns></returns>
        public static string GetTRSInstalledPath()
        {
            string trsInstallationPath = null;
            RegistryKey regkey = Registry.LocalMachine.OpenSubKey(@"Software\AIM\RMS\1.0");

            //Registry entry exists
            if (regkey != null)
            {
                if (regkey.GetValue("InstallDir") != null)
                {
                    string reflexInstallationPath = regkey.GetValue("InstallDir").ToString();
                    //Intentionally not used Directory.GetParent() because 
                    //if path is C:\Test\Nested\ then it would return C:\Test\Nested
                    //else if path is C:\Test\Nested then it would return C:\Test\
                    trsInstallationPath = Path.GetFullPath(Path.Combine(reflexInstallationPath, "..\\"));
                }
            }

            //Unable to find install dir 
            if (string.IsNullOrEmpty(trsInstallationPath))
            {
                string[] availableDrives = Directory.GetLogicalDrives();

                DriveInfo driveInfo;
                foreach (string drive in availableDrives)
                {
                    driveInfo = new DriveInfo(drive);
                    if (driveInfo.DriveType == DriveType.Fixed) //Only search in harddisk
                    {
                        trsInstallationPath = Path.Combine(drive, "Program Files\\Itim\\The Retail Suite");
                        if (Directory.Exists(trsInstallationPath))
                            break;

                        trsInstallationPath = Path.Combine(drive, "Program Files (x86)\\Itim\\The Retail Suite");
                        if (Directory.Exists(trsInstallationPath))
                            break;

                        //Set to empty so that we can check whether we are able to find install dir or not...
                        trsInstallationPath = string.Empty;
                    }
                }
                if (string.IsNullOrEmpty(trsInstallationPath))
                    trsInstallationPath = "D:\\Program Files\\Itim\\The Retail Suite"; //Default ? Could throw error?
            }

            return trsInstallationPath;
        }

    }
}
