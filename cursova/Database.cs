using MySql.Data.MySqlClient;

public static class DBMySQLUtils
{
    public static MySqlConnection GetDBConnection(string host = "localhost", int port = 3306, string database = "cursova", string username = "root", string password = "220806")
    {
        string connString = $"Server={host};Port={port};Database={database};Uid={username};Pwd={password};";
        MySqlConnection conn = new MySqlConnection(connString);
        return conn;
    }
}