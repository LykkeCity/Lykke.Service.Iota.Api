using System.Threading.Tasks;
using AzureStorage;
using AzureStorage.Tables;
using Common;
using Common.Log;
using Lykke.SettingsReader;
using Lykke.Service.Iota.Api.Core.Repositories;

namespace Lykke.Service.Iota.Api.AzureRepositories
{
    public class AddressVirtualRepository : IAddressVirtualRepository
    {
        private INoSQLTableStorage<AddressVirtualEntity> _table;
        private static string GetPartitionKey(string address) => address.CalculateHexHash32(3);
        private static string GetRowKey(string address) => address;

        public AddressVirtualRepository(IReloadingManager<string> connectionStringManager, ILog log)
        {
            _table = AzureTableStorage<AddressVirtualEntity>.Create(connectionStringManager, "AddressVirtuals", log);
        }

        public async Task<string> GetVirtualAddressAsync(string address)
        {
            var item = await _table.GetDataAsync(GetPartitionKey(address), GetRowKey(address));

            return item?.VirtualAddress;
        }

        public async Task SaveAsync(string address, string virtualAddress)
        {
            await _table.InsertOrReplaceAsync(new AddressVirtualEntity
            {
                PartitionKey = GetPartitionKey(address),
                RowKey = GetRowKey(address),
                VirtualAddress = virtualAddress
            });
        }

        public async Task DeleteAsync(string address)
        {
            await _table.DeleteIfExistAsync(GetPartitionKey(address), GetRowKey(address));
        }
    }
}
