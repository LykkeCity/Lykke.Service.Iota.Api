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
        private readonly int _minConfirmations;

        public PeriodicalService(ILog log,
            IChaosKitty chaosKitty,
            IBroadcastRepository broadcastRepository,
            IBroadcastInProgressRepository broadcastInProgressRepository,
            IBalanceRepository balanceRepository,
            IBalancePositiveRepository balancePositiveRepository,
            INodeClient nodeClient,
            int minConfirmations)
        {
            _log = log.CreateComponentScope(nameof(PeriodicalService));
            _chaosKitty = chaosKitty;
            _broadcastRepository = broadcastRepository;
            _broadcastInProgressRepository = broadcastInProgressRepository;
            _balanceRepository = balanceRepository;
            _balancePositiveRepository = balancePositiveRepository;
            _nodeClient = nodeClient;
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

                    await RefreshBalances(item.Hash);
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
                    _log.WriteInfo(nameof(UpdateBroadcasts), new { item.Hash },
                        $"Promotions for transaction is detected");

                    await _nodeClient.Promote(item.Hash);
                }
            }
        }

        private async Task RefreshBalances(string hash)
        {
            var addresses = await _nodeClient.GetBundleAddresses(hash);

            foreach (var address in addresses)
            {
                var balance = await _balanceRepository.GetAsync(address);
                if (balance != null)
                {
                    await RefreshAddressBalance(address, true);
                }
            }
        }

        private async Task<decimal> RefreshAddressBalance(string address, bool deleteZeroBalance)
        {
            var balance = await _nodeClient.GetAddressBalance(address, _minConfirmations);
            if (balance > 0)
            {
                var block = Timestamp.UnixSecondsTimestamp;

                var balancePositive = await _balancePositiveRepository.GetAsync(address);
                if (balancePositive == null)
                {
                    _log.WriteInfo(nameof(RefreshAddressBalance),
                        new { address, balance, block },
                        $"Positive balance is detected");
                }
                if (balancePositive != null && balancePositive.Amount != balance)
                {
                    _log.WriteInfo(nameof(RefreshAddressBalance),
                        new { address, balance, oldBalance = balancePositive.Amount, block },
                        $"Change in positive balance is detected");
                }

                await _balancePositiveRepository.SaveAsync(address, balance, block);

                _chaosKitty.Meow(new { address, balance, block }.ToJson());
            }

            if (balance == 0 && deleteZeroBalance)
            {
                _log.WriteInfo(nameof(RefreshAddressBalance), new { address },
                    $"Zero balance is detected");

                await _balancePositiveRepository.DeleteAsync(address);

                _chaosKitty.Meow(address);
            }

            return balance;
        }
    }
}
