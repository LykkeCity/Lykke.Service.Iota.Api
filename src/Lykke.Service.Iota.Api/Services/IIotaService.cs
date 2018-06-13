using Lykke.Service.Iota.Api.Core.Domain.Broadcast;
using Lykke.Service.Iota.Api.Models;
using System;
using System.Threading.Tasks;

namespace Lykke.Service.Iota.Api.Services
{
    public interface IIotaService
    {
        bool ValidateAddress(string address);

        bool ValidateSignedTransaction(string transactionHex);

        Task BroadcastAsync(string signedTransaction, Guid operationId);

        Task<IBroadcast> GetBroadcastAsync(Guid operationId);

        Task DeleteBroadcastAsync(IBroadcast broadcast);

        Task<long> GetVirtualAddressBalance(string address);

        Task<AddressInput[]> GetVirtualAddressInputs(string virtualAddress);
    }
}
