using System;

namespace Lykke.Service.Iota.Api.Core.Domain.Address
{
    public class RealAddressTransaction
    {
        public string Hash { get; set; }
        public string FromAddress { get; set; }
        public string ToAddress { get; set; }
        public long Amount { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
