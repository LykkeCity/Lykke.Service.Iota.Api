using Common.Log;
using Lykke.Service.Iota.Api.Core.Services;
using Lykke.Service.Iota.Api.Core.Repositories;
using System.Threading.Tasks;
using System.Linq;
using Lykke.Common.Chaos;
using Common;
using Tangle.Net.Utils;

namespace Lykke.Service.Iota.Job.Services
{
    public class PeriodicalService : IPeriodicalService
    {
        private ILog _log;
        private readonly IChaosKitty _chaosKitty;
        private readonly IBroadcastRepository _broadcastRepository;
        private readonly IBroadcastInProgressRepository _broadcastInProgressRepository;
        private readonly IBalanceRepository _balanceRepository;
        private readonly IBalancePositiveRepository _balancePositiveRepository;
        private readonly INodeClient _nodeClient;
        private readonly IIotaService _iotaService;
        private readonly int _minConfirmations;

        public PeriodicalService(ILog log,
            IChaosKitty chaosKitty,
            IBroadcastRepository broadcastRepository,
            IBroadcastInProgressRepository broadcastInProgressRepository,
            IBalanceRepository balanceRepository,
            IBalancePositiveRepository balancePositiveRepository,
            INodeClient nodeClient,
            IIotaService iotaService,
            int minConfirmations)
        {
            _log = log.CreateComponentScope(nameof(PeriodicalService));
            _chaosKitty = chaosKitty;
            _broadcastRepository = broadcastRepository;
            _broadcastInProgressRepository = broadcastInProgressRepository;
            _balanceRepository = balanceRepository;
            _balancePositiveRepository = balancePositiveRepository;
            _nodeClient = nodeClient;
            _iotaService = iotaService;
            _minConfirmations = minConfirmations;
        }

        public async Task UpdateBroadcasts()
        {
            var list = await _broadcastInProgressRepository.GetAllAsync();

            foreach (var item in list)
            {
                var included = await _nodeClient.TransactionIncluded(item.Hash);
                if (included)
                {
                    var txInfo = await _nodeClient.GetTransactionInfo(item.Hash);

                    _log.WriteInfo(nameof(UpdateBroadcasts),
                        new { item.OperationId, amount = txInfo.Value, txInfo.Block},
                        $"Brodcast update is detected");

                    await _broadcastRepository.SaveAsCompletedAsync(item.OperationId, txInfo.Value, 0, txInfo.Block);

                    _chaosKitty.Meow(item.OperationId);

                    await _broadcastInProgressRepository.DeleteAsync(item.OperationId);

                    _chaosKitty.Meow(item.OperationId);

                    //await RefreshBalances(item.Hash);
                }
            }
        }

        public async Task UpdateBalances()
        {
            var positiveBalances = await _balancePositiveRepository.GetAllAsync();
            var continuation = "";

            while (true)
            {
                var balances = await _balanceRepository.GetAsync(100, continuation);

                foreach (var balance in balances.Entities)
                {
                    var deleteZeroBalance = positiveBalances.Any(f => f.Address == balance.Address);

                    await RefreshAddressBalance(balance.Address, deleteZeroBalance);
                }

                if (string.IsNullOrEmpty(balances.ContinuationToken))
                {
                    break;
                }

                continuation = balances.ContinuationToken;
            }
        }

        public async Task PromoteBroadcasts()
        {
            var list = await _broadcastInProgressRepository.GetAllAsync();

            foreach (var item in list)
            {
                var included = await _nodeClient.TransactionIncluded(item.Hash);
                if (!included)
                {
                    _log.WriteInfo(nameof(UpdateBroadcasts), new { item.Hash }, $"Promote transaction");
                    await _nodeClient.Promote(item.Hash, 5);

                    included = await _nodeClient.TransactionIncluded(item.Hash);
                    if (!included)
                    {
                        _log.WriteInfo(nameof(UpdateBroadcasts), new { item.Hash }, $"Reattach transaction");
                        var result = await _nodeClient.Reattach(item.Hash);

                        await _broadcastRepository.UpdateHashAsync(item.OperationId, result.Hash, result.Block);
                        await _broadcastInProgressRepository.UpdateHashAsync(item.OperationId, result.Hash);
                    }
                }                
            }
        }

        //private async Task RefreshBalances(string hash)
        //{
        //    var addresses = await _nodeClient.GetBundleAddresses(hash);

        //    foreach (var address in addresses)
        //    {
        //        var balance = await _balanceRepository.GetAsync(address);
        //        if (balance != null)
        //        {
        //            await RefreshAddressBalance(address, true);
        //        }
        //    }
        //}

        private async Task<decimal> RefreshAddressBalance(string virtualAddress, bool deleteZeroBalance)
        {
            var balance = await _iotaService.GetVirtualAddressBalance(virtualAddress);
            if (balance > 0)
            {
                var block = Timestamp.UnixSecondsTimestamp;

                var balancePositive = await _balancePositiveRepository.GetAsync(virtualAddress);
                if (balancePositive == null)
                {
                    _log.WriteInfo(nameof(RefreshAddressBalance), new { virtualAddress, balance, block },
                        $"Positive balance is detected");
                }
                if (balancePositive != null && balancePositive.Amount != balance)
                {
                    _log.WriteInfo(nameof(RefreshAddressBalance),
                        new { virtualAddress, balance, oldBalance = balancePositive.Amount, block },
                        $"Change in positive balance is detected");
                }

                await _balancePositiveRepository.SaveAsync(virtualAddress, balance, block);

                _chaosKitty.Meow(new { virtualAddress, balance, block }.ToJson());
            }

            if (balance == 0 && deleteZeroBalance)
            {
                _log.WriteInfo(nameof(RefreshAddressBalance), new { virtualAddress },
                    $"Zero balance is detected");

                await _balancePositiveRepository.DeleteAsync(virtualAddress);

                _chaosKitty.Meow(virtualAddress);
            }

            return balance;
        }
    }
}
