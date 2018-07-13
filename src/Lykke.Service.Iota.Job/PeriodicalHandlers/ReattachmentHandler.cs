using System;
using System.Threading.Tasks;
using Common;
using Common.Log;
using Lykke.Service.Iota.Job.Services;
using Lykke.Common.Log;

namespace Lykke.Service.Iota.Job.PeriodicalHandlers
{
    public class ReattachmentHandler : TimerPeriod
    {
        private readonly ILog _log;
        private readonly IPeriodicalService _periodicalService;

        public ReattachmentHandler(ILogFactory logFactory, TimeSpan period, IPeriodicalService periodicalService) :
            base (period, logFactory)
        {
            _log = logFactory.CreateLog(this);
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
                _log.Info("Failed to reattach broadcasts", exception: ex);
            }
        }
    }    
}
