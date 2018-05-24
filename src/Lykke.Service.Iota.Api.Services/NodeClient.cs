using Common.Log;
using Lykke.Service.Iota.Api.Core.Services;
using RestSharp;
using System.Collections.Generic;
using System.Threading.Tasks;
using Tangle.Net.Entity;
using Tangle.Net.Repository;

namespace Lykke.Service.Iota.Api.Services
{
    public class NodeClient : INodeClient
    {
        private readonly ILog _log;
        private readonly RestIotaRepository _repository;

        public NodeClient(ILog log, string nodeUrl)
        {
            _log = log;

            _repository = new RestIotaRepository(new RestClient(nodeUrl));
        }

        public async Task<long> GetAddressBalance(string address)
        {
            var response = await _repository.GetBalancesAsync(new List<Address>
            {
                new Address(address)
            });

            return response.Addresses[0].Balance;
        }
    }
}
