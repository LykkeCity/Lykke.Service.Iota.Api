using System.Threading.Tasks;
using Lykke.Service.Iota.Api.Core.Domain.Address;
using System.Collections.Generic;

namespace Lykke.Service.Iota.Api.Core.Repositories
{
    public interface IAddressInputRepository
    {
        Task<IEnumerable<IAddressInput>> GetAsync(string addressVirtual);
        Task<IAddressInput> GetAsync(string addressVirtual, string address);
        Task SaveAsync(string addressVirtual, string address, int index);
        Task DeleteAsync(string addressVirtual, string address);
    }
}
