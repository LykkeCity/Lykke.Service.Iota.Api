using Lykke.AzureStorage.Tables;
using Lykke.AzureStorage.Tables.Entity.Annotation;
using Lykke.AzureStorage.Tables.Entity.ValueTypesMerging;
using Lykke.Service.Iota.Api.Core.Domain.Address;

namespace Lykke.Service.Iota.Api.AzureRepositories.AddressVirtual
{
    [ValueTypeMergingStrategy(ValueTypeMergingStrategy.UpdateAlways)]
    internal class AddressVirtualEntity : AzureTableEntity, IAddressVirtual
    {
        public string AddressVirtual
        {
            get => RowKey;
        }

        public long LatestAddressIndex { get; set; }
    }
}
