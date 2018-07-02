using System;

namespace Lykke.Service.Iota.Api.Core.Domain.Address
{
    public interface IAddressTransaction
    {
        string AddressVirtual { get; }
        string Hash { get; }
        string Context { get; }
        Guid OperationId { get; }
    }
}
