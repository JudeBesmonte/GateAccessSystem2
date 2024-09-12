using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;

namespace GateAccessSystem2.data
{
    internal class Connection
    {
        // Define the connection string
        public static MySqlConnection DBConnection;

        static string server = "127.0.0.1";
        static string database = "thesis";
        static string Uid = "root";
        static string password = "parasathesis";

        public static MySqlConnection dataSource()
        {
            // Fix connection string syntax
            string connectionString = $"server={server};database={database};Uid={Uid};password={password};";
            DBConnection = new MySqlConnection(connectionString);
            return DBConnection;
        }
    }
}
