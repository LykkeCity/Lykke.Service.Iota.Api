using System;

namespace Lykke.Service.Iota.Api.Core.Domain.Build
{
    public interface IBuild
    {
        Guid OperationId { get; }
        string TransactionContext { get; }
    }
}
