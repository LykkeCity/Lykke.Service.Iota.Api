using Lykke.Service.Iota.Api.Core.Shared;
using System.Threading.Tasks;

namespace Lykke.Service.Iota.Api.Services
{
    public interface IIotaService
    {
        bool ValidateAddress(string address);

        Task<long> GetVirtualAddressBalance(string address);

        Task<AddressInput[]> GetVirtualAddressInputs(string virtualAddress);
    }
}
