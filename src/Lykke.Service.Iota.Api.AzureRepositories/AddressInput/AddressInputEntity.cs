using Lykke.AzureStorage.Tables;
using Lykke.AzureStorage.Tables.Entity.Annotation;
using Lykke.AzureStorage.Tables.Entity.ValueTypesMerging;
using Lykke.Service.Iota.Api.Core.Domain.Address;

namespace Lykke.Service.Iota.Api.AzureRepositories.AddressVirtual
{
    [ValueTypeMergingStrategy(ValueTypeMergingStrategy.UpdateAlways)]
    internal class AddressInputEntity : AzureTableEntity, IAddressInput
    {
        public string AddressVirtual
        {
            get => PartitionKey;
        }

        public string Address
        {
            get => RowKey;
        }

        public long Index { get; set; }
    }
}
