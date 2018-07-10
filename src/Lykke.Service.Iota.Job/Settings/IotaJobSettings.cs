using Lykke.Common.Chaos;
using Lykke.Service.Iota.Api.Core.Settings;
using Lykke.SettingsReader.Attributes;
using System;

namespace Lykke.Service.Iota.Job.Settings
{
    public class IotaJobSettings
    {
        public DbSettings Db { get; set; }
        public string NodeUrl { get; set; }
        public int MinConfirmations { get; set; }
        public TimeSpan BalanceCheckerInterval { get; set; }
        public TimeSpan BroadcastCheckerInterval { get; set; }

        public int PromoteAttempts { get; set; }
        public int PromoteHeight { get; set; }
        public TimeSpan PromotionHandlerInterval { get; set; }

        public TimeSpan ReattachmentPeriod { get; set; }
        public TimeSpan ReattachmentHandlerInterval { get; set; }

        [Optional]
        public ChaosSettings ChaosKitty { get; set; }
    }
}
