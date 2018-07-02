using System.Collections.Generic;
using System.Threading.Tasks;
using Lykke.Service.Iota.Api.Core.Domain.Address;
using System;

namespace Lykke.Service.Iota.Api.Core.Repositories
{
    public interface IAddressTransactionRepository
    {
        Task DeleteAsync(string addressVirtual, string hash);
        Task<(IEnumerable<IAddressTransaction> Entities, string ContinuationToken)> GetAsync(string addressVirtual, int take, string continuation);
        Task SaveAsync(string addressVirtual, string hash, string context, Guid operationId);
    }
}
