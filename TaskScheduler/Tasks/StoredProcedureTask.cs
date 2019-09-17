using log4net;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TaskScheduler.Tasks
{
    public class StoredProcedureTask: ITask
    {
        public bool IsExecuting { get; private set; }
        private SqlConnection connection;
        public string Location { get; set; }
        public string Name { get; set; }
        public string[] Parameters { get; set; }
        public bool ToWait { get; set; }
        public async Task RunTask(ILog log)
        {
            if (IsExecuting)
            {
                log.Warn("Stored procedure " + Name + " cannot start until the previous command is complete.");
            }
            else
            {
                using (connection = new SqlConnection(Location))
                {
                    try
                    {
                        connection.Open();
                        string commandText = "Exec " + Name + string.Join(",",Parameters);
                        SqlCommand command = new SqlCommand(commandText, connection);
                        command.CommandTimeout = 0;
                        log.Info("Starting stored procedure " + Name);
                        IsExecuting = true;
                        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
                        log.Info("Finished Stored procedure " + Name);
                    }
                    catch (Exception ex)
                    {
                        log.Fatal("Stored procedure " + Name + " failed.", ex);
                    }
                    finally
                    {
                        IsExecuting = false;
                        
                        if (connection != null)
                        {
                            connection.Close();
                        }
                    }
                }
            }
        }
        public bool Compare(ITask task)
        {
            var other = task as StoredProcedureTask;
            return other != null &&
                   connection.ConnectionString == other.connection.ConnectionString &&
                   Location == other.Location &&
                   Name == other.Name &&
                   ToWait == other.ToWait &&
                   Parameters.SequenceEqual(other.Parameters);
        }
    }
}
