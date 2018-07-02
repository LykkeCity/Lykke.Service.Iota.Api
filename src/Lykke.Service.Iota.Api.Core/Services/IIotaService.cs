using Lykke.Service.Iota.Api.Shared;
using System.Threading.Tasks;

namespace Lykke.Service.Iota.Api.Core.Services
{
    public interface IIotaService
    {
        Task<long> GetVirtualAddressBalance(string address);
        Task<AddressInput[]> GetVirtualAddressInputs(string virtualAddress);
        Task<string> GetRealAddress(string virtualAddress);
    }
}
