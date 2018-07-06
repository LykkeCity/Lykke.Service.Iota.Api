using System.Threading.Tasks;
using System.Collections.Generic;
using AzureStorage;
using AzureStorage.Tables;
using Common.Log;
using Lykke.SettingsReader;
using Lykke.Service.Iota.Api.Core.Domain.Address;
using Lykke.Service.Iota.Api.Core.Repositories;
using System;

namespace Lykke.Service.Iota.Api.AzureRepositories
{
    public class AddressTransactionRepository : IAddressTransactionRepository
    {
        private INoSQLTableStorage<AddressTransactionEntity> _table;
        private static string GetPartitionKey(string addressVirtual) => addressVirtual;
        private static string GetRowKey(string hash) => hash;
        //private static string GetRowKey() => DateTime.UtcNow.ToString("o");

        public AddressTransactionRepository(IReloadingManager<string> connectionStringManager, ILog log)
        {
            _table = AzureTableStorage<AddressTransactionEntity>.Create(connectionStringManager, "AddressTransactions", log);
        }

        public async Task<(IEnumerable<IAddressTransaction> Entities, string ContinuationToken)> GetAsync(string addressVirtual, int take, string continuation)
        {
            return await _table.GetDataWithContinuationTokenAsync(GetPartitionKey(addressVirtual), take, continuation);
        }

        public async Task SaveAsync(string addressVirtual, string hash, string context, Guid operationId)
        {
            await _table.InsertOrReplaceAsync(new AddressTransactionEntity
            {
                PartitionKey = GetPartitionKey(addressVirtual),
                RowKey = GetRowKey(hash),
                Context = context,
                OperationId = operationId
            });
        }

        public async Task DeleteAsync(string addressVirtual, string hash)
        {
            await _table.DeleteIfExistAsync(GetPartitionKey(addressVirtual), GetRowKey(hash));
        }
    }
}
