using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections;
using System.Configuration;
using System.Diagnostics;
using System.Data.SqlClient;

using Itim.TRS.InstallerLib;
using Itim.TRS.InstallerLib.Data;
using SQLServer2005;
using SQLServer2008;
using System.IO;

namespace Itim.TRS.InstallerLib
{
    static class DBManager
    {

        #region Properties
        public static string ConnectionString { get; set; }
        public static string CustomerConnectionString { get; set; }
        #endregion

        #region Public Methods

        public static List<Server> GetTRSServers()
        {
            using (IRSecurityDataContext context = new IRSecurityDataContext(ConnectionString))
            {
                var servers = from server in context.Servers
                              join instance in context.TRSInstances
                              on server.fkTRSInstanceId equals instance.pkTRSInstanceId
                              where instance.IsSelfInstance == true && server.IsDatabaseServer == false
                              select server;

                return servers.ToList();
            }
        }

        public static string LoadInstalledReleaseVersion()
        {
            using (IRSecurityDataContext context = new IRSecurityDataContext(ConnectionString))
            {
                var query = from r in context.DatabaseVersions
                            orderby r.DeploymentDate descending
                            select r.VersionNumber;

                return query.First();
            }
        }

        public static List<InstalledHotfix> LoadRequiredHotfixes(List<string> requiredHotfixes, ServerMode installationMode)
        {
            using (IRSecurityDataContext context = new IRSecurityDataContext(ConnectionString))
            {
                var hotfixes = (from h in context.InstalledHotfixes
                                where requiredHotfixes.Contains(h.HotfixVersion)
                                select h);

                return hotfixes.ToList();
            }
        }

        public static List<InstalledHotfix> LoadRequiredHotfixesForServer(List<string> requiredHotfixes, ServerMode installationMode)
        {
            using (IRSecurityDataContext context = new IRSecurityDataContext(ConnectionString))
            {
                var hotfixes = (from h in context.InstalledHotfixes
                                where requiredHotfixes.Contains(h.HotfixVersion)
                                select h);

                if (installationMode == ServerMode.Application)
                    hotfixes = hotfixes.Where<InstalledHotfix>(h => h.IsForAppServer == true);
                else if (installationMode == ServerMode.Web)
                    hotfixes = hotfixes.Where<InstalledHotfix>(h => h.IsForWebServer == true);
                else if (installationMode == ServerMode.Database)
                    hotfixes = hotfixes.Where<InstalledHotfix>(h => h.IsForDBServer == true);

                return hotfixes.ToList();
            }
        }

        public static bool VerifyPrerequisiteForServer(List<string> requiredHotfixes, ServerMode installationMode)
        {
            using (IRSecurityDataContext context = new IRSecurityDataContext(ConnectionString))
            {
                var hotfixes = (from h in context.InstalledHotfixes
                                where requiredHotfixes.Contains(h.HotfixVersion)
                                select h);
                int count = hotfixes.ToList().Count;
                if (installationMode == ServerMode.Application)
                    hotfixes = hotfixes.Where<InstalledHotfix>(h => h.IsForAppServer == true);
                else if (installationMode == ServerMode.Web)
                    hotfixes = hotfixes.Where<InstalledHotfix>(h => h.IsForWebServer == true);
                else if (installationMode == ServerMode.Database)
                    hotfixes = hotfixes.Where<InstalledHotfix>(h => h.IsForDBServer == true);

                if (hotfixes.ToList().Count == count)
                    return true;

                return false;
            }
        }

        public static List<InstalledHotfix> LoadAllHotfixesForServerRole(bool IsWebInstallation, bool IsAppInstallation)
        {
            if (IsWebInstallation && IsAppInstallation)
            {
                using (IRSecurityDataContext context = new IRSecurityDataContext(ConnectionString))
                {
                    var hotfixes = from h in context.InstalledHotfixes
                                   where (h.IsForWebServer == true && h.IsForAppServer == true)
                                   select h;

                    return hotfixes.ToList();
                }
            }
            else if (IsWebInstallation && !IsAppInstallation)
            {
                using (IRSecurityDataContext context = new IRSecurityDataContext(ConnectionString))
                {
                    var hotfixes = from h in context.InstalledHotfixes
                                   where h.IsForWebServer == true
                                   select h;

                    return hotfixes.ToList();
                }
            }
            else if (!IsWebInstallation && IsAppInstallation)
            {
                using (IRSecurityDataContext context = new IRSecurityDataContext(ConnectionString))
                {
                    var hotfixes = from h in context.InstalledHotfixes
                                   where h.IsForAppServer == true
                                   select h;

                    return hotfixes.ToList();
                }
            }
            else
            {
                using (IRSecurityDataContext context = new IRSecurityDataContext(ConnectionString))
                {
                    var hotfixes = from h in context.InstalledHotfixes
                                   where h.IsForDBServer == true
                                   select h;

                    return hotfixes.ToList();
                }
            }

        }

        public static List<DatabaseVersion> LoadRequiredDbVersions(string requiredVersions)
        {
            using (IRSecurityDataContext context = new IRSecurityDataContext(ConnectionString))
            {
                var databaseVersions = from dv in context.DatabaseVersions
                                       where requiredVersions == dv.VersionNumber
                                       orderby dv.DeploymentDate
                                       select dv;

                return databaseVersions.ToList();
            }
        }

        public static string GetCustomerName()
        {
            using (IRSecurityDataContext context = new IRSecurityDataContext(ConnectionString))
            {
                var customerNameQuery = from p in context.SystemParameters
                                        where p.Description == "Customer"
                                        select p.Value;

                return customerNameQuery.First();
            }
        }

        public static void SaveHotfix(InstalledHotfix hotfix, List<string> DBdetails, List<string> Appdetails, List<string> Webdetails)
        {
            using (IRSecurityDataContext context = new IRSecurityDataContext(ConnectionString))
            {

                if (hotfix.pkInstalledHotfixId == 0)
                    context.InstalledHotfixes.InsertOnSubmit(hotfix);
                else
                {
                    InstalledHotfix loadedHotfix = context.InstalledHotfixes.Single(h => h.pkInstalledHotfixId == hotfix.pkInstalledHotfixId);
                    loadedHotfix.IsForAppServer = hotfix.IsForAppServer;
                    loadedHotfix.IsForDBServer = hotfix.IsForDBServer;
                    loadedHotfix.IsForWebServer = hotfix.IsForWebServer;
                }
                context.SubmitChanges();

                int lastInsertedId = hotfix.pkInstalledHotfixId;

                if (DBdetails.Count > 0)
                {
                    AddHotfixDetails(DBdetails, lastInsertedId, true, false, false);

                }

                if (Appdetails.Count > 0)
                {

                    AddHotfixDetails(Appdetails, lastInsertedId, false, true, false);
                }

                if (Webdetails.Count > 0)
                {

                    AddHotfixDetails(Webdetails, lastInsertedId, false, false, true);
                }

            }
        }

        public static void AddHotfixDetails(List<string> details, int lastInsertedId, bool isForDBServer, bool isForAppServer, bool isForWebServer)
        {
            using (IRSecurityDataContext context = new IRSecurityDataContext(ConnectionString))
            {


                if (details?.Count > 0)
                {
                    foreach (var item in details)
                    {
                        var hotfixDetail = new InstalledHotfixDetails
                        {
                            fkInstalledHotfixId = lastInsertedId,
                            Artifacts = item,
                            IsForDBServer = isForDBServer,
                            IsForAppServer = isForAppServer,
                            IsForWebServer = isForWebServer
                        };
                        context.InstalledHotfixDetails.InsertOnSubmit(hotfixDetail);
                    }

                    context.SubmitChanges();
                }
            }
        }

        public static void SavePatch(DatabaseVersion Patch)
        {
            using (IRSecurityDataContext context = new IRSecurityDataContext(ConnectionString))
            {
                if (Patch.pkDatabaseVersionId == 0)
                    context.DatabaseVersions.InsertOnSubmit(Patch);
                context.SubmitChanges();
            }
        }

        public static void ExecuteCustomerScript(string scriptFilePath)
        {
            string version;
            using (SqlConnection sqlcon = new SqlConnection(CustomerConnectionString))
            {
                sqlcon.Open();
                using (SqlCommand sqlcmd = new SqlCommand("Select @@Version", sqlcon))
                {
                    version = sqlcmd.ExecuteScalar().ToString();
                }
                sqlcon.Close();
            }

            if (version.Contains("Microsoft SQL Server 2005"))
                SQLServer2005.SqlServer.ExecuteScript(scriptFilePath, CustomerConnectionString);
            else if (version.Contains("Microsoft SQL Server 2008"))
                SQLServer2008.SqlServer.ExecuteScript(scriptFilePath, CustomerConnectionString);
            else if (version.Contains("Microsoft SQL Server 2014"))
                SQLServer2014.SqlServer.ExecuteScript(scriptFilePath, CustomerConnectionString);
            else
                SQLServer2016.SqlServer.ExecuteScript(scriptFilePath, CustomerConnectionString);
        }

        public static void ExecuteScript(string scriptFilePath)
        {
            string version;
            using (SqlConnection sqlcon = new SqlConnection(ConnectionString))
            {
                sqlcon.Open();
                using (SqlCommand sqlcmd = new SqlCommand("Select @@Version", sqlcon))
                {
                    version = sqlcmd.ExecuteScalar().ToString();
                }
                sqlcon.Close();
            }

            if (version.Contains("Microsoft SQL Server 2005"))
                SQLServer2005.SqlServer.ExecuteScript(scriptFilePath, ConnectionString);
            else if (version.Contains("Microsoft SQL Server 2008"))
                SQLServer2008.SqlServer.ExecuteScript(scriptFilePath, ConnectionString);
            else if (version.Contains("Microsoft SQL Server 2014"))
                SQLServer2014.SqlServer.ExecuteScript(scriptFilePath, ConnectionString);
            else
                SQLServer2016.SqlServer.ExecuteScript(scriptFilePath, ConnectionString);
        }


        public static void ExecuteScript(string scriptFilePath, string databaseName)
        {
            if (string.IsNullOrEmpty(scriptFilePath))
                throw new ArgumentNullException(nameof(scriptFilePath), "Script file path cannot be null or empty.");

            if (string.IsNullOrEmpty(databaseName))
                throw new ArgumentNullException(nameof(databaseName), "Database name cannot be null or empty.");

            if (!File.Exists(scriptFilePath))
                throw new FileNotFoundException($"SQL script file not found: {scriptFilePath}");
            string version;
            string connectionStringWithDatabase;
            
                if (databaseName == "Customer")
                {
                    connectionStringWithDatabase = $"{CustomerConnectionString};Initial Catalog={databaseName}";

                }
                else
                {
                    connectionStringWithDatabase = $"{ConnectionString};Initial Catalog={databaseName}";
                }

                using (SqlConnection sqlcon = new SqlConnection(connectionStringWithDatabase))
                {
                    sqlcon.Open();
                    using (SqlCommand sqlcmd = new SqlCommand("Select @@Version", sqlcon))
                    {
                        version = sqlcmd.ExecuteScalar().ToString();
                    }
                    sqlcon.Close();
                }

                if (version.Contains("Microsoft SQL Server 2005"))
                    SQLServer2005.SqlServer.ExecuteScript(scriptFilePath, connectionStringWithDatabase);
                else if (version.Contains("Microsoft SQL Server 2008"))
                    SQLServer2008.SqlServer.ExecuteScript(scriptFilePath, connectionStringWithDatabase);
                else if (version.Contains("Microsoft SQL Server 2014"))
                    SQLServer2014.SqlServer.ExecuteScript(scriptFilePath, connectionStringWithDatabase);
                else
                    SQLServer2016.SqlServer.ExecuteScript(scriptFilePath, connectionStringWithDatabase);
            
        }




            //public static void ExecuteScript(string scriptFilePath, string databaseName)
            //{
            //    if (string.IsNullOrEmpty(scriptFilePath))
            //        throw new ArgumentNullException(nameof(scriptFilePath), "Script file path cannot be null or empty.");

            //    if (string.IsNullOrEmpty(databaseName))
            //        throw new ArgumentNullException(nameof(databaseName), "Database name cannot be null or empty.");

            //    if (!File.Exists(scriptFilePath))
            //        throw new FileNotFoundException($"SQL script file not found: {scriptFilePath}");

            //    string version;
            //    string connectionStringWithDatabase;

            //    try
            //    {
            //        // Build connection string
            //        connectionStringWithDatabase = databaseName == "Customer"
            //            ? $"{CustomerConnectionString};Initial Catalog={databaseName}"
            //            : $"{ConnectionString};Initial Catalog={databaseName}";

            //        // Get SQL Server version
            //        using (SqlConnection sqlcon = new SqlConnection(connectionStringWithDatabase))
            //        {
            //            sqlcon.Open();
            //            using (SqlCommand sqlcmd = new SqlCommand("SELECT @@VERSION", sqlcon))
            //            {
            //                version = sqlcmd.ExecuteScalar().ToString();
            //            }
            //        }

            //        // Execute script based on version
            //        if (version.Contains("Microsoft SQL Server 2005"))
            //            SQLServer2005.SqlServer.ExecuteScript(scriptFilePath, connectionStringWithDatabase);
            //        else if (version.Contains("Microsoft SQL Server 2008"))
            //            SQLServer2008.SqlServer.ExecuteScript(scriptFilePath, connectionStringWithDatabase);
            //        else if (version.Contains("Microsoft SQL Server 2014"))
            //            SQLServer2014.SqlServer.ExecuteScript(scriptFilePath, connectionStringWithDatabase);
            //        else
            //            SQLServer2016.SqlServer.ExecuteScript(scriptFilePath, connectionStringWithDatabase);
            //    }
            //    catch (Exception ex)
            //    {
            //        throw new Exception($"Failed to execute SQL script '{Path.GetFileName(scriptFilePath)}' on database '{databaseName}'.", ex);
            //    }
            //}


            #endregion
        }
}
