﻿using System.Collections.Generic;
using System.Threading.Tasks;
using Lykke.Service.Iota.Api.Core.Domain.Address;

namespace Lykke.Service.Iota.Api.Core.Repositories
{
    public interface IAddressRepository
    {
        Task<IEnumerable<IAddress>> GetAsync(string addressVirtual);
        Task<(IEnumerable<IAddress> Entities, string ContinuationToken)> GetAsync(string addressVirtual, int take, string continuation);
        Task<IAddress> GetAsync(string addressVirtual, string address);
        Task SaveAsync(string addressVirtual, string address, long index);
        Task DeleteAsync(string addressVirtual, string address);
    }
}
