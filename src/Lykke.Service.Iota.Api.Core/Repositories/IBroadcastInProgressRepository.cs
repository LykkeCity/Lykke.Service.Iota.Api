using Lykke.Service.Iota.Api.Core.Domain.Broadcast;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Lykke.Service.Iota.Api.Core.Repositories
{
    public interface IBroadcastInProgressRepository
    {
        Task AddAsync(Guid operationId, string hash);
        Task DeleteAsync(Guid operationId);
        Task<IEnumerable<IBroadcastInProgress>> GetAllAsync();
    }
}
