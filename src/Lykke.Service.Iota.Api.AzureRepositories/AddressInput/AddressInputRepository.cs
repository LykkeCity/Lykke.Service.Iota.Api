using System.Threading.Tasks;
using AzureStorage;
using AzureStorage.Tables;
using Common;
using Common.Log;
using Lykke.SettingsReader;
using Lykke.Service.Iota.Api.Core.Domain.Address;
using Lykke.Service.Iota.Api.Core.Repositories;
using System.Collections.Generic;
using System.Linq;

namespace Lykke.Service.Iota.Api.AzureRepositories
{
    public class AddressInputRepository : IAddressInputRepository
    {
        private INoSQLTableStorage<AddressInputEntity> _table;
        private static string GetPartitionKey(string addressVirtual) => addressVirtual;
        private static string GetRowKey(string address) => address;

        public AddressInputRepository(IReloadingManager<string> connectionStringManager, ILog log)
        {
            _table = AzureTableStorage<AddressInputEntity>.Create(connectionStringManager, "AddressInputs", log);
        }

        public async Task<IEnumerable<IAddressInput>> GetAsync(string addressVirtual)
        {
            return (await _table.GetDataAsync(GetPartitionKey(addressVirtual)))
                .OrderBy(f => f.Index);
        }

        public async Task<IAddressInput> GetAsync(string addressVirtual, string address)
        {
            return await _table.GetDataAsync(GetPartitionKey(addressVirtual), GetRowKey(address));
        }

        public async Task SaveAsync(string addressVirtual, string address, long index)
        {
            await _table.InsertOrReplaceAsync(new AddressInputEntity
            {
                PartitionKey = GetPartitionKey(addressVirtual),
                RowKey = GetRowKey(address),
                Index = index
            });
        }

        public async Task DeleteAsync(string addressVirtual, string address)
        {
            await _table.DeleteIfExistAsync(GetPartitionKey(addressVirtual), GetRowKey(address));
        }
    }
}
