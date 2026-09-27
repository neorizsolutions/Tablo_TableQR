using System;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using Npgsql;

class Program
{
    static void Main()
    {
        string connString = "Host=localhost;Database=TabloDb;Username=postgres;Password=postgres";

        using var conn = new NpgsqlConnection(connString);
        conn.Open();

        using var cmd = new NpgsqlCommand(@"SELECT ""RestaurantId"", ""NameOrNumber"", ""Id"" FROM ""RestaurantTables"";", conn);
        using var reader = cmd.ExecuteReader();
        
        var tables = new List<(Guid RestaurantId, string NameOrNumber, Guid Id, int ParsedNumber)>();
        
        while (reader.Read())
        {
            var resId = reader.GetGuid(0);
            var name = reader.GetString(1);
            var id = reader.GetGuid(2);
            var numericPart = Regex.Replace(name, @"[^\d]", "");
            int parsed = int.TryParse(numericPart, out int num) ? num : 0;
            
            tables.Add((resId, name, id, parsed));
        }
        
        bool conflict = false;
        var groups = new Dictionary<string, List<string>>();
        foreach(var t in tables) {
            string key = t.RestaurantId.ToString() + "_" + t.ParsedNumber;
            if(!groups.ContainsKey(key)) groups[key] = new List<string>();
            groups[key].Add(t.NameOrNumber);
        }
        
        foreach(var g in groups) {
            if(g.Value.Count > 1) {
                conflict = true;
                Console.WriteLine($"Numeric Conflict in {g.Key}: {string.Join(", ", g.Value)}");
            }
        }
        
        if(!conflict) {
            Console.WriteLine("No numeric conflicts found.");
        }
    }
}
