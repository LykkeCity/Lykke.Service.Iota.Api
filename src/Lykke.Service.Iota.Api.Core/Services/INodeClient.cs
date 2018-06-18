using System.Threading.Tasks;

namespace Lykke.Service.Iota.Api.Core.Services
{
    public interface INodeClient
    {
        Task<long> GetAddressBalance(string address, int threshold);
        Task<(long Value, long Block)> GetTransactionInfo(string hash);
        Task<bool> TransactionIncluded(string tailTxHash);
        Task<string[]> GetBundleAddresses(string tailTxHash);
        Task<(string Hash, long Block)> Broadcast(string[] trytes);
        Task<(string Hash, long Block)> Reattach(string tailTxHash);
        Task Promote(string tailTxHash, int attempts = 10, int depth = 27);
    }
}
