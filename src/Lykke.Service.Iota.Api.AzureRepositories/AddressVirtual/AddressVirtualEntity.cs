using Lykke.AzureStorage.Tables;

namespace Lykke.Service.Iota.Api.AzureRepositories
{
    internal class AddressVirtualEntity : AzureTableEntity
    {
        public string Address
        {
            get => RowKey;
        }

        public string VirtualAddress { get; set; }
    }
}
