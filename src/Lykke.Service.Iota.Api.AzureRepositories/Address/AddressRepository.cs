using System.Threading.Tasks;
using System.Collections.Generic;
using AzureStorage;
using AzureStorage.Tables;
using Common.Log;
using Lykke.SettingsReader;
using Lykke.Service.Iota.Api.Core.Domain.Address;
using Lykke.Service.Iota.Api.Core.Repositories;

namespace Lykke.Service.Iota.Api.AzureRepositories.Address
{
    public class AddressRepository : IAddressRepository
    {
        private INoSQLTableStorage<AddressEntity> _table;
        private static string GetPartitionKey(string addressVirtual) => addressVirtual;
        private static string GetRowKey(string address) => address;

        public AddressRepository(IReloadingManager<string> connectionStringManager, ILog log)
        {
            _table = AzureTableStorage<AddressEntity>.Create(connectionStringManager, "Addresses", log);
        }

        public async Task<(IEnumerable<IAddress> Entities, string ContinuationToken)> GetAsync(string addressVirtual, int take, string continuation)
        {
            return await _table.GetDataWithContinuationTokenAsync(GetPartitionKey(addressVirtual), take, continuation);
        }

        public async Task<IAddress> GetAsync(string addressVirtual, string address)
        {
            return await _table.GetDataAsync(GetPartitionKey(addressVirtual), GetRowKey(address));
        }

        public async Task SaveAsync(string addressVirtual, string address, long index, bool hasCashOut)
        {
            await _table.InsertOrReplaceAsync(new AddressEntity
            {
                PartitionKey = GetPartitionKey(addressVirtual),
                RowKey = GetRowKey(address),
                Index = index,
                HasCashOut = hasCashOut
            });
        }

        public async Task DeleteAsync(string addressVirtual, string address)
        {
            await _table.DeleteIfExistAsync(GetPartitionKey(addressVirtual), GetRowKey(address));
        }
    }
}
