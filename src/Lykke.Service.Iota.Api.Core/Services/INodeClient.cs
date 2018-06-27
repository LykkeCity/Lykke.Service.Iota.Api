using System.Threading.Tasks;

namespace Lykke.Service.Iota.Api.Core.Services
{
    public interface INodeClient
    {
        Task<string> GetNodeInfo();
        Task<long> GetAddressBalance(string address, int threshold);
        Task<bool> HasCashOutTransaction(string address);
        Task<bool> HasPendingTransaction(string address, bool cashOutTxsOnly = false);
        Task<bool> TransactionIncluded(string tailTxHash);
        Task<(long Value, long Block)> GetTransactionInfo(string hash);
        Task<(bool Included, long Value, string Address, long Block, string[] Txs)> GetBundleInfo(string hash);
        Task<string[]> GetBundleAddresses(string tailTxHash);
        Task<(string Hash, long? Block, string Error)> Broadcast(string[] trytes);
        Task<(string Hash, long Block)> Reattach(string tailTxHash);
        Task Promote(string[] txs, int attempts = 3, int depth = 15);
        string[] GetTransactionNonZeroAddresses(string[] trytes);
    }
}
