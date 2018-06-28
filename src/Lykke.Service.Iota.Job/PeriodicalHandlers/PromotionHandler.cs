using System;
using System.Threading.Tasks;
using Common;
using Common.Log;
using Lykke.Service.Iota.Job.Services;
using System.Threading;

namespace Lykke.Service.Iota.Job.PeriodicalHandlers
{
    public class PromotionHandler : IDisposable
    {
        private readonly TimerTrigger _timer;
        private readonly ILog _log;
        private readonly IPeriodicalService _periodicalService;

        public PromotionHandler(TimeSpan period, ILog log, IPeriodicalService periodicalService)
        {
            _log = log.CreateComponentScope(nameof(PromotionHandler));
            _periodicalService = periodicalService;

            _timer = new TimerTrigger(nameof(PromotionHandler), period, log, Timer_Triggered);
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
                _log.WriteError(nameof(Timer_Triggered), "Failed to promote transactions", ex);
            }
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }
    }   
}
