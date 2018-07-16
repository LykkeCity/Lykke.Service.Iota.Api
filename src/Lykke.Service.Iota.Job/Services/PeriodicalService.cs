﻿using System;
using System.Threading.Tasks;
using System.Linq;
using Common;
using Common.Log;
using Tangle.Net.Utils;
using Newtonsoft.Json;
using Lykke.Common.Chaos;
using Lykke.Service.Iota.Api.Core.Services;
using Lykke.Service.Iota.Api.Core.Repositories;
using Lykke.Service.Iota.Api.Shared;
using Lykke.Service.Iota.Job.Settings;
using Lykke.Common.Log;

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
        private readonly IAddressVirtualRepository _addressVirtualRepository;
        private readonly IBuildRepository _buildRepository;
        private readonly INodeClient _nodeClient;
        private readonly IIotaService _iotaService;
        private readonly IotaJobSettings _settings;

        public PeriodicalService(ILogFactory logFactory,
            IChaosKitty chaosKitty,
            IBroadcastRepository broadcastRepository,
            IBroadcastInProgressRepository broadcastInProgressRepository,
            IBalanceRepository balanceRepository,
            IBalancePositiveRepository balancePositiveRepository,
            IAddressInputRepository addressInputRepository,
            IAddressVirtualRepository addressVirtualRepository,
            IBuildRepository buildRepository,
            INodeClient nodeClient,
            IIotaService iotaService,
            IotaJobSettings settings)
        {
            _log = logFactory.CreateLog(this);
            _chaosKitty = chaosKitty;
            _broadcastRepository = broadcastRepository;
            _broadcastInProgressRepository = broadcastInProgressRepository;
            _balanceRepository = balanceRepository;
            _balancePositiveRepository = balancePositiveRepository;
            _addressInputRepository = addressInputRepository;
            _addressVirtualRepository = addressVirtualRepository;
            _buildRepository = buildRepository;
            _nodeClient = nodeClient;
            _iotaService = iotaService;
            _settings = settings;
        }

        public async Task UpdateBroadcasts()
        {
            var list = await _broadcastInProgressRepository.GetAllAsync();

            foreach (var item in list)
            {
                var bundleInfo = await _nodeClient.GetBundleInfo(item.Hash);
                if (bundleInfo.Included)
                {
                    _log.Info("Brodcast update is detected", new { item.OperationId, amount = bundleInfo.Value, bundleInfo.Block });

                    await _broadcastRepository.SaveAsCompletedAsync(item.OperationId, bundleInfo.Value, 0, bundleInfo.Block);

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
                    await _nodeClient.Promote(info.Txs, _settings.PromoteAttempts);
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
                    var blockTime = DateTime.UtcNow - DateTimeOffset.FromUnixTimeMilliseconds(info.Block).UtcDateTime;

                    if (!info.Included && blockTime > _settings.ReattachmentPeriod)
                    {
                        var txLast = info.Txs.Last();
                        var start = DateTime.Now;

                        var result = await _nodeClient.Reattach(txLast);

                        _log.Info("Reattach transaction", new
                        {
                            secs = Math.Round((DateTime.Now - start).TotalSeconds, 1),
                            newTx = result.Hash,
                            oldTx = txLast
                        });
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

            foreach (var output in transactionContext.Outputs)
            {
                if (output.Address.StartsWith(Consts.VirtualAddressPrefix))
                {
                    virtualAddresses.Add(output.Address);
                }
                else
                {
                    var virtualAddress = await _addressVirtualRepository.GetVirtualAddressAsync(output.Address);
                    if (!string.IsNullOrEmpty(virtualAddress))
                    {
                        virtualAddresses.Add(virtualAddress);
                    }
                }
            }

            virtualAddresses = virtualAddresses.Distinct().ToList();

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
                    _log.Info("Positive balance is detected", new { virtualAddress, balance, block });

                    await RefreshInputs(virtualAddress);
                }
                if (balancePositive != null && balancePositive.Amount != balance)
                {
                    _log.Info("Change in positive balance is detected",
                        new { virtualAddress, balance, oldBalance = balancePositive.Amount, block });

                    await RefreshInputs(virtualAddress);
                }

                _chaosKitty.Meow(new { virtualAddress, balance, block }.ToJson());

                await _balancePositiveRepository.SaveAsync(virtualAddress, balance, block);

                return;
            }

            if (balance == 0 && deleteZeroBalance)
            {
                _log.Info($"Zero balance is detected", new { virtualAddress, balance = 0 });

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
                var wasSpent = await _nodeClient.HasCashOutTransaction(input.Address);
                if (wasSpent)
                {
                    _log.Info("Input with used private key is removed", 
                        new { input.AddressVirtual, input.Address, input.Index });

                    await _addressInputRepository.DeleteAsync(input.AddressVirtual, input.Address);
                }
            }
        }
    }
}
