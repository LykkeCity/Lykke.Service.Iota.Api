using Lykke.Service.Iota.Api.Core.Settings.ServiceSettings;

namespace Lykke.Service.Iota.Api.Settings
{
    public class IotaApiSettings
    {
        public DbSettings Db { get; set; }
        public string NodeUrl { get; set; }
        public string ExplorerUrl { get; set; }
        public int MinConfirmations { get; set; }
    }
}
