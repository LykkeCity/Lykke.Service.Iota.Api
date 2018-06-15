using Common.Log;
using Lykke.Service.Iota.Api.Core.Domain;
using Lykke.Service.Iota.Api.Core.Domain.Broadcast;
using Lykke.Service.Iota.Api.Core.Repositories;
using Lykke.Service.Iota.Api.Core.Services;
using Lykke.Service.Iota.Api.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Tangle.Net.Entity;

namespace Lykke.Service.Iota.Api.Services
{
    public class IotaService : IIotaService
    {
        private readonly ILog _log;
        private readonly IBroadcastRepository _broadcastRepository;
        private readonly IBroadcastInProgressRepository _broadcastInProgressRepository;
        private readonly IAddressInputRepository _addressInputRepository;
        private readonly INodeClient _nodeClient;
        private readonly int _minConfirmations;

        public IotaService(ILog log,
            IBroadcastRepository broadcastRepository,
            IBroadcastInProgressRepository broadcastInProgressRepository,
            IAddressInputRepository addressInputRepository,
            IBalanceRepository balanceRepository,
            INodeClient nodeClient,
            int minConfirmations)
        {
            _log = log;
            _broadcastRepository = broadcastRepository;
            _broadcastInProgressRepository = broadcastInProgressRepository;
            _addressInputRepository = addressInputRepository;
            _nodeClient = nodeClient;
            _minConfirmations = minConfirmations;
        }

        public bool ValidateAddress(string address)
        {
            if (address.StartsWith(Consts.VirtualAddressPrefix))
            {
                return true;
            }

            try
            {
                var iotaAddress = new Address(address);

                return true;
            }
            catch { }

            return false;
        }

        public async Task<AddressInput[]> GetVirtualAddressInputs(string virtualAddress)
        {
            var list = new List<AddressInput>();
            var addressInputs = await _addressInputRepository.GetAsync(virtualAddress);

            foreach (var addressInput in addressInputs)
            {
                list.Add(new AddressInput
                {
                    Address = addressInput.Address,
                    Index = addressInput.Index,
                    Balance = await _nodeClient.GetAddressBalance(addressInput.Address)
                });
            }

            return list.ToArray();
        }

        public async Task<long> GetVirtualAddressBalance(string address)
        {
            var inputs = await GetVirtualAddressInputs(address);

            return inputs.Sum(f => f.Balance);
        }

        public bool ValidateSignedTransaction(string transactionHex)
        {
            return true;
        }

        public async Task BroadcastAsync(string signedTransaction, Guid operationId)
        {
            var context = JsonConvert.DeserializeObject<SignedTransactionContext>(signedTransaction);

            await _nodeClient.Broadcast(context.Transactions);
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

        public class SignedTransactionContext
        {
            public string Hash { get; set; }
            public string[] Transactions { get; set; }
        }
    }
}
