using System.Threading.Tasks;
using AzureStorage;
using AzureStorage.Tables;
using Common;
using Common.Log;
using Lykke.SettingsReader;
using Lykke.Service.Iota.Api.Core.Domain.Address;
using Lykke.Service.Iota.Api.Core.Repositories;

namespace Lykke.Service.Iota.Api.AzureRepositories.AddressVirtual
{
    public class AddressVirtualRepository : IAddressVirtualRepository
    {
        private INoSQLTableStorage<AddressVirtualEntity> _table;
        private static string GetPartitionKey(string addressVirtual) => addressVirtual.CalculateHexHash32(3);
        private static string GetRowKey(string addressVirtual) => addressVirtual;

        public AddressVirtualRepository(IReloadingManager<string> connectionStringManager, ILog log)
        {
            _table = AzureTableStorage<AddressVirtualEntity>.Create(connectionStringManager, "AddressVirtuals", log);
        }

        public async Task<IAddressVirtual> GetAsync(string addressVirtual)
        {
            return await _table.GetDataAsync(GetPartitionKey(addressVirtual), GetRowKey(addressVirtual));
        }

        public async Task SaveAsync(string addressVirtual, string latestAddress, long latestAddressIndex)
        {
            await _table.InsertOrReplaceAsync(new AddressVirtualEntity
            {
                PartitionKey = GetPartitionKey(addressVirtual),
                RowKey = GetRowKey(addressVirtual),
                LatestAddress = latestAddress,
                LatestAddressIndex = latestAddressIndex
            });
        }

        public async Task DeleteAsync(string addressVirtual)
        {
            await _table.DeleteIfExistAsync(GetPartitionKey(addressVirtual), GetRowKey(addressVirtual));
        }
    }
}
