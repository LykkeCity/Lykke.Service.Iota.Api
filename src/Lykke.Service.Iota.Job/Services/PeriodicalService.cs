using Common.Log;
using Lykke.Service.Iota.Api.Core.Services;
using Lykke.Service.Iota.Api.Core.Repositories;
using System.Threading.Tasks;
using System.Linq;
using Lykke.Common.Chaos;
using Common;
using Tangle.Net.Utils;
using System;

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
                var info = await _nodeClient.GetBundleInfo(item.Hash);
                if (!info.Included)
                {
                    _log.WriteInfo(nameof(PromoteBroadcasts), new { item.Hash }, $"Promote transaction");

                    try
                    {
                        await _nodeClient.Promote(info.TxHash, 10);
                    }
                    catch (Exception ex)
                    {
                        _log.WriteInfo(nameof(PromoteBroadcasts), new { item.Hash }, $"Failed to promote: {ex.ToString()}");
                    }

                    info = await _nodeClient.GetBundleInfo(info.TxHash);
                    if (!info.Included)
                    {
                        _log.WriteInfo(nameof(PromoteBroadcasts), new { item.Hash }, $"Reattach transaction");
                        var result = await _nodeClient.Reattach(item.Hash);
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

            var inputs = await _addressInputRepository.GetAsync(virtualAddress);
            foreach (var input in inputs)
            {
                var wasSpent = await _nodeClient.WereAddressesSpentFrom(input.Address);
                if (wasSpent)
                {
                    var inputBalance = await _nodeClient.GetAddressBalance(input.Address, _minConfirmations);
                    if (inputBalance > 0)
                    {
                        _log.WriteError(nameof(RefreshAddressBalance), input.ToJson(),
                            new Exception("Positive balance is for the input with used private key"));
                    }
                    else
                    {
                        _log.WriteInfo(nameof(RefreshAddressBalance), input.ToJson(),
                            $"Input with used private key is removed");

                        await _addressInputRepository.DeleteAsync(input.AddressVirtual, input.Address);
                    }
                }
            }

            return balance;
        }
    }
}
