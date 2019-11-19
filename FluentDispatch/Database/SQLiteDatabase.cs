using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using FluentDispatch.Helpers;
using SQLitePCL;

namespace FluentDispatch.Database
{
    internal class SQLiteDatabase
    {
        private static readonly Lazy<Task<SqliteConnection>> LazyConnection = new Lazy<Task<SqliteConnection>>(
            async () =>
            {
                if (!Directory.Exists(Constants.GCDCachePath))
                {
                    Directory.CreateDirectory(Constants.GCDCachePath);
                }

                var location = Directory.GetParent(Assembly.GetExecutingAssembly().Location);
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    var anyProvider = Directory
                        .GetFiles(location.FullName, "e_sqlite3.dll", SearchOption.TopDirectoryOnly)
                        .Any();
                    if (!anyProvider)
                    {
                        var x64 = Environment.Is64BitProcess;
                        Helper.ExtractResource(
                            x64
                                ? "FluentDispatch.Sqlite.e_sqlite3_x64.dll"
                                : "FluentDispatch.Sqlite.e_sqlite3_x86.dll",
                            $"{location.FullName}{Path.DirectorySeparatorChar}e_sqlite3.dll");
                    }
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    var anyProvider = Directory
                        .GetFiles(location.FullName, "libe_sqlite3.dylib", SearchOption.TopDirectoryOnly)
                        .Any();
                    if (!anyProvider)
                    {
                        Helper.ExtractResource("FluentDispatch.Sqlite.libe_sqlite3.dylib",
                            $"{location.FullName}{Path.DirectorySeparatorChar}libe_sqlite3.dylib");
                    }
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    var anyProvider = Directory
                        .GetFiles(location.FullName, "libe_sqlite3.so", SearchOption.TopDirectoryOnly)
                        .Any();
                    if (!anyProvider)
                    {
                        var x64 = Environment.Is64BitProcess;
                        Helper.ExtractResource(
                            x64
                                ? "FluentDispatch.Sqlite.libe_sqlite3_x64.so"
                                : "FluentDispatch.Sqlite.libe_sqlite3_x86.so",
                            $"{location.FullName}{Path.DirectorySeparatorChar}libe_sqlite3.so");
                    }
                }

                Batteries.Init();
                raw.sqlite3_config(raw.SQLITE_CONFIG_SERIALIZED);
                await EnsureCacheDbIsInitiated();
                var connectionString =
                    $"Data Source={Constants.PathToSqliteDbFile};";
                var conn = new SqliteConnection(connectionString);
                await conn.OpenAsync();
                return conn;
            }, LazyThreadSafetyMode.ExecutionAndPublication);

        private static async Task EnsureCacheDbIsInitiated()
        {
            var connectionString = $"Data Source={Constants.PathToSqliteDbFile};";
            using (var conn = new SqliteConnection(connectionString))
            {
                await conn.OpenAsync();
                using (var command =
                    new SqliteCommand(@"SELECT COUNT (*) FROM sqlite_master WHERE name = 'CacheItem' and type='table';",
                        conn))
                {
                    var reader = await command.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        if (!int.TryParse(reader.GetString(0), out var count) || count == 0)
                        {
                            using (var creationCommand = new SqliteCommand(
                                @"CREATE TABLE CacheItem (key TEXT, blob BLOB)",
                                conn))
                            {
                                await creationCommand.ExecuteNonQueryAsync();
                            }
                        }
                    }
                }

                using (var command =
                    new SqliteCommand(
                        @"SELECT COUNT (*) FROM sqlite_master WHERE name = 'CacheItem1' and type='table';",
                        conn))
                {
                    var reader = await command.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        if (!int.TryParse(reader.GetString(0), out var count) || count == 0)
                        {
                            using (var creationCommand = new SqliteCommand(
                                @"CREATE TABLE CacheItem1 (key TEXT, blob BLOB)",
                                conn))
                            {
                                await creationCommand.ExecuteNonQueryAsync();
                            }
                        }
                    }
                }

                using (var command =
                    new SqliteCommand(
                        @"SELECT COUNT (*) FROM sqlite_master WHERE name = 'CacheItem2' and type='table';",
                        conn))
                {
                    var reader = await command.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        if (!int.TryParse(reader.GetString(0), out var count) || count == 0)
                        {
                            using (var creationCommand = new SqliteCommand(
                                @"CREATE TABLE CacheItem2 (key TEXT, blob BLOB)",
                                conn))
                            {
                                await creationCommand.ExecuteNonQueryAsync();
                            }
                        }
                    }
                }
            }
        }

        public static Task<SqliteConnection> Connection => LazyConnection.Value;
    }
}