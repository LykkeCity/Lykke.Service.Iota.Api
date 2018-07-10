﻿using Lykke.Service.Iota.Api.Core.Domain.Address;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Lykke.Service.Iota.Api.Core.Services
{
    public interface INodeClient
    {
        Task<object> GetNodeInfo();
        Task<(string From, string[] To)> GetBundleAddresses(string hash);
        Task<RealAddressTransaction[]> GetFromAddressTransactions(string address);
        Task<RealAddressTransaction[]> GetToAddressTransactions(string address);
        Task<long> GetAddressBalance(string address, int threshold);
        Task<bool> HasCashOutTransaction(string address);
        Task<Dictionary<string, bool>> HasCashOutTransaction(string[] addresses);
        Task<bool> HasPendingTransaction(string address, bool cashOutTxsOnly = false);
        Task<bool> TransactionIncluded(string tailTxHash);
        Task<(bool Included, long Value, string Address, long Block, string[] Txs)> GetBundleInfo(string hash);
        Task<(string Hash, long? Block, string Error)> Broadcast(string[] trytes);
        Task<(string Hash, long Block)> Reattach(string tailTxHash);
        Task Promote(string[] txs, int attempts = 3, int depth = 15);
    }
}
