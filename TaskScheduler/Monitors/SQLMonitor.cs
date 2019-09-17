using log4net;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TaskScheduler.Monitors
{
    public class SQLMonitor: IMonitor
    {
        private static readonly ILog Logger = LogManager.GetLogger(Environment.MachineName);
        private SqlDependency watcher;
        private static string queueName = "ServiceBrokerQueue";
        private static Dictionary<string, string> connectionsStarted = new Dictionary<string, string>();
        public SQLMonitor(string sqlInstance, string sqlDb, string tblName)
        {
            string connectionString = string.Format(
             "Driver=SQLOLEDB;" +
             "Data Source={0};" +
             "Initial Catalog={1};" +
             "Integrated Security=SSPI;", sqlInstance, sqlDb);
            Connection = new SqlConnection(connectionString);
            TurnOn(connectionString);
            watcher = new SqlDependency();
            SqlQuery = GetSqlQuery(tblName);
        }
        public SQLMonitor(string connectionString, string tblName)
        {
            Connection = new SqlConnection(connectionString);
            TurnOn(connectionString);
            watcher = new SqlDependency();
            SqlQuery = GetSqlQuery(tblName);
        }
        public SQLMonitor(SqlConnection connectionString, string tblName)
        {
            Connection = new SqlConnection(connectionString.ConnectionString);
            TurnOn(connectionString.ConnectionString);
            watcher = new SqlDependency();
            SqlQuery = GetSqlQuery(tblName);
        }
        public string MonitorName { get; set; }
        public SqlConnection Connection { get; }
        public string SqlQuery { get; }
        public IJob Job { get; set; }
        public SqlNotificationInfo NotificationType {get;set;}
        private static void TurnOn(string connection)
        {
            if (!connectionsStarted.ContainsKey(connection))
            {
                SqlDependency.Start(connection);
                connectionsStarted[connection] = "";
            }
        }
        private static void TurnOff(string connection)
        {
            if (connectionsStarted.ContainsKey(connection))
            {
                SqlDependency.Stop(connection);
                connectionsStarted.Remove(connection);
            }
        }
        public void BeginMonitoring()
        {
            Job.IsOn = true;
            if (Connection != null && Connection.State == ConnectionState.Closed && !string.IsNullOrWhiteSpace(SqlQuery))
            {
                Logger.Info("Starting monitoring for " + MonitorName);
                Connection.Open();
            }
            watcher = new SqlDependency();
            watcher.OnChange += OnChange;
            SqlCommand command = new SqlCommand(SqlQuery, Connection);
            watcher.AddCommandDependency(command);
            using (SqlDataReader reader = command.ExecuteReader())
            {
            }
            Connection.Close();
            Job.UpdateStatus();
        }
        public void EndMonitoring()
        {
            Logger.Info("Ending monitoring for " + MonitorName);
            TurnOff(Connection.ConnectionString);
        }
        private void OnChange(object sender, SqlNotificationEventArgs args)
        {
            if(args.Info == NotificationType)
            {
                Job.StartJob();
            }
            watcher.OnChange -= OnChange;
            BeginMonitoring();
        }
        private string GetSqlQuery(string tableName)
        {
            using (var connection = new SqlConnection(Connection.ConnectionString))
            {
                using (SqlCommand command = connection.CreateCommand())
                {
                    connection.Open();
                    List<string> columnNames = new List<string>();
                    command.CommandText = @"select c.name from sys.columns c inner join sys.tables t on t.object_id = c.object_id and t.name = @tblName and t.type = 'U'
                                                inner join sys.schemas s on t.schema_id = s.schema_id
                                                where s.name =@schema";
                    var names = tableName.Split('.');
                    string schema = "";
                    string table = "";
                    switch(names.Length)
                    {
                        case 1:
                            schema = "dbo";
                            table = names[0];
                            break;
                        case 2:
                            schema = names[0];
                            table = names[1];
                            break;
                        case 3:
                            schema = names[1];
                            table = names[2];
                            break;
                        case 4:
                            schema = names[2];
                            table = names[3];
                            break;
                        default:
                            Logger.Fatal("Could not determine schema and table name for " + tableName);
                            break;
                    }
                    command.Parameters.Add("@tblName", SqlDbType.VarChar);
                    command.Parameters["@tblName"].Value = table;
                    command.Parameters.Add("@schema", SqlDbType.VarChar);
                    command.Parameters["@schema"].Value = schema;
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            columnNames.Add(reader.GetString(0));
                        }
                    }
                    connection.Close();
                    return "Select [" + string.Join("],[", columnNames) + "] FROM " + tableName;
                }
            }
        }

        public bool Compare(IMonitor monitor)
        {
            var other = monitor as SQLMonitor;
            return other != null &&
                   MonitorName == other.MonitorName &&
                   Connection.ConnectionString ==  other.Connection.ConnectionString &&
                   SqlQuery == other.SqlQuery &&
                   NotificationType == other.NotificationType &&
                   Job.Compare(other.Job) ;
        }
    }
}
