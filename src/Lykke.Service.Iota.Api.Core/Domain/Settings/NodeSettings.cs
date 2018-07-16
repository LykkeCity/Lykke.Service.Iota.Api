using System;

namespace Lykke.Service.Iota.Api.Core.Domain.Settings
{
    public class NodeSettings
    {
        public string Url { get; set; }
        public string Version { get; set; }
        public TimeSpan Timeout { get; set; }
        public int Threshold { get; set; }
        public int BroadcastDepth { get; set; }
        public int PromoteDepth { get; set; }
        public int MinWeightMagnitude { get; set; }
    }
}
