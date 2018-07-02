using Lykke.AzureStorage.Tables;
using Lykke.AzureStorage.Tables.Entity.Annotation;
using Lykke.AzureStorage.Tables.Entity.ValueTypesMerging;
using Lykke.Service.Iota.Api.Core.Domain.Address;
using System;

namespace Lykke.Service.Iota.Api.AzureRepositories
{
    [ValueTypeMergingStrategy(ValueTypeMergingStrategy.UpdateAlways)]
    internal class AddressTransactionEntity : AzureTableEntity, IAddressTransaction
    {
        public string AddressVirtual { get => PartitionKey; }
        public string Hash { get => RowKey; }
        public string Context { get; set; }
        public Guid OperationId { get; set; }
    }
}
