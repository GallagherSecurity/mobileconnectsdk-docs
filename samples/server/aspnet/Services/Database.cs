using Dapper;
using Microsoft.Data.Sqlite;

#nullable enable

namespace GallagherUniversityStudentPortalSampleSite.Services
{
    public sealed class Database : IDisposable
    {
        public static async Task Setup(bool resetDatabase = false)
        {
            Directory.CreateDirectory("Data");

            var dbExisted = File.Exists("Data/database.sqlitedb");

            using var db = new Database();

            if (!dbExisted || resetDatabase)
            {
                await db.RunAsync("DROP TABLE IF EXISTS students");
                await db.RunAsync("CREATE TABLE IF NOT EXISTS students (id INTEGER PRIMARY KEY, username TEXT UNIQUE, studentId TEXT UNIQUE, password TEXT, commandCentreHref TEXT)");
            }
        }

        readonly SqliteConnection _connection;
        
        public Database()
        {
            var builder = new SqliteConnectionStringBuilder { DataSource = "Data/database.sqlitedb" };

            _connection = new SqliteConnection(connectionString: builder.ConnectionString);
            _connection.Open();
        }

        public void Dispose() => _connection.Close();
        
        public Task<int> RunAsync(string sqlCommand, object? param = null)
            => _connection.ExecuteAsync(sqlCommand, param); // dapper does all the work

        public Task<T?> GetAsync<T>(string sqlCommand, object? param = null) where T : class
            => _connection.QueryFirstOrDefaultAsync<T?>(sqlCommand, param);

        public Task<IEnumerable<T>> AllAsync<T>(string sqlCommand, object? param = null)
            => _connection.QueryAsync<T>(sqlCommand, param);
    }
}
