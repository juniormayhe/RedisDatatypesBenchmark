﻿namespace Infrastructure.CrossCutting.Cache.Redis
{
    using Infrastructure.CrossCutting.Settings;

    using Newtonsoft.Json;

    using StackExchange.Redis;

    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    public class RedisCacheStore : ICacheStore
    {
        private readonly IDatabase database;
        private readonly CommandFlags readFlag;
        private readonly IServer server;

        public RedisCacheStore(IRedisConnectionWrapper connectionWrapper, RedisSettings redisSettings)
        {
            this.database = connectionWrapper.Database(redisSettings.DefaultDb);
            this.server = connectionWrapper.FirstServer();
            this.readFlag = redisSettings.PreferSlaveForRead ? CommandFlags.PreferSlave : CommandFlags.PreferMaster;
        }

        async Task<T> ICacheStore.GetJsonAsync<T>(string key)
        {
            try
            {
                var cacheValue = await this.database.StringGetAsync(key, this.readFlag).ConfigureAwait(false);

                if (!string.IsNullOrEmpty(cacheValue))
                {
                    return JsonConvert.DeserializeObject<T>(cacheValue);
                }
            }
            catch (Exception ex)
            {

            }

            return default(T);
        }

        async Task ICacheStore.SetJsonAsync<T>(string key, T value, TimeSpan? expiry = default(TimeSpan?))
        {
            try
            {
                await this.database.StringSetAsync(key, JsonConvert.SerializeObject(value), expiry).ConfigureAwait(false);
            }
            catch (Exception ex)
            {

            }
        }

        async Task ICacheStore.RemoveAsync(string key)
        {
            try
            {
                await this.database.KeyDeleteAsync(key).ConfigureAwait(false);
            }
            catch (Exception ex)
            {

            }
        }

        T ICacheStore.GetJson<T>(string key)
        {
            try
            {
                var cacheValue = this.database.StringGet(key, CommandFlags.PreferSlave);

                if (!string.IsNullOrEmpty(cacheValue))
                {
                    return JsonConvert.DeserializeObject<T>(cacheValue);
                }
            }
            catch (Exception ex)
            {
            }

            return default(T);
        }

        void ICacheStore.JsonSet<T>(string key, T value, TimeSpan? expiry = default)
        {
            try
            {
                this.database.StringSet(key, JsonConvert.SerializeObject(value), expiry);
            }
            catch (Exception ex)
            {
            }
        }

        void ICacheStore.Remove(string key)
        {
            try
            {
                this.database.KeyDelete(key);
            }
            catch (Exception ex)
            {
            }
        }


        string ICacheStore.StringGet(string key)
        {
            string result = null;
            try
            {
                result = this.database.StringGet(key, CommandFlags.PreferSlave);
            }
            catch (Exception ex)
            {
                throw new ArgumentException(key);
            }

            return result;
        }

        void ICacheStore.StringSet(string key, string value, TimeSpan? expiredIn)
        {
            try
            {
                this.database.StringSet(key, value, expiredIn);
            }
            catch (Exception ex)
            {
            }
        }

        async Task<string> ICacheStore.StringGetAsync(string key)
        {
            string result = null;
            try
            {
                result = await this.database.StringGetAsync(key, this.readFlag).ConfigureAwait(false);

            }
            catch (Exception ex)
            {

            }

            return result;
        }

        async Task ICacheStore.StringSetAsync(string key, string value, TimeSpan? expiredIn)
        {
            try
            {
                await this.database.StringSetAsync(key, value, expiredIn).ConfigureAwait(false);
            }
            catch (Exception ex)
            {

            }
        }

        public void Truncate(string[] patterns)
        {
            foreach (var item in patterns)
            {
                IEnumerable<RedisKey> routingKeys = this.server.Keys(database: 5, pattern: item);

                foreach (var key in routingKeys)
                {
                    database.KeyDelete(key);
                }
            }
        }

        #region hashes
        void ICacheStore.HashSet(string key, IDictionary<string, string> values, TimeSpan? expiredIn = null)
        {
            try
            {
                var entries = values.Select(kvp => new HashEntry(kvp.Key, kvp.Value)).ToArray();
                //Redis does not provides direct ability to set expiration on individual keys inside hashset. 
                this.database.HashSet(key, entries.ToArray(), CommandFlags.FireAndForget);
                if (expiredIn != null)
                {
                    this.database.KeyExpire(key, expiredIn);
                }
            }
            catch (Exception ex)
            {
            }
        }

        async void ICacheStore.HashSetAsync(string key, IDictionary<string, string> values, TimeSpan? expiredIn = null)
        {
            try
            {
                var entries = values.Select(kvp => new HashEntry(kvp.Key, kvp.Value)).ToArray();
                //Redis does not provides direct ability to set expiration on individual keys inside hashset. 
                await this.database.HashSetAsync(key, entries.ToArray(), CommandFlags.FireAndForget).ConfigureAwait(false);
                if (expiredIn != null)
                {
                    await this.database.KeyExpireAsync(key, expiredIn).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
            }
        }

        IDictionary<string, string> ICacheStore.HashGet(string key)
        {
            IDictionary<string, string> values = null;

            try
            {
                HashEntry[] entries = this.database.HashGetAll(key);
                values = entries.ToDictionary(p => p.Name.ToString(), p => p.Value.ToString());
            }
            catch (Exception ex)
            {
            }
            return values;
        }

        async Task<IDictionary<string, string>> ICacheStore.HashGetAsync(string key)
        {
            IDictionary<string, string> values = null;

            try
            {
                HashEntry[] entries = await this.database.HashGetAllAsync(key).ConfigureAwait(false);
                values = entries.ToDictionary(p => p.Name.ToString(), p => p.Value.ToString());
            }
            catch (Exception ex)
            {
            }
            return values;
        }

        #endregion

        #region sets
        void ICacheStore.SetAddAll(string key, IEnumerable<string> values, TimeSpan? expiredIn = default)
        {
            try
            {
                foreach (var value in values)
                {
                    this.database.SetAdd(key, (RedisValue)value, CommandFlags.FireAndForget);
                    this.database.KeyExpire(key, expiredIn);
                }
            }
            catch (Exception ex)
            {
            }
        }

        async void ICacheStore.SetAddAllAsync(string key, IEnumerable<string> values, TimeSpan? expiredIn = default)
        {
            try
            {
                foreach (var value in values)
                {
                    await this.database.SetAddAsync(key, (RedisValue)value, CommandFlags.FireAndForget).ConfigureAwait(false);
                    await this.database.KeyExpireAsync(key, expiredIn, CommandFlags.FireAndForget).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
            }
        }

        IEnumerable<string> ICacheStore.SetGetAll(string key)
        {
            IEnumerable<string> result = null;
            try
            {
                result = this.database.SetMembers(key, CommandFlags.PreferSlave)
                    .Select(x => x.ToString());
            }
            catch (Exception ex)
            {
                throw new ArgumentException(key);
            }

            return result;
        }

        async Task<IEnumerable<string>> ICacheStore.SetGetAllAsync(string key)
        {
            IEnumerable<string> result = null;
            try
            {
                var t = await this.database.SetMembersAsync(key, CommandFlags.PreferSlave)
                    .ConfigureAwait(false);
                result = t.Select(x => x.ToString());
            }
            catch (Exception ex)
            {
                throw new ArgumentException(key);
            }

            return result;
        }

        #endregion
    }
}
