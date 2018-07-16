using Lykke.Service.Iota.Api.Core.Domain.Settings;
using Lykke.Service.Iota.Api.Core.Settings;

namespace Lykke.Service.Iota.Api.Settings
{
    public class IotaApiSettings
    {
        public DbSettings Db { get; set; }
        public NodeSettings Node { get; set; }
        public string ExplorerUrl { get; set; }
    }
}
