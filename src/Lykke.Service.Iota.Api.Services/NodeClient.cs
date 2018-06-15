using Common.Log;
using Lykke.Service.Iota.Api.Core.Services;
using RestSharp;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Tangle.Net.Entity;
using Tangle.Net.Repository;
using System;
using Tangle.Net.ProofOfWork;

namespace Lykke.Service.Iota.Api.Services
{
    public class NodeClient : INodeClient
    {
        private readonly ILog _log;
        private readonly RestIotaRepository _repository;

        public NodeClient(ILog log, string nodeUrl)
        {
            _log = log;

            _repository = new RestIotaRepository(new RestClient(nodeUrl), new PoWService(new CpuPearlDiver()));
        }

        public async Task<long> GetAddressBalance(string address)
        {
            var response = await _repository.GetBalancesAsync(new List<Address>
            {
                new Address(address)
            });

            return response.Addresses[0].Balance;
        }

        public async Task<string> Broadcast(string[] trytes)
        {
            var depth = 8;
            var minWeightMagnitude = 14;

            var txs = trytes.Select(f => Transaction.FromTrytes(new TransactionTrytes(f)));

            var transactionsToApprove = await _repository.GetTransactionsToApproveAsync(depth);

            var attachResultTrytes = await _repository.AttachToTangleAsync(
                                       transactionsToApprove.BranchTransaction,
                                       transactionsToApprove.TrunkTransaction,
                                       txs,
                                       minWeightMagnitude);

            await _repository.BroadcastAndStoreTransactionsAsync(attachResultTrytes);

            return transactionsToApprove.BranchTransaction.Value;
        }
    }
}
