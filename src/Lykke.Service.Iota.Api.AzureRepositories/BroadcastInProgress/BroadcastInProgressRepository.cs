using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AzureStorage;
using AzureStorage.Tables;
using Common.Log;
using Lykke.SettingsReader;
using Lykke.Service.Iota.Api.Core.Repositories;
using Lykke.Service.Iota.Api.Core.Domain.Broadcast;
using Common;

namespace Lykke.Service.Iota.Api.AzureRepositories.BroadcastInProgress
{
    public class BroadcastInProgressRepository : IBroadcastInProgressRepository
    {
        private readonly INoSQLTableStorage<BroadcastInProgressEntity> _table;
        private static string GetPartitionKey(Guid operationId) => operationId.ToString().CalculateHexHash32(3);
        private static string GetRowKey(Guid operationId) => operationId.ToString();

        public BroadcastInProgressRepository(IReloadingManager<string> connectionStringManager, ILog log)
        {
            _table = AzureTableStorage<BroadcastInProgressEntity>.Create(connectionStringManager, "BroadcastsInProgress", log);
        }

        public async Task<IEnumerable<IBroadcastInProgress>> GetAllAsync()
        {
            return await _table.GetDataAsync();
        }

        public async Task AddAsync(Guid operationId, string hash)
        {
            await _table.InsertOrReplaceAsync(new BroadcastInProgressEntity
            {
                PartitionKey = GetPartitionKey(operationId),
                RowKey = GetRowKey(operationId),
                Hash = hash
            });
        }

        public async Task DeleteAsync(Guid operationId)
        {
            await _table.DeleteIfExistAsync(GetPartitionKey(operationId), GetRowKey(operationId));
        }
    }
}
