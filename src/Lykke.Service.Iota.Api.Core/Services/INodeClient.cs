using System.Threading.Tasks;

namespace Lykke.Service.Iota.Api.Core.Services
{
    public interface INodeClient
    {
        Task<long> GetAddressBalance(string address);
        Task Broadcast(string[] trytes);
        Task Promote(string hash, int attempts = 10);
        Task Reattach(string hash);
    }
}
