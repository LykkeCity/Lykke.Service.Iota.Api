using Common.Log;
using Lykke.Service.Iota.Api.Core.Services;
using Lykke.Service.Iota.Api.Core.Repositories;
using System.Threading.Tasks;
using System.Linq;
using Lykke.Common.Chaos;
using Common;
using Tangle.Net.Utils;
using System;
using Newtonsoft.Json;
using Lykke.Service.Iota.Api.Core.Shared;

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
        private readonly IAddressInputRepository _addressInputRepository;
        private readonly IBuildRepository _buildRepository;
        private readonly INodeClient _nodeClient;
        private readonly IIotaService _iotaService;
        private readonly int _minConfirmations;

        public PeriodicalService(ILog log,
            IChaosKitty chaosKitty,
            IBroadcastRepository broadcastRepository,
            IBroadcastInProgressRepository broadcastInProgressRepository,
            IBalanceRepository balanceRepository,
            IBalancePositiveRepository balancePositiveRepository,
            IAddressInputRepository addressInputRepository,
            IBuildRepository buildRepository,
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
            _addressInputRepository = addressInputRepository;
            _buildRepository = buildRepository;
            _nodeClient = nodeClient;
            _iotaService = iotaService;
            _minConfirmations = minConfirmations;
        }

        public async Task UpdateBroadcasts()
        {
            var list = await _broadcastInProgressRepository.GetAllAsync();

            foreach (var item in list)
            {
                var bundleInfo = await _nodeClient.GetBundleInfo(item.Hash);
                if (bundleInfo.Included)
                {
                    _log.WriteInfo(nameof(UpdateBroadcasts),
                        new { item.OperationId, amount = bundleInfo.TxValue, bundleInfo.TxBlock},
                        $"Brodcast update is detected");

                    await _broadcastRepository.SaveAsCompletedAsync(item.OperationId, bundleInfo.TxValue, 0, bundleInfo.TxBlock);

                    _chaosKitty.Meow(item.OperationId);

                    await _broadcastInProgressRepository.DeleteAsync(item.OperationId);

                    _chaosKitty.Meow(item.OperationId);

                    await RefreshOperationBalances(item.OperationId);
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
                var info = await _nodeClient.GetBundleInfo(item.Hash);
                if (!info.Included)
                {
                    _log.WriteInfo(nameof(PromoteBroadcasts), new { info.TxHash }, $"Promote transaction");

                    await _nodeClient.Promote(info.TxHash, new String('9', 81), 3);
                }
            }
        }

        public async Task ReattachBroadcasts()
        {
            var list = await _broadcastInProgressRepository.GetAllAsync();

            foreach (var item in list)
            {
                var info = await _nodeClient.GetBundleInfo(item.Hash);
                if (!info.Included)
                {
                    var mins = (DateTime.UtcNow - DateTimeOffset.FromUnixTimeMilliseconds(info.TxBlock).UtcDateTime).TotalMinutes;

                    if (!info.Included && mins > 3)
                    {
                        _log.WriteInfo(nameof(ReattachBroadcasts), new { info.TxHash }, $"Reattach transaction");

                        var result = await _nodeClient.Reattach(info.TxHash);
                    }
                }
            }
        }

        private async Task RefreshOperationBalances(Guid operationId)
        {
            var build = await _buildRepository.GetAsync(operationId);
            if (build == null)
            {
                return;
            }

            var transactionContext = JsonConvert.DeserializeObject<TransactionContext>(build.TransactionContext);

            var virtualAddresses = transactionContext.Inputs
                .Select(f => f.VirtualAddress)
                .ToList();
            virtualAddresses.AddRange(transactionContext.Outputs
                .Where(f => f.Address.StartsWith(Consts.VirtualAddressPrefix))
                .Select(f => f.Address));

            foreach (var virtualAddress in virtualAddresses)
            {
                var balance = await _balanceRepository.GetAsync(virtualAddress);
                if (balance != null)
                {
                    await RefreshAddressBalance(virtualAddress, true);
                }
            }
        }

        private async Task RefreshAddressBalance(string virtualAddress, bool deleteZeroBalance)
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

                    await RefreshInputs(virtualAddress);
                }
                if (balancePositive != null && balancePositive.Amount != balance)
                {
                    _log.WriteInfo(nameof(RefreshAddressBalance),
                        new { virtualAddress, balance, oldBalance = balancePositive.Amount, block },
                        $"Change in positive balance is detected");

                    await RefreshInputs(virtualAddress);
                }

                _chaosKitty.Meow(new { virtualAddress, balance, block }.ToJson());

                await _balancePositiveRepository.SaveAsync(virtualAddress, balance, block);

                return;
            }

            if (balance == 0 && deleteZeroBalance)
            {
                _log.WriteInfo(nameof(RefreshAddressBalance), new { virtualAddress },
                    $"Zero balance is detected");

                await RefreshInputs(virtualAddress);

                _chaosKitty.Meow(virtualAddress);

                await _balancePositiveRepository.DeleteAsync(virtualAddress);

                return;
            }
        }

        private async Task RefreshInputs(string virtualAddress)
        {
            var inputs = await _addressInputRepository.GetAsync(virtualAddress);
            foreach (var input in inputs)
            {
                var wasSpent = await _nodeClient.WereAddressesSpentFrom(input.Address);
                if (wasSpent)
                {
                    _log.WriteInfo(nameof(RefreshAddressBalance), input.ToJson(),
                        $"Input with used private key is removed");

                    await _addressInputRepository.DeleteAsync(input.AddressVirtual, input.Address);
                }
            }
        }
    }
}
