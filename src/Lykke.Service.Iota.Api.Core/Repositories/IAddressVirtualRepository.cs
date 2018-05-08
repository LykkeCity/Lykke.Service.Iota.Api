using System.Threading.Tasks;
using Lykke.Service.Iota.Api.Core.Domain.Address;

namespace Lykke.Service.Iota.Api.Core.Repositories
{
    public interface IAddressVirtualRepository
    {
        Task SaveAsync(string addressVirtual, long latestAddressIndex);
        Task DeleteAsync(string addressVirtual);
        Task<IAddressVirtual> GetAsync(string addressVirtual);
    }
}
