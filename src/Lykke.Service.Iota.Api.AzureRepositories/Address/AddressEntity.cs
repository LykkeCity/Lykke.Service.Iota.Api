using Lykke.AzureStorage.Tables;
using Lykke.AzureStorage.Tables.Entity.Annotation;
using Lykke.AzureStorage.Tables.Entity.ValueTypesMerging;
using Lykke.Service.Iota.Api.Core.Domain.Address;

namespace Lykke.Service.Iota.Api.AzureRepositories.Address
{
    [ValueTypeMergingStrategy(ValueTypeMergingStrategy.UpdateAlways)]
    internal class AddressEntity : AzureTableEntity, IAddress
    {
        public string Address
        {
            get => RowKey;
        }

        public string AddressVirtual
        {
            get => PartitionKey;
        }

        public long Index { get; set; }
    }
}
