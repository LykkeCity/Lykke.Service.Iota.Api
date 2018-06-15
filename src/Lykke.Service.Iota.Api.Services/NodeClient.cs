using Common.Log;
using Lykke.Service.Iota.Api.Core.Services;
using RestSharp;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Tangle.Net.Entity;
using Tangle.Net.Repository;
using Tangle.Net.ProofOfWork;
using System;
using Tangle.Net.Repository.DataTransfer;
using Tangle.Net.Repository.Client;
using Tangle.Net.Repository.Responses;

namespace Lykke.Service.Iota.Api.Services
{
    public class NodeClient : INodeClient
    {
        private readonly ILog _log;
        private readonly RestIotaRepository _repository;
        private readonly RestIotaClient _client;

        public NodeClient(ILog log, string nodeUrl)
        {
            _log = log;

            _repository = new RestIotaRepository(new RestClient(nodeUrl), new PoWService(new CpuPearlDiver()));
            _client = new RestIotaClient(new RestClient(nodeUrl));
        }

        public async Task<long> GetAddressBalance(string address)
        {
            var response = await _repository.GetBalancesAsync(new List<Address>
            {
                new Address(address)
            });

            return response.Addresses[0].Balance;
        }

        public async Task<Bundle> GetBundle(string hash)
        {
            return await _repository.GetBundleAsync(new Hash(hash));
        }

        public async Task<bool> TransactionIncluded(string hash)
        {
            var hashObj = new Hash(hash);

            var result = await _repository.GetLatestInclusionAsync(new List<Hash> { hashObj });
            if (result != null)
            {
                if (result.States.Keys.Contains(hashObj))
                {
                    return result.States[hashObj];
                }
            }

            return false;
        }

        public async Task<ConsistencyInfo> CheckConsistency(string hash)
        {
            return await _repository.CheckConsistencyAsync(new List<Hash> { new Hash(hash) });
        }

        public async Task Broadcast(string[] trytes)
        {
            var txs = trytes.Select(f => Transaction.FromTrytes(new TransactionTrytes(f)));

            await _repository.SendTrytesAsync(txs);
        }

        public async Task Reattach(string hash)
        {
            await _repository.ReplayBundleAsync(new Hash(hash));
        }

        public async Task Promote(string hash, int attempts = 10)
        {
            for (var i = 0; i < attempts; i++)
            {
                if (await TransactionIncluded(hash))
                {
                    return;
                }

                var bundle = new Bundle();

                bundle.AddTransfer(new Transfer
                {
                    Address = new Address(new String('9', 81)),
                    Tag = Tag.Empty,
                    Message = new TryteString(""),
                    ValueToTransfer = 0
                });
                bundle.Finalize();
                bundle.Sign();

                var result = await _client.ExecuteParameterizedCommandAsync<GetTransactionsToApproveResponse>(new Dictionary<string, object>
                {
                    { "command", CommandType.GetTransactionsToApprove },
                    { "depth", 27 },
                    { "reference", hash }
                });

                var attachResultTrytes = await _repository.AttachToTangleAsync(
                    new Hash(result.BranchTransaction),
                    new Hash(result.TrunkTransaction),
                    bundle.Transactions);

                await _repository.BroadcastAndStoreTransactionsAsync(attachResultTrytes);
            }
        }
    }
}
