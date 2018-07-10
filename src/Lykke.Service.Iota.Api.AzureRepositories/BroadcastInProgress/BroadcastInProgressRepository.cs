using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AzureStorage;
using AzureStorage.Tables;
using Lykke.SettingsReader;
using Lykke.Service.Iota.Api.Core.Repositories;
using Lykke.Service.Iota.Api.Core.Domain.Broadcast;
using Common;
using Lykke.Common.Log;

namespace Lykke.Service.Iota.Api.AzureRepositories
{
    public class BroadcastInProgressRepository : IBroadcastInProgressRepository
    {
        private readonly INoSQLTableStorage<BroadcastInProgressEntity> _table;
        private static string GetPartitionKey(Guid operationId) => operationId.ToString().CalculateHexHash32(3);
        private static string GetRowKey(Guid operationId) => operationId.ToString();

        public BroadcastInProgressRepository(IReloadingManager<string> connectionStringManager, ILogFactory logFactory)
        {
            _table = AzureTableStorage<BroadcastInProgressEntity>.Create(connectionStringManager, "BroadcastsInProgress", logFactory);
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
