using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MessagePack;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Primitives;
using Microsoft.IO;

namespace FluentDispatch.Cache
{
    internal class PersistentCacheProvider : ICacheProvider
    {
        private readonly SqliteConnection _dbConnection;
        private readonly RecyclableMemoryStreamManager _recyclableMemoryStreamManager;
        private readonly LazyCache.IAppCache _cache;
        private readonly ILogger _logger;

        public PersistentCacheProvider(SqliteConnection dbConnection, LazyCache.IAppCache cache,
            ILoggerFactory loggerFactory)
        {
            _dbConnection = dbConnection;
            _cache = cache;
            _recyclableMemoryStreamManager = new RecyclableMemoryStreamManager();
            _logger = loggerFactory == null
                ? NullLogger<PersistentCacheProvider>.Instance
                : loggerFactory.CreateLogger<PersistentCacheProvider>();
        }

        public async Task AddItemAsync<TInput>(TInput item, CancellationToken ct)
        {
            try
            {
                var key = Guid.NewGuid().ToString();
                _logger.LogDebug($"Adding key {key} to persistent storage.");
                _cache.Add(key, item, new MemoryCacheEntryOptions
                {
                    Size = 1,
                    Priority = CacheItemPriority.Normal,
                    ExpirationTokens = { new CancellationChangeToken(ct) },
                    PostEvictionCallbacks =
                    {
                        new PostEvictionCallbackRegistration
                        {
                            EvictionCallback = PostEvictionCallback
                        }
                    }
                });

                var command = new SqliteCommand("INSERT INTO CacheItem (key, blob) VALUES(@key, @blob)",
                    _dbConnection);
                command.Parameters.Add(new SqliteParameter("key", key));
                command.Parameters.Add(new SqliteParameter("blob", await Serialize(item)));
                await command.ExecuteNonQueryAsync(CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
            }
        }

        public async Task AddItem1Async<TInput1>(string key, TInput1 item,
            CancellationToken ct)
        {
            try
            {
                _logger.LogDebug($"Adding key {key} to persistent storage.");
                _cache.Add(key, item, new MemoryCacheEntryOptions
                {
                    Size = 1,
                    Priority = CacheItemPriority.Normal,
                    ExpirationTokens = { new CancellationChangeToken(ct) },
                    PostEvictionCallbacks =
                    {
                        new PostEvictionCallbackRegistration
                        {
                            EvictionCallback = PostEvictionCallbackItem1
                        }
                    }
                });

                var command = new SqliteCommand("INSERT INTO CacheItem1 (key, blob) VALUES(@key, @blob)",
                    _dbConnection);
                command.Parameters.Add(new SqliteParameter("key", key));
                command.Parameters.Add(new SqliteParameter("blob", await Serialize(item)));
                await command.ExecuteNonQueryAsync(CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
            }
        }

        public async Task AddItem2Async<TInput2>(string key, TInput2 item,
            CancellationToken ct)
        {
            try
            {
                _logger.LogDebug($"Adding key {key} to persistent storage.");
                _cache.Add(key, item, new MemoryCacheEntryOptions
                {
                    Size = 1,
                    Priority = CacheItemPriority.Normal,
                    ExpirationTokens = { new CancellationChangeToken(ct) },
                    PostEvictionCallbacks =
                    {
                        new PostEvictionCallbackRegistration
                        {
                            EvictionCallback = PostEvictionCallbackItem2
                        }
                    }
                });

                var command = new SqliteCommand("INSERT INTO CacheItem2 (key, blob) VALUES(@key, @blob)",
                    _dbConnection);
                command.Parameters.Add(new SqliteParameter("key", key));
                command.Parameters.Add(new SqliteParameter("blob", await Serialize(item)));
                await command.ExecuteNonQueryAsync(CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
            }
        }

        public async Task<IEnumerable<TOutput>> RetrieveItemsAsync<TOutput>()
        {
            var items = new List<TOutput>();
            try
            {
                var command = new SqliteCommand("SELECT blob FROM CacheItem",
                    _dbConnection);
                var reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess);
                while (reader.Read())
                {
                    var buffer = GetBytes(reader, 0);
                    var item = await Deserialize<TOutput>(buffer);
                    items.Add(item);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
            }

            return items;
        }

        public async Task<IEnumerable<(string key, TOutput1 item1)>> RetrieveItems1Async<TOutput1>()
        {
            var items = new List<(string key, TOutput1 item1)>();
            try
            {
                var command = new SqliteCommand("SELECT key, blob FROM CacheItem1",
                    _dbConnection);
                var reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess);
                while (reader.Read())
                {
                    var key = reader.GetString(0);
                    var buffer = GetBytes(reader, 1);
                    var item = await Deserialize<TOutput1>(buffer);
                    items.Add((key, item));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
            }

            return items;
        }

        public async Task<IEnumerable<(string key, TOutput2 item2)>> RetrieveItems2Async<TOutput2>()
        {
            var items = new List<(string key, TOutput2 item2)>();
            try
            {
                var command = new SqliteCommand("SELECT key, blob FROM CacheItem2",
                    _dbConnection);
                var reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess);
                while (reader.Read())
                {
                    var key = reader.GetString(0);
                    var buffer = GetBytes(reader, 1);
                    var item = await Deserialize<TOutput2>(buffer);
                    items.Add((key, item));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
            }

            return items;
        }

        public async Task FlushDatabaseAsync()
        {
            try
            {
                const string query = @"DELETE FROM CacheItem;DELETE FROM CacheItem1;DELETE FROM CacheItem2;vacuum;";
                var command = new SqliteCommand(query,
                    _dbConnection);
                await command.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
            }
        }

        private void PostEvictionCallback<TOutput>(object key, TOutput value, EvictionReason reason, object state)
        {
            try
            {
                _logger.LogDebug($"Eviction of key {key}: {reason} from persistent storage.");
                var command = new SqliteCommand("DELETE FROM CacheItem WHERE key = @key",
                    _dbConnection);
                command.Parameters.Add(new SqliteParameter("key", key));
                command.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
            }
        }

        private void PostEvictionCallbackItem1<TOutput1>(object key, TOutput1 value, EvictionReason reason,
            object state)
        {
            try
            {
                _logger.LogDebug($"Eviction of key {key}: {reason} from persistent storage.");
                var command = new SqliteCommand("DELETE FROM CacheItem1 WHERE key = @key",
                    _dbConnection);
                command.Parameters.Add(new SqliteParameter("key", key));
                command.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
            }
        }

        private void PostEvictionCallbackItem2<TOutput2>(object key, TOutput2 value, EvictionReason reason,
            object state)
        {
            try
            {
                _logger.LogDebug($"Eviction of key {key}: {reason} from persistent storage.");
                var command = new SqliteCommand("DELETE FROM CacheItem2 WHERE key = @key",
                    _dbConnection);
                command.Parameters.Add(new SqliteParameter("key", key));
                command.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
            }
        }

        private async Task<byte[]> Serialize<TInput>(TInput input)
        {
            using (var memoryStream = _recyclableMemoryStreamManager.GetStream(nameof(Serialize)))
            {
                await MessagePackSerializer.SerializeAsync(memoryStream, input);
                return memoryStream.ToArray();
            }
        }

        private async Task<TOutput> Deserialize<TOutput>(byte[] data)
        {
            using (var memoryStream = _recyclableMemoryStreamManager.GetStream(nameof(Deserialize), data, 0, data.Length))
            {
                return await MessagePackSerializer.DeserializeAsync<TOutput>(memoryStream);
            }
        }

        private byte[] GetBytes(IDataRecord reader, int column)
        {
            const int chunkSize = 2 * 1024;
            var buffer = new byte[chunkSize];
            long fieldOffset = 0;
            using (var stream = _recyclableMemoryStreamManager.GetStream(nameof(GetBytes)))
            {
                long bytesRead;
                while ((bytesRead = reader.GetBytes(column, fieldOffset, buffer, 0, buffer.Length)) > 0)
                {
                    stream.Write(buffer, 0, (int)bytesRead);
                    fieldOffset += bytesRead;
                }

                return stream.ToArray();
            }
        }
    }
}