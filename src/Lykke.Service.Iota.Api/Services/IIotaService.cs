using Lykke.Service.Iota.Api.Core.Domain.Broadcast;
using System;
using System.Threading.Tasks;

namespace Lykke.Service.Iota.Api.Services
{
    public interface IIotaService
    {
        object GetAddress(string address);

        object GetTransaction(string transactionHex);

        Task<string> BuildTransactionAsync(Guid operationId, object fromAddress,
            object toAddress, decimal amount, bool includeFee);

        Task BroadcastAsync(object transaction, Guid operationId);

        Task<IBroadcast> GetBroadcastAsync(Guid operationId);

        Task DeleteBroadcastAsync(IBroadcast broadcast);

        Task<decimal> GetAddressBalance(string address);

        decimal GetFee();
    }
}
