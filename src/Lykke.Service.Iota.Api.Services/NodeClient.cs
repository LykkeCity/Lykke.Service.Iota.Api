using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Common.Log;
using RestSharp;
using Lykke.Service.Iota.Api.Core.Services;
using Tangle.Net.Entity;
using Tangle.Net.Repository;
using Tangle.Net.ProofOfWork;
using Tangle.Net.Repository.DataTransfer;
using Tangle.Net.Repository.Client;
using Tangle.Net.Repository.Responses;
using Common;

namespace Lykke.Service.Iota.Api.Services
{
    public class NodeClient : INodeClient
    {
        private readonly ILog _log;
        private readonly RestIotaRepository _repository;
        private readonly RestIotaClient _client;

        public NodeClient(ILog log, string nodeUrl)
        {
            var restClient = new RestClient(nodeUrl);

            _log = log;
            _repository = new RestIotaRepository(restClient, new PoWService(new CpuPearlDiver()));
            _client = new RestIotaClient(restClient);
        }

        public async Task<long> GetAddressBalance(string address, int threshold)
        {
            var response = await _repository.GetBalancesAsync(
                new List<Address> { new Address(address) }, 
                threshold);

            return response.Addresses[0].Balance;
        }

        public async Task<string[]> GetBundleAddresses(string tailTxHash)
        {
            var bundle = await _repository.GetBundleAsync(new Hash(tailTxHash));

            return bundle.Transactions
                .Where(f => f.Value != 0)
                .Select(f => f.Address.Value)
                .Distinct()
                .ToArray();
        }

        public async Task<bool> TransactionIncluded(string tailTxHash)
        {
            var hashObj = new Hash(tailTxHash);

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

        public async Task<(long Value, long Block)> GetTransactionInfo(string hash)
        {
            var hashObj = new Hash(hash);

            var result = await _repository.GetTrytesAsync(new List<Hash> { hashObj });
            var tx = Transaction.FromTrytes(result.First());

            return (tx.Value, tx.AttachmentTimestamp);
        }

        public async Task<ConsistencyInfo> CheckConsistency(string hash)
        {
            return await _repository.CheckConsistencyAsync(new List<Hash> { new Hash(hash) });
        }

        public async Task<(string Hash, long Block)> Broadcast(string[] trytes)
        {
            _log.WriteInfo(nameof(Broadcast), "", "Get txs from trytes");
            var txs = trytes.Select(f => Transaction.FromTrytes(new TransactionTrytes(f)));

            _log.WriteInfo(nameof(Broadcast), "", "Send txs");
            var txsTrities = await _repository.SendTrytesAsync(txs);

            _log.WriteInfo(nameof(Broadcast), "", "Get broadcated txs");
            var txsBroadcasted = txsTrities.Select(f => Transaction.FromTrytes(f)).ToList();

            _log.WriteInfo(nameof(Broadcast), "", "Get tailed tx");
            var tailTx = txsBroadcasted.Where(f => f.IsTail).First();

            _log.WriteInfo(nameof(Broadcast), tailTx.ToJson(), "Tailed tx");

            return (tailTx.Hash.Value, tailTx.Timestamp);
        }

        public async Task<(string Hash, long Block)> Reattach(string tailTxHash)
        {
            var txsTrities = await _repository.ReplayBundleAsync(new Hash(tailTxHash));
            var txsBroadcasted = txsTrities.Select(f => Transaction.FromTrytes(f)).ToList();
            var tailTx = txsBroadcasted.Where(f => f.IsTail).First();

            return (tailTx.Hash.Value, tailTx.Timestamp);
        }

        public async Task Promote(string tailTxHash, int attempts = 10, int depth = 27)
        {
            for (var i = 0; i < attempts; i++)
            {
                if (await TransactionIncluded(tailTxHash))
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
                    { "depth", depth },
                    { "reference", tailTxHash }
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
