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
using Tangle.Net.Repository.Responses;
using Lykke.Service.Iota.Api.Services.Helpers;
using Flurl.Http;
using Lykke.Service.Iota.Api.Core.Domain.Address;
using Lykke.Common.Log;

namespace Lykke.Service.Iota.Api.Services
{
    public class NodeClient : INodeClient
    {
        private const string PromoteErrorOldTransaction = "transaction is too old";
        private const string PromoteErrorConsistency = "entry point failed consistency check";
        private const string NodeHeaderName = "X-IOTA-API-Version";
        private const string NodeHeaderValue = "1.5";
        private const int NodeTimeout = 30;
        private const int NodeDepth = 8;
        private const int NodeMinWeightMagnitude = 14;

        private readonly ILog _log;
        private readonly RestIotaRepository _repository;
        private readonly string _nodeUrl;

        public NodeClient(ILogFactory logFactory, string nodeUrl)
        {
            var restClient = new RestClient(nodeUrl)
            {
                Timeout = 30000
            };

            _log = logFactory.CreateLog(this);
            _repository = new RestIotaRepository(restClient, new PoWService(new CpuPearlDiver()));
            _nodeUrl = nodeUrl;
        }

        public async Task<object> GetNodeInfo()
        {
            return await Run(() => _repository.GetNodeInfoAsync());
        }

        public async Task<bool> HasPendingTransaction(string address, bool cashOutTxsOnly = false)
        {
            var txsHashes = await Run(() => _repository.FindTransactionsByAddressesAsync(new List<Address> { new Address(address) }));
            var txs = await GetTransactions(txsHashes.Hashes);

            var nonZeroTxs = txs.Where(f => f.Value < 0).ToList();
            if (!cashOutTxsOnly)
            {
                nonZeroTxs.AddRange(txs.Where(f => f.Value > 0).ToList());
            }

            var bundleHashes = nonZeroTxs
                .OrderBy(f => f.AttachmentTimestamp)
                .Select(f => f.BundleHash.Value)
                .Distinct();

            foreach (var bundleHash in bundleHashes)
            {
                var bundleTxsHashes = await Run(() => _repository.FindTransactionsByBundlesAsync(new List<Hash> { new Hash(bundleHash) }));
                if (bundleTxsHashes != null && bundleTxsHashes.Hashes != null)
                {
                    if (!(await HasIncludedTransactions(bundleTxsHashes.Hashes)))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public async Task<(string From, string[] To)> GetBundleAddresses(string hash)
        {
            var bundleTxsHashes = await Run(() => _repository.FindTransactionsByBundlesAsync(new List<Hash> { new Hash(hash) }));
            if (bundleTxsHashes != null && bundleTxsHashes.Hashes != null)
            {
                var bundleTxs = await GetTransactions(bundleTxsHashes.Hashes);
                var bundleFirstAttachmentTxs = bundleTxs
                    .GroupBy(f => f.CurrentIndex)
                    .Select(f => f.OrderBy(x => x.AttachmentTimestamp).First())
                    .OrderBy(f => f.CurrentIndex);
                var fromTx = bundleFirstAttachmentTxs
                    .Where(f => f.Value < 0)
                    .FirstOrDefault();
                var toTxs = bundleFirstAttachmentTxs
                    .Where(f => f.Value > 0);

                return (fromTx?.Address.Value, toTxs.Select(f => f.Address.Value).ToArray());
            }

            return (null, null);
        }

        public async Task<RealAddressTransaction[]> GetFromAddressTransactions(string address)
        {
            try
            {
                var addressTransactions = new List<RealAddressTransaction>();

                var txsHashes = await Run(() => _repository.FindTransactionsByAddressesAsync(new List<Address> { new Address(address) }));
                var txs = await GetTransactions(txsHashes.Hashes);

                var fromAddressTxs = txs
                    .Where(f => f.Value < 0)
                    .OrderBy(f => f.AttachmentTimestamp)
                    .ToList();
                var bundleHashes = fromAddressTxs
                    .Select(f => f.BundleHash.Value)
                    .Distinct();

                foreach (var bundleHash in bundleHashes)
                {
                    var bundleTxsHashes = await Run(() => _repository.FindTransactionsByBundlesAsync(new List<Hash> { new Hash(bundleHash) }));
                    if (bundleTxsHashes != null && bundleTxsHashes.Hashes != null)
                    {
                        var bundleTxs = await GetTransactions(bundleTxsHashes.Hashes);
                        if (await BundleTxsIncluded(bundleTxs))
                        {
                            var bundleFirstAttachmentTxs = bundleTxs
                                .GroupBy(f => f.CurrentIndex)
                                .Select(f => f.OrderBy(x => x.AttachmentTimestamp).First())
                                .OrderBy(f => f.CurrentIndex);
                            var bundleFirstAttachmentTailTx = bundleFirstAttachmentTxs
                                .Where(f => f.IsTail)
                                .FirstOrDefault();

                            if (bundleFirstAttachmentTailTx != null)
                            {
                                var bundleFirstAttachmentFromTxs = bundleFirstAttachmentTxs.Where(f => f.Value > 0);

                                foreach (var bundleFirstAttachmentFromTx in bundleFirstAttachmentFromTxs)
                                {
                                    addressTransactions.Add(new RealAddressTransaction
                                    {
                                        Hash = bundleFirstAttachmentTailTx.BundleHash.Value,
                                        FromAddress = address,
                                        ToAddress = bundleFirstAttachmentFromTx.Address.ValueWithChecksum(),
                                        Amount = bundleFirstAttachmentFromTx.Value,
                                        Timestamp = bundleFirstAttachmentFromTx.AttachmentDateTimeUtc()
                                    });
                                }
                            }
                        }
                    }
                }

                return addressTransactions.ToArray();
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to get transactions for address={address}", ex);
            }
        }

        public async Task<RealAddressTransaction[]> GetToAddressTransactions(string address)
        {
            try
            {
                var addressTransactions = new List<RealAddressTransaction>();
                var addressObj = new Address(address);

                var txsHashes = await Run(() => _repository.FindTransactionsByAddressesAsync(new List<Address> { addressObj }));
                var txs = await GetTransactions(txsHashes.Hashes);

                var toAddressTxs = txs
                    .Where(f => f.Value > 0)
                    .OrderBy(f => f.AttachmentTimestamp)
                    .ToList();
                var bundleHashes = toAddressTxs
                    .Select(f => f.BundleHash.Value)
                    .Distinct();

                foreach (var bundleHash in bundleHashes)
                {
                    var bundleTxsHashes = await Run(() => _repository.FindTransactionsByBundlesAsync(new List<Hash> { new Hash(bundleHash) }));
                    if (bundleTxsHashes != null && bundleTxsHashes.Hashes != null)
                    {
                        var bundleTxs = await GetTransactions(bundleTxsHashes.Hashes);
                        if (await BundleTxsIncluded(bundleTxs))
                        {
                            var bundleFirstAttachmentTxs = bundleTxs
                                .GroupBy(f => f.CurrentIndex)
                                .Select(f => f.OrderBy(x => x.AttachmentTimestamp).First())
                                .OrderBy(f => f.CurrentIndex);
                            var bundleFirstAttachmentTailTx = bundleFirstAttachmentTxs
                                .Where(f => f.IsTail)
                                .FirstOrDefault();
                            var bundleFirstAttachmentFromTx = bundleFirstAttachmentTxs
                                .Where(f => f.Value < 0)
                                .FirstOrDefault();

                            if (bundleFirstAttachmentTailTx != null && bundleFirstAttachmentFromTx != null)
                            {
                                var toAddressBundleTxs = bundleFirstAttachmentTxs
                                    .Where(f => f.Value > 0 && f.Address.Value == addressObj.Value);

                                foreach (var toAddressBundleTx in toAddressBundleTxs)
                                {
                                    addressTransactions.Add(new RealAddressTransaction
                                    {
                                        Hash = bundleFirstAttachmentTailTx.BundleHash.Value,
                                        FromAddress = bundleFirstAttachmentFromTx.Address.ValueWithChecksum(),
                                        ToAddress = address,
                                        Amount = toAddressBundleTx.Value,
                                        Timestamp = toAddressBundleTx.AttachmentDateTimeUtc()
                                    });
                                }
                            }
                        }
                    }
                }

                return addressTransactions.ToArray();
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to get transactions for address={address}", ex);
            }
        }

        private async Task<bool> BundleTxsIncluded(List<Transaction> txs)
        {
            var txsTail = txs
                .Where(f => f.IsTail)
                .OrderBy(f => f.AttachmentTimestamp);
            var txsTailHashes = txsTail
                .Select(f => f.Hash)
                .ToList();

            return await HasIncludedTransactions(txsTailHashes);
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

        public async Task<Dictionary<string, bool>> HasCashOutTransaction(string[] addresses)
        {
            var addressHashes = addresses.Select(f => new Address()).ToList();
            var response = await Run(() => _repository.WereAddressesSpentFromAsync(addressHashes));

            return response.ToDictionary(f => f.Value, x => x.SpentFrom);
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

        private async Task<bool> HasIncludedTransactions(List<Hash> hashes)
        {
            var result = await Run(() => _repository.GetLatestInclusionAsync(hashes));
            if (result != null)
            {
                return result.States.Any(f => f.Value);
            }

            return false;
        }

        public async Task<(bool Included, long Value, string Address, long Block, string[] Txs)> GetBundleInfo(string hash)
        {
            var txsHashes = await Run(() => _repository.FindTransactionsByBundlesAsync(new List<Hash> { new Hash(hash) }));
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

        public async Task<(string Hash, long? Block, string Error)> Broadcast(string[] trytes)
        {
            _log.Info("Get txs from trytes");
            var transactions = trytes.Select(f => Transaction.FromTrytes(new TransactionTrytes(f)));

            _log.Info("Get transactions to approve");
            var txsToApprove = await GetTransactionsToApprove(NodeDepth);

            _log.Info("Attach to tangle");
            var attachResultTrytes = await _repository.AttachToTangleAsync(
                new Hash(txsToApprove.BranchTransaction),
                new Hash(txsToApprove.TrunkTransaction),
                transactions,
                NodeMinWeightMagnitude);

            _log.Info("Validate transactions");
            var error = await ValidateTransactions(transactions);
            if (!string.IsNullOrEmpty(error))
            {
                return (null, null, error);
            }

            _log.Info("Broadcast transactions");
            await BroadcastTransactionsAsync(attachResultTrytes);

            _log.Info("Store transactions");
            await StoreTransactionsAsync(attachResultTrytes);

            var txsBroadcasted = attachResultTrytes
                .Select(f => Transaction.FromTrytes(f))
                .ToList();
            var tailTx = txsBroadcasted
                .Where(f => f.IsTail)
                .First();

            _log.Info("Tailed tx", tailTx);

            return (tailTx.BundleHash.Value, tailTx.Timestamp, null);
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
            var bundle = await _repository.GetBundleAsync(new Hash(tailTxHash));
            var txsToApprove = await GetTransactionsToApprove(NodeDepth);

            var attachResultTrytes = await _repository.AttachToTangleAsync(
                new Hash(txsToApprove.BranchTransaction),
                new Hash(txsToApprove.TrunkTransaction),
                bundle.Transactions,
                NodeMinWeightMagnitude);

            await BroadcastTransactionsAsync(attachResultTrytes);
            await StoreTransactionsAsync(attachResultTrytes);

            var txsBroadcasted = attachResultTrytes.Select(f => Transaction.FromTrytes(f)).ToList();
            var tailTx = txsBroadcasted.Where(f => f.IsTail).First();

            return (tailTx.Hash.Value, tailTx.Timestamp);
        }

        public async Task Promote(string[] txs, int attempts = 3, int depth = 15)
        {
            var tx = "";
            var hashes = txs
                .Reverse()
                .Select(f => new Hash(f))
                .ToList();

            _log.Info("Promote txs", new { attempts, depth, txsNumber = txs.Length });

            foreach (var hash in hashes)
            {
                tx = hash.Value;

                var result = await PromoteTx(tx, attempts, depth);

                _log.Info("Promotion result", new { result.successAttempts, result.error, tx });

                if (result.successAttempts > 0 || result.error == PromoteErrorOldTransaction)
                {
                    return;
                }
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

                    var txsToApprove = await GetTransactionsToApprove(depth, tx);

                    var attachResultTrytes = await _repository.AttachToTangleAsync(
                        new Hash(txsToApprove.BranchTransaction),
                        new Hash(txsToApprove.TrunkTransaction),
                        bundle.Transactions);

                    await BroadcastTransactionsAsync(attachResultTrytes);

                    successAttempts++;
                }
                catch (Exception ex)
                {
                    error = ex.Message;

                    if (error.ToLower().Contains(PromoteErrorOldTransaction))
                    {
                        error = PromoteErrorOldTransaction;
                        break;
                    }
                    if (error.ToLower().Contains(PromoteErrorConsistency))
                    {
                        error = PromoteErrorConsistency;
                        break;
                    }
                }
            }

            return (successAttempts, error);
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
                return true;
            }

            return await Retry.Try(action, NeedToRetryException, tryCount, _log, 1000);
        }

        private async Task<GetTransactionsToApproveResponse> GetTransactionsToApprove(int depth)
        {
            var data = new
            {
                command = CommandType.GetTransactionsToApprove,
                depth
            };

            return await _nodeUrl
                .WithHeader(NodeHeaderName, NodeHeaderValue)
                .WithTimeout(NodeTimeout)
                .PostJsonAsync(data)
                .ReceiveJson<GetTransactionsToApproveResponse>();
        }

        private async Task<GetTransactionsToApproveResponse> GetTransactionsToApprove(int depth, string reference)
        {
            var data = new
            {
                command = CommandType.GetTransactionsToApprove,
                depth,
                reference
            };

            return await _nodeUrl
                .WithHeader(NodeHeaderName, NodeHeaderValue)
                .WithTimeout(NodeTimeout)
                .PostJsonAsync(data)
                .ReceiveJson<GetTransactionsToApproveResponse>();
        }

        private async Task BroadcastTransactionsAsync(List<TransactionTrytes> transactions)
        {
            var data = new
            {
                command = CommandType.BroadcastTransactions,
                trytes = transactions.Select(t => t.Value).ToList()
            };

            var result = await _nodeUrl
                .WithHeader(NodeHeaderName, NodeHeaderValue)
                .WithTimeout(NodeTimeout)
                .PostJsonAsync(data);
        }

        private async Task StoreTransactionsAsync(List<TransactionTrytes> transactions)
        {
            var data = new
            {
                command = CommandType.StoreTransactions,
                trytes = transactions.Select(t => t.Value).ToList()
            };

            var result = await _nodeUrl
                .WithHeader(NodeHeaderName, NodeHeaderValue)
                .WithTimeout(NodeTimeout)
                .PostJsonAsync(data);
        }
    }
}
