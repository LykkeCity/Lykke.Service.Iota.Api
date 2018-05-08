using Lykke.AzureStorage.Tables;
using Lykke.AzureStorage.Tables.Entity.Annotation;
using Lykke.AzureStorage.Tables.Entity.ValueTypesMerging;
using Lykke.Service.Iota.Api.Core.Domain.Balance;

namespace Lykke.Service.Iota.Api.AzureRepositories.Balance
{
    [ValueTypeMergingStrategy(ValueTypeMergingStrategy.UpdateAlways)]
    internal class BalanceEntity : AzureTableEntity, IBalance
    {
        public string Address
        {
            get => RowKey;
        }
    }
}
