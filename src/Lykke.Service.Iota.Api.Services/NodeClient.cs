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
using Tangle.Net.Repository.Client;
using Tangle.Net.Repository.Responses;
using Common;
using Lykke.Service.Iota.Api.Services.Helpers;
using Flurl.Http;

namespace Lykke.Service.Iota.Api.Services
{
    public class NodeClient : INodeClient
    {
        private const string PromoteError_OldTransaction = "transaction is too old";
        private const string PromoteError_Consistency = "entry point failed consistency check";

        private readonly ILog _log;
        private readonly RestIotaRepository _repository;
        private readonly string _nodeUrl;

        public NodeClient(ILog log, string nodeUrl)
        {
            var restClient = new RestClient(nodeUrl);

            _log = log.CreateComponentScope(nameof(NodeClient));
            _repository = new RestIotaRepository(restClient, new PoWService(new CpuPearlDiver()));
            _nodeUrl = nodeUrl;
        }

        public async Task<string> GetNodeInfo()
        {
            var response = await Run(() => _repository.GetNodeInfoAsync());

            return response.ToJson();
        }

        public async Task<bool> HasPendingTransaction(string address, bool cashOutTxsOnly = false)
        {
            var txsHashes = await Run(() => _repository.FindTransactionsByAddressesAsync(new List<Address> { new Address(address) }));
            var txs = await GetTransactions(txsHashes.Hashes);

            var nonZeroTx = txs.Where(f => f.Value < 0).ToList();
            if (!cashOutTxsOnly)
            {
                nonZeroTx.AddRange(txs.Where(f => f.Value > 0).ToList());
            }

            var nonZeroTxHashes = txs.OrderByDescending(f => f.AttachmentTimestamp)
                .Select(f => f.Hash.Value)
                .Distinct();

            foreach (var nonZeroTxHash in nonZeroTxHashes)
            {
                var bundleInfo = await GetBundleInfo(nonZeroTxHash);
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

        public async Task<bool> HasCashOutTransaction(string address)
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
                if (result.States.TryGetValue(hashObj, out var value))
                {
                    return value;
                }
            }

            return false;
        }

        public async Task<(bool Included, long Value, string Address, long Block, string[] Txs)> GetBundleInfo(string hash)
        {
            var tx = await GetTransaction(hash);
            var txsHashes = await Run(() => _repository.FindTransactionsByBundlesAsync(new List<Hash> { tx.BundleHash }));
            var txs = await GetTransactions(txsHashes.Hashes);
            var txsTail = txs
                .Where(f => f.IsTail)
                .OrderBy(f => f.AttachmentTimestamp);
            var txsTailHashes = txsTail
                .Select(f => f.Hash.Value)
                .ToArray();

            foreach (var txTail in txsTail)
            {
                if (await TransactionIncluded(txTail.Hash.Value))
                {
                    return (true, txTail.Value, txTail.Address.Value, txTail.AttachmentTimestamp, txsTailHashes);
                }
            }

            var txFirst = txsTail.First();
            var txLast = txsTail.Last();

            return (false, txFirst.Value, txFirst.Address.Value, txLast.AttachmentTimestamp, txsTailHashes);
        }

        public async Task<(long Value, long Block)> GetTransactionInfo(string hash)
        {
            var tx = await GetTransaction(hash);

            return (tx.Value, tx.AttachmentTimestamp);
        }

        public string[] GetTransactionNonZeroAddresses(string[] trytes)
        {
            var txs = trytes.Select(f => Transaction.FromTrytes(new TransactionTrytes(f)));

            return txs
                .Where(f => f.Value != 0)
                .Select(f => f.Hash.Value)
                .ToArray();
        }

        public async Task<(string Hash, long? Block, string Error)> Broadcast(string[] trytes)
        {
            var depth = 8;
            var minWeightMagnitude = 14;

            _log.WriteInfo(nameof(Broadcast), "", "Get txs from trytes");
            var transactions = trytes.Select(f => Transaction.FromTrytes(new TransactionTrytes(f)));

            _log.WriteInfo(nameof(Broadcast), "", "Get transactions to approve");
            var transactionsToApprove = await _repository.GetTransactionsToApproveAsync(depth);

            _log.WriteInfo(nameof(Broadcast), "", "Attach to tangle");
            var attachResultTrytes = await _repository.AttachToTangleAsync(
                transactionsToApprove.BranchTransaction,
                transactionsToApprove.TrunkTransaction,
                transactions,
                minWeightMagnitude);

            var error = await ValidateTransactions(transactions);
            if (!string.IsNullOrEmpty(error))
            {
                return (null, null, error);
            }

            _log.WriteInfo(nameof(Broadcast), "", "Broadcast and store transactions");
            await _repository.BroadcastAndStoreTransactionsAsync(attachResultTrytes);

            _log.WriteInfo(nameof(Broadcast), "", "Get broadcated txs");
            var txsBroadcasted = attachResultTrytes.Select(f => Transaction.FromTrytes(f)).ToList();

            _log.WriteInfo(nameof(Broadcast), "", "Get tailed tx");
            var tailTx = txsBroadcasted.Where(f => f.IsTail).First();

            _log.WriteInfo(nameof(Broadcast), tailTx.ToJson(), "Tailed tx");

            return (tailTx.Hash.Value, tailTx.Timestamp, null);
        }

        private async Task<string> ValidateTransactions(IEnumerable<Transaction> transactions)
        {
            foreach (var transaction in transactions)
            {
                var address = transaction.Address.Value;

                var addressHasCashOut = await HasCashOutTransaction(address);
                if (addressHasCashOut)
                {
                    return $"Address {address} has completed cash-out transaction";
                }

                if (transaction.Value < 0)
                {
                    var balance = await GetAddressBalance(address, 1);
                    if (balance != -transaction.Value)
                    {
                        return $"Input address {address} has wrong amount. " +
                            $"Current amount:{balance} Transaction amount: {transaction.Value}. " +
                            $"These values must be equal";
                    }

                    var hasPendingTx = await HasPendingTransaction(address);
                    if (hasPendingTx)
                    {
                        return $"Input address {address} has pending transaction";
                    }
                }

                if (transaction.Value > 0)
                {
                    var hasPendingTx = await HasPendingTransaction(address, true);
                    if (hasPendingTx)
                    {
                        return $"Output address {address} has pending cash-out transaction";
                    }
                }
            }

            return null;
        }

        public async Task<(string Hash, long Block)> Reattach(string tailTxHash)
        {
            var txsTrities = await Run(() => _repository.ReplayBundleAsync(new Hash(tailTxHash)));
            var txsBroadcasted = txsTrities.Select(f => Transaction.FromTrytes(f)).ToList();
            var tailTx = txsBroadcasted.Where(f => f.IsTail).First();

            return (tailTx.Hash.Value, tailTx.Timestamp);
        }

        public async Task Promote(string[] txs, int attempts = 3, int depth = 15)
        {
            var hashes = txs.Select(f => new Hash(f)).ToList();
            var tx = "";

            _log.WriteInfo(nameof(Promote),
                new { attempts, depth, txsNumber = txs.Length, txs },
                "Promote txs");

            foreach (var hash in hashes)
            {
                var info = await Run(() => _repository.CheckConsistencyAsync(new List<Hash> { hash }));
                if (info.State)
                {
                    tx = hash.Value;

                    var result = await PromoteTx(tx, attempts, depth);

                    _log.WriteInfo(nameof(Promote),
                        new { result.successAttempts, result.error, tx},
                        "Promotion result");

                    if (result.successAttempts > 0)
                    {
                        return;
                    }
                }
            }

            if (string.IsNullOrEmpty(tx))
            {
                _log.WriteInfo(nameof(Promote),
                    new { depth, error = "there are no consistent tx", txs },
                    "Promotion results");
            }
        }

        private async Task<(int successAttempts, string error)> PromoteTx(string tx, int attempts, int depth)
        {
            var error = "";
            var successAttempts = 0;

            for (var i = 0; i < attempts; i++)
            {
                try
                {
                    await PromoteTx(tx, depth);

                    successAttempts++;
                }
                catch (Exception ex)
                {
                    error = ex.Message;

                    if (error.ToLower().Contains(PromoteError_OldTransaction))
                    {
                        error = PromoteError_OldTransaction;
                        break;

                    }
                    if (error.ToLower().Contains(PromoteError_Consistency))
                    {
                        error = PromoteError_Consistency;
                        break;
                    }
                }
            }

            return (successAttempts, error);
        }

        private async Task PromoteTx(string tx, int depth)
        {
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

            var data = new
            {
                command = CommandType.GetTransactionsToApprove,
                depth,
                reference = tx
            };

            var result = await _nodeUrl
                .WithHeader("X-IOTA-API-Version", 1)
                .PostJsonAsync(data)
                .ReceiveJson<GetTransactionsToApproveResponse>();

            var attachResultTrytes = await Run(() => _repository.AttachToTangleAsync(
                new Hash(result.BranchTransaction),
                new Hash(result.TrunkTransaction),
                bundle.Transactions));

            await _repository.BroadcastAndStoreTransactionsAsync(attachResultTrytes);
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
