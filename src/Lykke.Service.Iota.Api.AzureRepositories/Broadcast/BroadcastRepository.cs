﻿using System;
using System.Threading.Tasks;
using AzureStorage;
using AzureStorage.Tables;
using Lykke.SettingsReader;
using Lykke.Service.Iota.Api.Core.Domain.Broadcast;
using Lykke.Service.Iota.Api.Core.Repositories;
using Common;
using Lykke.Common.Log;

namespace Lykke.Service.Iota.Api.AzureRepositories
{
    public class BroadcastRepository : IBroadcastRepository
    {
        private INoSQLTableStorage<BroadcastEntity> _table;
        private static string GetPartitionKey(Guid operationId) => operationId.ToString().CalculateHexHash32(3);
        private static string GetRowKey(Guid operationId) => operationId.ToString();

        public BroadcastRepository(IReloadingManager<string> connectionStringManager, ILogFactory logFactory)
        {
            _table = AzureTableStorage<BroadcastEntity>.Create(connectionStringManager, "Broadcasts", logFactory);
        }

        public async Task<IBroadcast> GetAsync(Guid operationId)
        {
            return await _table.GetDataAsync(GetPartitionKey(operationId), GetRowKey(operationId));
        }

        public async Task AddAsync(Guid operationId, string hash, long block)
        {
            await _table.InsertOrReplaceAsync(new BroadcastEntity
            {
                PartitionKey = GetPartitionKey(operationId),
                RowKey = GetRowKey(operationId),
                BroadcastedUtc = DateTime.UtcNow,
                State = BroadcastState.InProgress,
                Hash = hash,
                Block = block
            });
        }

        public async Task AddFailedAsync(Guid operationId, string error)
        {
            await _table.InsertOrReplaceAsync(new BroadcastEntity
            {
                PartitionKey = GetPartitionKey(operationId),
                RowKey = GetRowKey(operationId),
                FailedUtc = DateTime.UtcNow,
                State = BroadcastState.Failed,
                Error = error
            });
        }

        public async Task SaveAsCompletedAsync(Guid operationId, decimal amount, decimal fee, long block)
        {
            await _table.ReplaceAsync(GetPartitionKey(operationId), GetRowKey(operationId), x =>
            {
                x.State = BroadcastState.Completed;
                x.CompletedUtc = DateTime.UtcNow;
                x.Amount = amount;
                x.Fee = fee;
                x.Block = block;

                return x;
            });
        }

        public async Task DeleteAsync(Guid operationId)
        {
            await _table.DeleteIfExistAsync(GetPartitionKey(operationId), GetRowKey(operationId));
        }
    }
}
