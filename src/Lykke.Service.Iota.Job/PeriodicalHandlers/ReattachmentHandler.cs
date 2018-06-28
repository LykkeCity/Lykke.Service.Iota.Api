using System;
using System.Threading.Tasks;
using Common;
using Common.Log;
using Lykke.Service.Iota.Job.Services;
using System.Threading;

namespace Lykke.Service.Iota.Job.PeriodicalHandlers
{
    public class ReattachmentHandler : IDisposable
    {
        private readonly TimerTrigger _timer;
        private readonly ILog _log;
        private readonly IPeriodicalService _periodicalService;

        public ReattachmentHandler(TimeSpan period, ILog log, IPeriodicalService periodicalService)
        {
            _log = log.CreateComponentScope(nameof(ReattachmentHandler));
            _periodicalService = periodicalService;

            _timer = new TimerTrigger(nameof(ReattachmentHandler), period, log, Timer_Triggered);
            _timer.Start();
        }

        private async Task Timer_Triggered(ITimerTrigger timer, TimerTriggeredHandlerArgs args, CancellationToken cancellationToken)
        {
            try
            {
                await _periodicalService.PromoteBroadcasts();
            }
            catch (Exception ex)
            {
                _log.WriteError(nameof(Timer_Triggered), "Failed to reattach broadcasts", ex);
            }
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }
    }    
}
