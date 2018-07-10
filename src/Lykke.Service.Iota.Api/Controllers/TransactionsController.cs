﻿using System;
using System.Net;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using Common;
using Common.Log;
using Lykke.Common.Log;
using Lykke.Common.Api.Contract.Responses;
using Lykke.Service.BlockchainApi.Contract.Transactions;
using Lykke.Service.BlockchainApi.Contract;
using Lykke.Service.Iota.Api.Core.Domain;
using Lykke.Service.Iota.Api.Core.Repositories;
using Lykke.Service.Iota.Api.Helpers;
using Lykke.Service.Iota.Api.Shared;
using Lykke.Service.Iota.Api.Core.Services;
using Lykke.Service.Iota.Api.Core.Domain.Address;

namespace Lykke.Service.Iota.Api.Controllers
{
    [Route("api/transactions")]
    public class TransactionsController : Controller
    {
        private readonly ILog _log;
        private readonly IBuildRepository _buildRepository;
        private readonly IAddressRepository _addressRepository;
        private readonly IAddressInputRepository _addressInputRepository;
        private readonly IBroadcastRepository _broadcastRepository;
        private readonly IBroadcastInProgressRepository _broadcastInProgressRepository;
        private readonly INodeClient _nodeClient;
        private readonly IIotaService _iotaService;

        public TransactionsController(ILogFactory logFactory, 
            IBuildRepository buildRepository,
            IAddressRepository addressRepository,
            IAddressInputRepository addressInputRepository,
            IBroadcastRepository broadcastRepository,
            IBroadcastInProgressRepository broadcastInProgressRepository,
            INodeClient nodeClient,
            IIotaService iotaService)
        {
            _log = logFactory.CreateLog(this);
            _buildRepository = buildRepository;
            _addressRepository = addressRepository;
            _addressInputRepository = addressInputRepository;
            _broadcastRepository = broadcastRepository;
            _broadcastInProgressRepository = broadcastInProgressRepository;
            _nodeClient = nodeClient;
            _iotaService = iotaService;
        }

        [HttpPost("single")]
        [ProducesResponseType(typeof(BuildTransactionResponse), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> Build([Required, FromBody] BuildSingleTransactionRequest request)
        {
            if (!ModelState.IsValid ||
                !ModelState.IsValidAddress(request.ToAddress, nameof(request.FromAddress)))
            {
                return BadRequest(ModelState.ToErrorResponse());
            }
            if (!request.FromAddress.StartsWith(Consts.VirtualAddressPrefix))
            {
                return BadRequest(ErrorResponse.Create($"{nameof(request.FromAddress)} must start " +
                    $"from {Consts.VirtualAddressPrefix}"));
            }
            if (request.AssetId != Asset.Miota.Id)
            {
                return BadRequest(ErrorResponse.Create($"{nameof(request.AssetId)} was not found"));
            }
            if (!long.TryParse(request.Amount, out var amount))
            {
                return BadRequest(ErrorResponse.Create($"{nameof(request.Amount)} can not be converted to long"));
            }

            var build = await _buildRepository.GetAsync(request.OperationId);
            if (build != null)
            {
                return Ok(new BuildTransactionResponse()
                {
                    TransactionContext = build.TransactionContext
                });
            }

            _log.Info("Build transaction", request);

            var txType = request.GetTransactionType();

            var inputsValidation = await ValidateTxInputs(request, amount);
            if (inputsValidation != null)
            {
                return BadRequest(inputsValidation);
            }

            var outputsValidation = await ValidateTxOutputs(request, amount, txType);
            if (inputsValidation != null)
            {
                return BadRequest(inputsValidation);
            }

            var transactionContext = GetTxContext(request, amount, txType);

            await _buildRepository.AddAsync(request.OperationId, transactionContext);

            return Ok(new BuildTransactionResponse()
            {
                TransactionContext = transactionContext
            });
        }

        private async Task<BlockchainErrorResponse> ValidateTxInputs(BuildSingleTransactionRequest request, long amount)
        {
            var fromAddressBalance = await _iotaService.GetVirtualAddressBalance(request.FromAddress);
            if (amount > fromAddressBalance)
            {
                return BlockchainErrorResponse.FromKnownError(BlockchainErrorCode.NotEnoughBalance);
            }

            var addressInputs = await _addressInputRepository.GetAsync(request.FromAddress);
            if (!addressInputs.Any())
            {
                return BlockchainErrorResponse.FromUnknownError(
                    $"Inputs for {nameof(request.FromAddress)} were not found");
            }

            foreach (var addressInput in addressInputs)
            {
                var addressHasCashOut = await _nodeClient.HasCashOutTransaction(addressInput.Address);
                if (addressHasCashOut)
                {
                    return BlockchainErrorResponse.FromUnknownError(
                        $"Input address {addressInput.Address} has completed cash-out transaction");
                }

                var hasPendingTx = await _nodeClient.HasPendingTransaction(addressInput.Address);
                if (hasPendingTx)
                {
                    return BlockchainErrorResponse.FromUnknownError(
                        $"Input address {addressInput.Address} has pending transaction");
                }
            }

            return null;
        }

        private async Task<BlockchainErrorResponse> ValidateTxOutputs(BuildSingleTransactionRequest request, long amount,
            Shared.TransactionType type)
        {
            var toAddress = request.ToAddress;

            if (type == Shared.TransactionType.Cashin)
            {
                var toRealAddress = await _iotaService.GetRealAddress(toAddress);

                var addressHasCashOut = await _nodeClient.HasCashOutTransaction(toRealAddress);
                if (addressHasCashOut)
                {
                    return BlockchainErrorResponse.FromUnknownError(
                        $"Output address {toRealAddress} has completed cash-out transaction");
                }

                var hasPendingTx = await _nodeClient.HasPendingTransaction(toRealAddress, true);
                if (hasPendingTx)
                {
                    return BlockchainErrorResponse.FromUnknownError(
                        $"Output address {toRealAddress} has pending transaction");
                }
            }

            if (type == Shared.TransactionType.Cashout)
            {
                var addressHasCashOut = await _nodeClient.HasCashOutTransaction(toAddress);
                if (addressHasCashOut)
                {
                    return BlockchainErrorResponse.FromUnknownError(
                        $"Output address {toAddress} has completed cash-out transaction");
                }
            }

            return null;
        }

        private string GetTxContext(BuildSingleTransactionRequest request, long amount,
            Shared.TransactionType type)
        {
            return new TransactionContext
            {
                Type = type,
                Inputs = new TransactionInput[]
                {
                    new TransactionInput
                    {
                        VirtualAddress = request.FromAddress
                    }
                },
                Outputs = new TransactionOutput[]
                {
                    new TransactionOutput
                    {
                        Address = request.ToAddress,
                        Value = amount
                    }
                },
            }.ToJson();
        }

        [HttpPut]
        [ProducesResponseType(StatusCodes.Status501NotImplemented)]
        public IActionResult Rebuild()
        {
            return new StatusCodeResult(StatusCodes.Status501NotImplemented);
        }

        [HttpPost("broadcast")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> Broadcast([Required, FromBody] BroadcastTransactionRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState.ToErrorResponse());
            }

            var broadcast = await _broadcastRepository.GetAsync(request.OperationId);
            if (broadcast != null)
            {
                return new StatusCodeResult(StatusCodes.Status409Conflict);
            }

            var context = JsonConvert.DeserializeObject<SignedTransactionContext>(request.SignedTransaction);
            if (context == null || string.IsNullOrEmpty(context.Hash) || context.Transactions == null || !context.Transactions.Any())
            {
                return BadRequest(ErrorResponse.Create($"{nameof(request.SignedTransaction)} is not a valid"));
            }

            _log.Info("Broadcast transaction", request);

            var result = await _nodeClient.Broadcast(context.Transactions);
            if (!result.Block.HasValue)
            {
                await _broadcastRepository.AddFailedAsync(request.OperationId, result.Error);

                return Ok();
            }

            await _broadcastRepository.AddAsync(request.OperationId, result.Hash, result.Block.Value);
            await _broadcastInProgressRepository.AddAsync(request.OperationId, result.Hash);

            return Ok();
        }

        [HttpGet("broadcast/single/{operationId}")]
        [ProducesResponseType(typeof(BroadcastedSingleTransactionResponse), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetBroadcast([Required] Guid operationId)
        {
            var broadcast = await _broadcastRepository.GetAsync(operationId);
            if (broadcast == null)
            {
                return NoContent();
            }

            return Ok(new BroadcastedSingleTransactionResponse
            {
                OperationId = broadcast.OperationId,
                Hash = broadcast.Hash,
                State = broadcast.State.ToBroadcastedTransactionState(),
                Amount = broadcast.Amount.HasValue ? broadcast.Amount.Value.ToString() : "",
                Fee = broadcast.Fee.HasValue ? broadcast.Fee.Value.ToString() : "",
                Error = broadcast.Error,
                Timestamp = broadcast.GetTimestamp(),
                Block = broadcast.Block
            });
        }

        [HttpDelete("broadcast/{operationId}")]
        public async Task<IActionResult> DeleteBroadcast([Required] Guid operationId)
        {
            var broadcast = await _broadcastRepository.GetAsync(operationId);
            if (broadcast == null)
            {
                return NoContent();
            }

            _log.Info("Delete broadcast", new { operationId = operationId });

            await _buildRepository.DeleteAsync(broadcast.OperationId);
            await _broadcastInProgressRepository.DeleteAsync(broadcast.OperationId);
            await _broadcastRepository.DeleteAsync(broadcast.OperationId);

            return Ok();
        }

        [HttpGet("history/from/{address}")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(HistoricalTransactionContract[]))]
        public async Task<IActionResult> GetHistoryFromAddress([Required] string address,
            [Required, FromQuery] int take, 
            [FromQuery] string afterHash)
        {
            if (!ModelState.IsValid ||
                !ModelState.IsValidAddress(address) ||
                !ModelState.IsValidTakeParameter(take))
            {
                return BadRequest(ModelState.ToErrorResponse());
            }

            var txs = new List<RealAddressTransaction>();

            if (address.StartsWith(Consts.VirtualAddressPrefix))
            {
                txs = await GetFromVirtualAddressTransactions(address, take, afterHash);
            }
            else
            {
                txs = GetTxs(await _nodeClient.GetFromAddressTransactions(address), take, afterHash);
            }

            return Ok(GetHistoricalTxs(txs));
        }

        [HttpGet("history/to/{address}")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(HistoricalTransactionContract[]))]
        public async Task<IActionResult> GetHistoryToAddress([Required] string address,
            [Required, FromQuery] int take,
            [FromQuery] string afterHash)
        {
            if (!ModelState.IsValid ||
                !ModelState.IsValidAddress(address) ||
                !ModelState.IsValidTakeParameter(take))
            {
                return BadRequest(ModelState.ToErrorResponse());
            }

            var txs = new List<RealAddressTransaction>();

            if (address.StartsWith(Consts.VirtualAddressPrefix))
            {
                txs = await GetToVirtualAddressTransactions(address, take, afterHash);
            }
            else
            {
                txs = GetTxs(await _nodeClient.GetToAddressTransactions(address), take, afterHash);
            }

            return Ok(GetHistoricalTxs(txs));
        }

        private async Task<List<RealAddressTransaction>> GetFromVirtualAddressTransactions(string virtualAddress, int take, string afterHash)
        {
            var txs = new List<RealAddressTransaction>();

            var addresses = await _addressRepository.GetAsync(virtualAddress);
            if (addresses.Any())
            {
                if (!string.IsNullOrEmpty(afterHash))
                {
                    var bundleAddresses = await _nodeClient.GetBundleAddresses(afterHash);
                    if (!string.IsNullOrEmpty(bundleAddresses.From))
                    {
                        var address = addresses.FirstOrDefault(f => f.Address == bundleAddresses.From);
                        if (address != null)
                        {
                            addresses = addresses.Where(f => f.Index >= address.Index);
                        }
                    }
                }

                foreach (var address in addresses)
                {
                    var fromAddressTxs = await _nodeClient.GetFromAddressTransactions(address.Address);
                    foreach (var fromAddressTx in fromAddressTxs)
                    {
                        if (fromAddressTx.Hash != afterHash)
                        {
                            txs.Add(fromAddressTx);

                            if (txs.Count >= take)
                            {
                                break;
                            }
                        }
                    }
                }
            }

            return txs;
        }

        private async Task<List<RealAddressTransaction>> GetToVirtualAddressTransactions(string virtualAddress, int take, string afterHash)
        {
            var txs = new List<RealAddressTransaction>();

            var addresses = await _addressRepository.GetAsync(virtualAddress);
            if (addresses.Any())
            {
                if (!string.IsNullOrEmpty(afterHash))
                {
                    var bundleAddresses = await _nodeClient.GetBundleAddresses(afterHash);
                    if (bundleAddresses.To != null)
                    {
                        foreach (var toAddress in bundleAddresses.To)
                        {
                            var address = addresses.FirstOrDefault(f => f.Address == toAddress);
                            if (address != null)
                            {
                                addresses = addresses.Where(f => f.Index >= address.Index);
                                break;
                            }
                        }
                    }
                }

                foreach (var address in addresses)
                {
                    var toAddressTxs = await _nodeClient.GetToAddressTransactions(address.Address);
                    foreach (var toAddressTx in toAddressTxs)
                    {
                        if (toAddressTx.Hash != afterHash)
                        {
                            txs.Add(toAddressTx);

                            if (txs.Count >= take)
                            {
                                break;
                            }
                        }
                    }
                }
            }

            return txs;
        }

        private List<RealAddressTransaction> GetTxs(IEnumerable<RealAddressTransaction> txs, int take, string afterHash)
        {
            return txs
                .OrderByDescending(f => f.Timestamp)
                .TakeWhile(f => f.Hash != afterHash)
                .Reverse()
                .Take(take)
                .ToList();
        }

        private HistoricalTransactionContract[] GetHistoricalTxs(List<RealAddressTransaction> txs)
        {
            return txs.Select(f => new HistoricalTransactionContract
            {
                Amount = f.Amount.ToString(),
                AssetId = Asset.Miota.Id,
                FromAddress = f.FromAddress,
                Hash = f.Hash,
                Timestamp = f.Timestamp,
                ToAddress = f.ToAddress
            }).ToArray();
        }
    }
}
