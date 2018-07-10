using System;
using System.Threading.Tasks;
using Common;
using Common.Log;
using Lykke.Service.Iota.Job.Services;
using Lykke.Common.Log;

namespace Lykke.Service.Iota.Job.PeriodicalHandlers
{
    public class BroadcastHandler : TimerPeriod
    {
        private readonly ILog _log;
        private readonly IPeriodicalService _periodicalService;

        public BroadcastHandler(ILogFactory logFactory, TimeSpan period, IPeriodicalService periodicalService) :
            base(period, logFactory)
        {
            _log = logFactory.CreateLog(this);
            _periodicalService = periodicalService;
        }

        public override async Task Execute()
        {
            try
            {
                await _periodicalService.UpdateBroadcasts();
            }
            catch (Exception ex)
            {
                _log.Error("Failed to update broadcasts", ex);
            }
        }
    }
}
