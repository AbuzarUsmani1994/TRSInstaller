using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

using Microsoft.SqlServer.Management.Common;
using Microsoft.SqlServer.Management.Smo;
using System.Data.SqlClient;

namespace SQLServer2005
{
	public static class SqlServer
	{
        public static void ExecuteScript(string scriptFilePath, string ConnectionString)
        {
            ExecuteScriptText(File.ReadAllText(scriptFilePath), ConnectionString);
        }

        public static void ExecuteScriptText(string script, string ConnectionString)
        {
            try
            {
                using (SqlConnection connection = new SqlConnection(ConnectionString))
                {
                    ServerConnection serverConnection = new ServerConnection(connection);
                    serverConnection.StatementTimeout = 0;
                    Server server = new Server(serverConnection);
                    server.ConnectionContext.ExecuteNonQuery(script, ExecutionTypes.Default);
                }
            }
            catch (ExecutionFailureException e)
            {
                if (e.InnerException is SqlException)
                    throw FormatSqlException((SqlException)e.InnerException);
                else
                    throw e.InnerException;
            }
        }

		#region Private Methods
		private static Exception FormatSqlException(SqlException error)
		{
			string message = String.Format("Error executing sql: Msg {0}, Level {1}, State {2}, Line {3} - {4}, ",
															  error.Number, error.Class, error.State, error.LineNumber, error.Message);
			return new Exception(message, error);
		}
		#endregion
	}
}
