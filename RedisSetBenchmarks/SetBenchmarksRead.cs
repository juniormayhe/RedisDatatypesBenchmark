﻿namespace RedisSetBenchmarks
{
    using BenchmarkDotNet.Attributes;
    using BenchmarkDotNet.Order;

    using Infrastructure.CrossCutting.Cache;

    using RedisShared;

    using System.Collections.Generic;

    [RankColumn]
    [Orderer(SummaryOrderPolicy.FastestToSlowest, MethodOrderPolicy.Declared)]
    [MemoryDiagnoser]
    public class SetBenchmarksRead
    {
        public IEnumerable<RoutingLog> ListForReading { get; set; }

        public ICacheStore Cache { get; set; }

        [GlobalSetup]
        public void InitialData()
        {
            this.Cache = CacheHelper.GetCacheStore();

            // warmup cache for further reading
            this.ListForReading = Seed.BuildReasons(totalKeys: 2, totalReasons: 2, totalRemovedEntities: 4);
            this.WarmUpCacheForReadingWithRequestIdAsKey();
            this.WarmUpCacheForReadingWithAllFieldsAsKey();
            this.WarmUpCacheForReadingWithRequestIdAndProductIdAsKey();
        }

        /**
         * Structure for hashes could be
         * 
         * Key - RequestId_GUID
         * |__ Field - ProductId:INT_VariantId:GUID_Reason:STRING, Value - semi colon delimited string
         * |__ Field - ProductId:INT_VariantId:GUID_Reason:STRING, Value - semi colon delimited string
         */
        [Benchmark]
        public void O4_Get_Set_RequestIdInKey()
        {
            var reasons = new Dictionary<string, IEnumerable<string>>();
            foreach (var item in ListForReading)
            {
                string key = $"o4_set:RequestId_{item.RequestId}";

                IDictionary<string, string> values = Cache.SetGet(key);

                List<string> items = new List<string>();
                foreach (var kvp in values)
                {
                    string productAndVariantAndReason = kvp.Key;
                    string reasonAndRemovedEntities = kvp.Value;
                    items.Add($"{productAndVariantAndReason}:{reasonAndRemovedEntities}");
                }

                reasons.Add(key, items);
            }
        }

        /**
         * Structure for hashes could be
         * 
         * Key - RequestId_GUID:ProductId:INT
         * |__ Value - VariantId:GUID|Reason:STRING:Entities
         * |__ Value - VariantId:GUID|Reason:STRING:Entities
         * 
         * where both Reason is a string and Entities is a semi colon delimited string
         */
        [Benchmark]
        public void O4_Get_Set_RequestIdAndProductIdInKey()
        {
            var reasons = new Dictionary<string, IEnumerable<string>>();
            foreach (var item in ListForReading)
            {
                string key = $"o4_set:RequestId_{item.RequestId}:ProductId_{item.ProductId}";

                IEnumerable<string> values = this.Cache.SetGet(key);

                List<string> items = new List<string>();
                foreach (string value in values)
                {
                    string[] v = value.Split(":");
                    string reasonAndVariant = v[0];
                    string[] removedEntities = v[1].Split(",");
                    reasons.Add(reasonAndVariant, removedEntities);
                }
                reasons.Add(key, items);
            }
        }

        /**
         * Structure for hashes could be
         * 
         * Key - RequestId_GUID:ProductId_INT:VariantId_GUID
         * |__ Value - Reason:Entities
         * |__ Value - Reason:Entities
         * 
         * where both Reason is a string and Entities is a semi colon delimited string
         */
        [Benchmark]
        public void O4_Get_Set_AllFieldsInKey()
        {
            var reasons = new Dictionary<string, IEnumerable<string>>();
            foreach (var item in this.ListForReading)
            {
                string key = $"o4_set:{item.GetFullKey()}";

                IEnumerable<string> values = this.Cache.SetGet(key);

                List<string> items = new List<string>();
                foreach (string value in values)
                {
                    string[] v = value.Split(":");
                    string reason = v[0];
                    string[] removedEntities = v[1].Split(",");
                    reasons.Add(reason, removedEntities);
                }
                reasons.Add(key, items);
            }
        }

        #region private methods
        private void WarmUpCacheForReadingWithRequestIdAsKey()
        {
            foreach (var item in this.ListForReading)
            {
                string key = $"o4_set:RequestId_{item.RequestId}";

                var entriesForSet = new List<string>();
                foreach (var removedEntityByReason in item.RemovedEntitiesByReason)
                {
                    string productVariantReasonKey = $"{item.GetProductVariantKey()}|Reason_{removedEntityByReason.Key}";
                    string entityIds = string.Join(",", removedEntityByReason.Value);

                    entriesForSet.Add($"{productVariantReasonKey}:{entityIds}");
                }

                this.Cache.SetAddAll(key, entriesForSet);
            }
        }

        private void WarmUpCacheForReadingWithRequestIdAndProductIdAsKey()
        {
            foreach (var item in this.ListForReading)
            {
                string key = $"o4_set:RequestId_{item.RequestId}:ProductId_{item.ProductId}";

                var entriesForSet = new List<string>();
                foreach (var removedEntityByReason in item.RemovedEntitiesByReason)
                {
                    string variantAndReason = $"VariantId_{item.VariantId}|Reason_{removedEntityByReason.Key}";
                    string entityIds = string.Join(",", removedEntityByReason.Value);

                    entriesForSet.Add($"{variantAndReason}:{entityIds}");
                }

                this.Cache.SetAddAll(key, entriesForSet);
            }
        }

        private void WarmUpCacheForReadingWithAllFieldsAsKey()
        {
            foreach (var item in this.ListForReading)
            {
                string key = $"o4_set:{item.GetFullKey()}";

                var entriesForSet = new List<string>();
                foreach (var removedEntityByReason in item.RemovedEntitiesByReason)
                {
                    string reasonCode = removedEntityByReason.Key;
                    string entityIds = string.Join(",", removedEntityByReason.Value);
                    
                    entriesForSet.Add($"{reasonCode}:{entityIds}");
                    
                }
                this.Cache.SetAddAll(key, entriesForSet);
            }
        }

        #endregion
    }
}