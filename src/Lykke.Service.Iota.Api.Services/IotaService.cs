using Lykke.Service.Iota.Api.Core.Repositories;
using Lykke.Service.Iota.Api.Core.Services;
using Lykke.Service.Iota.Api.Shared;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Lykke.Service.Iota.Api.Services
{
    public class IotaService : IIotaService
    {
        private readonly IAddressInputRepository _addressInputRepository;
        private readonly IBuildRepository _buildRepository;
        private readonly INodeClient _nodeClient;
        private readonly int _minConfirmations;

        public IotaService(IAddressInputRepository addressInputRepository,
            IBuildRepository buildRepository,
            IBalanceRepository balanceRepository,
            INodeClient nodeClient,
            int minConfirmations)
        {
            _addressInputRepository = addressInputRepository;
            _buildRepository = buildRepository;
            _nodeClient = nodeClient;
            _minConfirmations = minConfirmations;
        }

        public async Task<string> GetRealAddress(string virtualAddress)
        {
            var addressInputs = await _addressInputRepository.GetAsync(virtualAddress);
            if (addressInputs == null || addressInputs.Count() == 0)
            {
                return null;
            }

            var latestIndex = addressInputs.Max(f => f.Index);
            var latestAddress = addressInputs.First(f => f.Index == latestIndex);

            return latestAddress.Address;
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

        public async Task<long> GetVirtualAddressBalance(string virtualAddress)
        {
            var inputs = await GetVirtualAddressInputs(virtualAddress);

            return inputs.Sum(f => f.Balance);
        }
    }
}
