using System;

namespace Lykke.Service.Iota.Api.Core.Domain.Broadcast
{
    public interface IBroadcastInProgress
    {
        Guid OperationId { get; }
        string Hash { get; }
    }
}
