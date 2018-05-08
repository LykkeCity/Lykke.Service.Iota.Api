using Lykke.Service.Iota.Api.Core.Settings.SlackNotifications;

namespace Lykke.Service.Iota.Job.Settings
{
    public class AppSettings
    {
        public IotaJobSettings IotaJob { get; set; }
        public SlackNotificationsSettings SlackNotifications { get; set; }
    }
}
