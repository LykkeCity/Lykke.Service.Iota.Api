﻿using Common.Log;
using Lykke.Service.Iota.Api.Core.Domain;
using Lykke.Service.Iota.Api.Core.Repositories;
using Lykke.Service.Iota.Api.Core.Services;
using Lykke.Service.Iota.Api.Core.Shared;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Tangle.Net.Entity;

namespace Lykke.Service.Iota.Api.Services
{
    public class IotaService : IIotaService
    {
        private readonly ILog _log;
        private readonly IAddressInputRepository _addressInputRepository;
        private readonly IBuildRepository _buildRepository;
        private readonly INodeClient _nodeClient;
        private readonly int _minConfirmations;

        public IotaService(ILog log,
            IAddressInputRepository addressInputRepository,
            IBuildRepository buildRepository,
            IBalanceRepository balanceRepository,
            INodeClient nodeClient,
            int minConfirmations)
        {
            _log = log;
            _addressInputRepository = addressInputRepository;
            _buildRepository = buildRepository;
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
                    Balance = await _nodeClient.GetAddressBalance(addressInput.Address, _minConfirmations)
                });
            }

            return list.ToArray();
        }

        public async Task<long> GetVirtualAddressBalance(string address)
        {
            var inputs = await GetVirtualAddressInputs(address);

            return inputs.Sum(f => f.Balance);
        }
    }
}
