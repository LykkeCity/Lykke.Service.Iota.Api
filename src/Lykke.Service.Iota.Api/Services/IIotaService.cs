using Lykke.Service.Iota.Api.Core.Domain.Address;
using Lykke.Service.Iota.Api.Core.Domain.Broadcast;
using System;
using System.Threading.Tasks;

namespace Lykke.Service.Iota.Api.Services
{
    public interface IIotaService
    {
        bool ValidateAddress(string address);

        object GetTransaction(string transactionHex);

        Task BroadcastAsync(object transaction, Guid operationId);

        Task<IBroadcast> GetBroadcastAsync(Guid operationId);

        Task DeleteBroadcastAsync(IBroadcast broadcast);

        Task<long> GetAddressBalance(string address);
    }
}
