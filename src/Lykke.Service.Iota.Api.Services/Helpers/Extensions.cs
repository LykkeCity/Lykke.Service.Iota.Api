using System;
using Tangle.Net.Entity;

namespace Lykke.Service.Iota.Api.Services.Helpers
{
    public static class Extensions
    {
        public static DateTime AttachmentDateTimeUtc(this Transaction self)
        {
            return DateTimeOffset.FromUnixTimeMilliseconds(self.AttachmentTimestamp).UtcDateTime;
        }

        public static string ValueWithChecksum(this Address self)
        {
            return $"{self.Value}{Checksum.FromAddress(self).Value}";
        }
    }
}
