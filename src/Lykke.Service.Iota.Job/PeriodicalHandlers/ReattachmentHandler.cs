using System;
using System.Threading.Tasks;
using Common;
using Common.Log;
using Lykke.Service.Iota.Job.Services;

namespace Lykke.Service.Iota.Job.PeriodicalHandlers
{
    public class ReattachmentHandler : TimerPeriod
    {
        private ILog _log;
        private IPeriodicalService _periodicalService;

        public ReattachmentHandler(TimeSpan period, ILog log, IPeriodicalService periodicalService) :
            base(nameof(ReattachmentHandler), (int)period.TotalMilliseconds, log)
        {
            _log = log;
            _periodicalService = periodicalService;
        }

        public override async Task Execute()
        {
            try
            {
                await _periodicalService.ReattachBroadcasts();
            }
            catch (Exception ex)
            {
                await _log.WriteErrorAsync(nameof(ReattachmentHandler), nameof(Execute),
                    "Failed to reattach broadcasts", ex);
            }
        }
    }
}
