using Dapper;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;

namespace BinanceAlgorithmLight.ConnectDB
{
    public static class ConnectTrial
    {
        public static string connectionString = @"Data Source=sql8001.site4now.net;Initial Catalog=db_a86450_valik;User ID=db_a86450_valik_admin;Password=Zaqwsx123";

        public static bool Check(string trial_key)
        {
            //return true;
            using (IDbConnection connection = new SqlConnection(connectionString))
            {
                List<string> list = connection.Query<string>($"SELECT * FROM Trials").ToList();
                foreach (string it in list) if (it == trial_key) return true;
            }
            return false;
        }
    }
}
