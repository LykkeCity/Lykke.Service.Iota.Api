using Lykke.Service.Iota.Api.Core.Settings.SlackNotifications;

namespace Lykke.Service.Iota.Api.Settings
{
    public class AppSettings
    {
        public IotaApiSettings IotaApi { get; set; }
        public SlackNotificationsSettings SlackNotifications { get; set; }
    }
}
