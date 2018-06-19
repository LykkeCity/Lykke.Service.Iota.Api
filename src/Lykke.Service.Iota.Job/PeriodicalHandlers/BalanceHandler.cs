using System;
using System.Threading.Tasks;
using Common;
using Common.Log;
using Lykke.Service.Iota.Job.Services;

namespace Lykke.Service.Iota.Job.PeriodicalHandlers
{
    public class BalanceHandler : TimerPeriod
    {
        private ILog _log;
        private IPeriodicalService _periodicalService;

        public BalanceHandler(TimeSpan period, ILog log, IPeriodicalService periodicalService) :
            base(nameof(BalanceHandler), (int)period.TotalMilliseconds, log)
        {
            _log = log;
            _periodicalService = periodicalService;
        }

        public override async Task Execute()
        {
            try
            {
                await _periodicalService.UpdateBalances();
            }
            catch (Exception ex)
            {
                await _log.WriteErrorAsync(nameof(BalanceHandler), nameof(Execute), 
                    "Failed to update balances", ex);
            }
        }
    }
}
