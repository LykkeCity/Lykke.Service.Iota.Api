using System.Threading.Tasks;

namespace Lykke.Service.Iota.Api.Core.Repositories
{
    public interface IAddressVirtualRepository
    {
        Task DeleteAsync(string address);
        Task<string> GetVirtualAddressAsync(string address);
        Task SaveAsync(string address, string virtualAddress);
    }
}
