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
using Lykke.Service.Iota.Api.Services.Helpers;

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

            _log = log.CreateComponentScope(nameof(NodeClient));
            _repository = new RestIotaRepository(restClient, new PoWService(new CpuPearlDiver()));
            _client = new RestIotaClient(restClient);
        }

        public async Task<string> GetNodeInfo()
        {
            var response = await Run(() => _repository.GetNodeInfoAsync());

            return response.ToJson();
        }

        public async Task<bool> HasPendingTransaction(string address)
        {
            var txsHashes = await Run(() => _repository.FindTransactionsByAddressesAsync(new List<Address> { new Address(address) }));
            var txs = await GetTransactions(txsHashes.Hashes);
            var nonZeroTxs = txs
                .Where(f => f.Value != 0)
                .OrderByDescending(f => f.AttachmentTimestamp)
                .Select(f => f.Hash.Value)
                .Distinct();

            foreach (var nonZeroTx in nonZeroTxs)
            {
                var bundleInfo = await GetBundleInfo(nonZeroTx);
                if (!bundleInfo.Included)
                {
                    return true;
                }
            }

            return false;
        }

        public async Task<long> GetAddressBalance(string address, int threshold)
        {
            try
            {
                var response = await Run(() => _repository.GetBalancesAsync(new List<Address> { new Address(address) }, threshold));

                return response.Addresses[0].Balance;
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to get balance for address={address} and threshold={threshold}", ex);
            }
        }

        public async Task<bool> WereAddressesSpentFrom(string address)
        {
            var response = await Run(() => _repository.WereAddressesSpentFromAsync(new List<Address> { new Address(address) }));

            return response.First().SpentFrom;
        }

        public async Task<string[]> GetBundleAddresses(string tailTxHash)
        {
            var bundle = await Run(() => _repository.GetBundleAsync(new Hash(tailTxHash)));

            return bundle.Transactions
                .Where(f => f.Value != 0)
                .Select(f => f.Address.Value)
                .Distinct()
                .ToArray();
        }

        public async Task<bool> TransactionIncluded(string hash)
        {
            var hashObj = new Hash(hash);

            var result = await Run(() => _repository.GetLatestInclusionAsync(new List<Hash> { hashObj }));
            if (result != null)
            {
                if (result.States.Keys.Contains(hashObj))
                {
                    return result.States[hashObj];
                }
            }

            return false;
        }

        public async Task<(bool Included, string TxHash, string TxAddress, long TxValue, long TxBlock)> GetBundleInfo(string hash)
        {
            var tx = await GetTransaction(hash);
            var txsHashes = await Run(() => _repository.FindTransactionsByBundlesAsync(new List<Hash> { tx.BundleHash }));
            var txs = await GetTransactions(txsHashes.Hashes);
            var txsTail = txs.Where(f => f.IsTail).OrderByDescending(f => f.AttachmentTimestamp);
            var txLatest = txsTail.First();

            foreach (var txTail in txsTail)
            {
                if (await TransactionIncluded(txTail.Hash.Value))
                {
                    return (true, txTail.Hash.Value, txTail.Address.Value, txTail.Value, txTail.AttachmentTimestamp);
                }
            }

            return (false, txLatest.Hash.Value, txLatest.Address.Value, txLatest.Value, txLatest.AttachmentTimestamp);
        }

        public async Task<(long Value, long Block)> GetTransactionInfo(string hash)
        {
            var tx = await GetTransaction(hash);

            return (tx.Value, tx.AttachmentTimestamp);
        }

        public async Task<ConsistencyInfo> CheckConsistency(string hash)
        {
            return await Run(() => _repository.CheckConsistencyAsync(new List<Hash> { new Hash(hash) }));
        }

        public string[] GetTransactionNonZeroAddresses(string[] trytes)
        {
            var txs = trytes.Select(f => Transaction.FromTrytes(new TransactionTrytes(f)));

            return txs
                .Where(f => f.Value != 0)
                .Select(f => f.Hash.Value)
                .ToArray();
        }

        public async Task<(string Hash, long Block)> Broadcast(string[] trytes)
        {
            _log.WriteInfo(nameof(Broadcast), "", "Get txs from trytes");
            var txs = trytes.Select(f => Transaction.FromTrytes(new TransactionTrytes(f)));

            _log.WriteInfo(nameof(Broadcast), "", "Send txs");
            var txsTrities = await Run(() => _repository.SendTrytesAsync(txs));

            _log.WriteInfo(nameof(Broadcast), "", "Get broadcated txs");
            var txsBroadcasted = txsTrities.Select(f => Transaction.FromTrytes(f)).ToList();

            _log.WriteInfo(nameof(Broadcast), "", "Get tailed tx");
            var tailTx = txsBroadcasted.Where(f => f.IsTail).First();

            _log.WriteInfo(nameof(Broadcast), tailTx.ToJson(), "Tailed tx");

            return (tailTx.Hash.Value, tailTx.Timestamp);
        }

        public async Task<(string Hash, long Block)> Reattach(string tailTxHash)
        {
            var txsTrities = await Run(() => _repository.ReplayBundleAsync(new Hash(tailTxHash)));
            var txsBroadcasted = txsTrities.Select(f => Transaction.FromTrytes(f)).ToList();
            var tailTx = txsBroadcasted.Where(f => f.IsTail).First();

            return (tailTx.Hash.Value, tailTx.Timestamp);
        }

        public async Task Promote(string tailTxHash, int attempts = 10, int depth = 27)
        {
            var successAttempts = 0;
            var lastError = "";

            for (var i = 0; i < attempts; i++)
            {
                try
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

                    var result = await Run(() => _client.ExecuteParameterizedCommandAsync<GetTransactionsToApproveResponse>(new Dictionary<string, object>
                    {
                        { "command", CommandType.GetTransactionsToApprove },
                        { "depth", depth },
                        { "reference", tailTxHash }
                    }));

                    var attachResultTrytes = await Run(() => _repository.AttachToTangleAsync(
                        new Hash(result.BranchTransaction),
                        new Hash(result.TrunkTransaction),
                        bundle.Transactions));

                    await _repository.BroadcastAndStoreTransactionsAsync(attachResultTrytes);

                    successAttempts++;
                }
                catch (Exception ex)
                {
                    lastError = ex.Message;
                }
            }

            _log.WriteInfo(nameof(Promote), new { successAttempts, tailTxHash, lastError }, "Promotion results");
        }

        private async Task<Transaction> GetTransaction(string hash)
        {
            var txTrytes = await Run(() => _repository.GetTrytesAsync(new List<Hash> { new Hash(hash) }));

            return Transaction.FromTrytes(txTrytes.First());
        }

        private async Task<List<Transaction>> GetTransactions(List<Hash> hashes)
        {
            var txTrytes = await Run(() => _repository.GetTrytesAsync(hashes));

            return txTrytes.Select(f => Transaction.FromTrytes(f)).ToList();
        }

        private async Task<T> Run<T>(Func<Task<T>> action, int tryCount = 3)
        {
            bool NeedToRetryException(Exception ex)
            {
                if (ex is IotaApiException)
                {
                    return true;
                }

                return false;
            }

            return await Retry.Try(action, NeedToRetryException, tryCount, _log, 1000);
        }
    }
}
