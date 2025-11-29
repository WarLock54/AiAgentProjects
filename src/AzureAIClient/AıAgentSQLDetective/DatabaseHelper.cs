using Microsoft.Data.Sqlite;
using System.Data;

public static class DatabaseHelper
{
    private static string connectionString = "Data Source=:memory:"; // RAM üzerinde çalışır, çok hızlıdır.
    private static SqliteConnection _connection;

    public static void InitializeDatabase()
    {
        _connection = new SqliteConnection(connectionString);
        _connection.Open();

        // 1. Tabloyu Oluştur
        var createTableCmd = _connection.CreateCommand();
        createTableCmd.CommandText = @"
            CREATE TABLE Sales (
                Id INTEGER PRIMARY KEY,
                ProductName TEXT,
                Category TEXT,
                Quantity INTEGER,
                Price REAL,
                SaleDate TEXT
            );";
        createTableCmd.ExecuteNonQuery();

        // 2. Örnek Veri Doldur (Seed Data)
        var insertCmd = _connection.CreateCommand();
        insertCmd.CommandText = @"
            INSERT INTO Sales VALUES (1, 'Laptop X1', 'Electronics', 2, 1200.50, '2023-10-01');
            INSERT INTO Sales VALUES (2, 'Mouse Wireless', 'Electronics', 15, 25.00, '2023-10-05');
            INSERT INTO Sales VALUES (3, 'Coffee Maker', 'Home', 5, 80.00, '2023-10-10');
            INSERT INTO Sales VALUES (4, 'Gaming Monitor', 'Electronics', 3, 350.00, '2023-10-12');
            INSERT INTO Sales VALUES (5, 'Desk Chair', 'Furniture', 4, 150.00, '2023-10-15');
            INSERT INTO Sales VALUES (6, 'Laptop X1', 'Electronics', 1, 1200.50, '2023-11-01'); -- Geçen ay verisi
            INSERT INTO Sales VALUES (7, 'Mechanical Keyboard', 'Electronics', 8, 100.00, '2023-11-05');
        ";
        insertCmd.ExecuteNonQuery();
    }

    // Ajanın çağıracağı fonksiyon
    public static string ExecuteQuery(string query)
    {
        try
        {
            var cmd = _connection.CreateCommand();
            cmd.CommandText = query;

            using var reader = cmd.ExecuteReader();
            var results = new List<Dictionary<string, object>>();

            while (reader.Read())
            {
                var row = new Dictionary<string, object>();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    row[reader.GetName(i)] = reader.GetValue(i);
                }
                results.Add(row);
            }

            // Sonucu JSON benzeri string'e çevir
            if (results.Count == 0) return "No records found.";

            return System.Text.Json.JsonSerializer.Serialize(results);
        }
        catch (Exception ex)
        {
            return $"SQL Error: {ex.Message}";
        }
    }
}