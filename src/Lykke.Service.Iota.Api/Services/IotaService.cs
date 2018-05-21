using Common.Log;
using Lykke.Service.Iota.Api.Core.Domain.Broadcast;
using Lykke.Service.Iota.Api.Core.Repositories;
using System;
using System.Threading.Tasks;

namespace Lykke.Service.Iota.Api.Services
{
    public class IotaService : IIotaService
    {
        private readonly ILog _log;
        private readonly IBroadcastRepository _broadcastRepository;
        private readonly IBroadcastInProgressRepository _broadcastInProgressRepository;
        private readonly int _minConfirmations;

        public IotaService(ILog log,
            IBroadcastRepository broadcastRepository,
            IBroadcastInProgressRepository broadcastInProgressRepository,
            IBalanceRepository balanceRepository,
            IBalancePositiveRepository balancePositiveRepository,
            decimal fee,
            int minConfirmations)
        {
            _log = log;
            _broadcastRepository = broadcastRepository;
            _broadcastInProgressRepository = broadcastInProgressRepository;
            _minConfirmations = minConfirmations;
        }

        public object GetAddress(string address)
        {
            return null;
        }

        public object GetTransaction(string transactionHex)
        {
            return null;
        }

        public async Task<string> BuildTransactionAsync(Guid operationId, object fromAddress,
            object toAddress, decimal amount, bool includeFee)
        {
            await Task.Yield();

            return "";
        }

        public async Task BroadcastAsync(object transaction, Guid operationId)
        {
            await Task.Yield();
        }

        public async Task<IBroadcast> GetBroadcastAsync(Guid operationId)
        {
            return await _broadcastRepository.GetAsync(operationId);
        }

        public async Task DeleteBroadcastAsync(IBroadcast broadcast)
        {
            await _broadcastInProgressRepository.DeleteAsync(broadcast.OperationId);
            await _broadcastRepository.DeleteAsync(broadcast.OperationId);
        }

        public async Task<decimal> GetAddressBalance(string address)
        {
            await Task.Yield();

            return 0;
        }

        public decimal GetFee()
        {
            return 0;
        }        
    }
}
