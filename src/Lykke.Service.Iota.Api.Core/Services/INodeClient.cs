using System.Threading.Tasks;

namespace Lykke.Service.Iota.Api.Core.Services
{
    public interface INodeClient
    {
        Task<long> GetAddressBalance(string address);
    }
}
