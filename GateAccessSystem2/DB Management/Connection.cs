using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GateAccessSystem2.DB_Management
{
    internal class Connection
    {
        private static string server = "127.0.0.1";    
        private static string database = "thesis";      
        private static string username = "root";        
        private static string password = "parasathesis"; 

        public static MySqlConnection GetConnection()
        {
            string connectionString = $"Server={server};Database={database};Uid={username};Pwd={password};";
            return new MySqlConnection(connectionString);
        }
    }
}
