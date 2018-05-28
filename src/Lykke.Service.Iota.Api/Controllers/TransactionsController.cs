using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using System.Net;
using System;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using Common;
using Common.Log;
using Lykke.Common.Api.Contract.Responses;
using Lykke.Service.BlockchainApi.Contract.Transactions;
using Lykke.Service.BlockchainApi.Contract;
using Lykke.Service.Iota.Api.Core.Domain;
using Lykke.Service.Iota.Api.Core.Repositories;
using Lykke.Service.Iota.Api.Services;
using Lykke.Service.Iota.Api.Helpers;

namespace Lykke.Service.Iota.Api.Controllers
{
    [Route("api/transactions")]
    public class TransactionsController : Controller
    {
        private readonly ILog _log;
        private readonly IIotaService _iotaService;
        private readonly IBuildRepository _buildRepository;
        private readonly IAddressVirtualRepository _addressVirtualRepository;

        public TransactionsController(ILog log, 
            IIotaService iotaService,
            IBuildRepository buildRepository,
            IAddressVirtualRepository addressVirtualRepository)
        {
            _log = log;
            _iotaService = iotaService;
            _buildRepository = buildRepository;
            _addressVirtualRepository = addressVirtualRepository;
        }

        [HttpPost("single")]
        [ProducesResponseType(typeof(BuildTransactionResponse), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> Build([Required, FromBody] BuildSingleTransactionRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState.ToErrorResponse());
            }
            if (!request.FromAddress.StartsWith(Consts.VirtualAddressPrefix))
            {
                return BadRequest(ErrorResponse.Create($"{nameof(request.FromAddress)} must start " +
                    $"from {Consts.VirtualAddressPrefix}"));
            }
            if (!_iotaService.ValidateAddress(request.ToAddress))
            {
                return BadRequest(ErrorResponse.Create($"{nameof(request.ToAddress)} is not a valid"));
            }
            if (request.AssetId != Asset.Miota.Id)
            {
                return BadRequest(ErrorResponse.Create($"{nameof(request.AssetId)} was not found"));
            }
            if (!long.TryParse(request.Amount, out var amount))
            {
                return BadRequest(ErrorResponse.Create($"{nameof(request.Amount)} can not be converted to long"));
            }

            var addressVirtual = await _addressVirtualRepository.GetAsync(request.FromAddress);
            if (addressVirtual == null)
            {
                return BadRequest(ErrorResponse.Create($"{nameof(request.FromAddress)} was not found"));
            }

            var fromAddressBalance = await _iotaService.GetAddressBalance(addressVirtual.LatestAddress);
            if (amount > fromAddressBalance)
            {
                return BadRequest(BlockchainErrorResponse.FromKnownError(BlockchainErrorCode.NotEnoughtBalance));
            }

            var txType = request.ToAddress.StartsWith(Consts.VirtualAddressPrefix) ? Consts.TxCashin : Consts.TxCashout;
            if (txType == Consts.TxCashin && amount != fromAddressBalance)
            {
                return BadRequest(ErrorResponse.Create($"{nameof(amount)} ({amount}) must equal " +
                    $"{nameof(fromAddressBalance)} ({fromAddressBalance}) for the {txType} operation"));
            }

            var build = await _buildRepository.GetAsync(request.OperationId);
            if (build != null)
            {
                return Ok(new BuildTransactionResponse()
                {
                    TransactionContext = build.TransactionContext
                });
            }

            await _log.WriteInfoAsync(nameof(TransactionsController), nameof(Build),
                request.ToJson(), "Build transaction");

            var transactionContext = GetTxContext(request, amount, txType);

            await _buildRepository.AddAsync(request.OperationId, transactionContext);

            return Ok(new BuildTransactionResponse()
            {
                TransactionContext = transactionContext
            });
        }

        private static string GetTxContext(BuildSingleTransactionRequest request, long amount, string txType)
        {
            return new
            {
                Type = txType,
                Inputs = new object[]
                {
                    new
                    {
                        VirtualAddress = request.FromAddress
                    }
                },
                Outputs = new object[]
                {
                    new
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

            var broadcast = await _iotaService.GetBroadcastAsync(request.OperationId);
            if (broadcast != null)
            {
                return new StatusCodeResult(StatusCodes.Status409Conflict);
            }

            var transaction = _iotaService.GetTransaction(request.SignedTransaction);
            if (transaction == null)
            {
                return BadRequest(ErrorResponse.Create($"{nameof(request.SignedTransaction)} is not a valid"));
            }

            await _log.WriteInfoAsync(nameof(TransactionsController), nameof(Broadcast),
                request.ToJson(), "Broadcast transaction");

            await _iotaService.BroadcastAsync(transaction, request.OperationId);

            return Ok();
        }

        [HttpGet("broadcast/single/{operationId}")]
        [ProducesResponseType(typeof(BroadcastedSingleTransactionResponse), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetBroadcast([Required] Guid operationId)
        {
            var broadcast = await _iotaService.GetBroadcastAsync(operationId);
            if (broadcast == null)
            {
                return NoContent();
            }

            var amount = broadcast.Amount.HasValue ?
                Conversions.CoinsToContract(broadcast.Amount.Value, Asset.Miota.Accuracy) : "";

            var fee = broadcast.Fee.HasValue ?
                Conversions.CoinsToContract(broadcast.Fee.Value, Asset.Miota.Accuracy) : "";

            return Ok(new BroadcastedSingleTransactionResponse
            {
                OperationId = broadcast.OperationId,
                Hash = broadcast.Hash,
                State = broadcast.State.ToBroadcastedTransactionState(),
                Amount = amount,
                Fee = fee,
                Error = broadcast.Error,
                Timestamp = broadcast.GetTimestamp(),
                Block = broadcast.Block
            });
        }

        [HttpDelete("broadcast/{operationId}")]
        public async Task<IActionResult> DeleteBroadcast([Required] Guid operationId)
        {
            var broadcast = await _iotaService.GetBroadcastAsync(operationId);
            if (broadcast == null)
            {
                return NoContent();
            }

            await _log.WriteInfoAsync(nameof(TransactionsController), nameof(DeleteBroadcast),
                new { operationId = operationId }.ToJson(), 
                "Delete broadcast");

            await _buildRepository.DeleteAsync(operationId);
            await _iotaService.DeleteBroadcastAsync(broadcast);

            return Ok();
        }

        [HttpGet("history/from/{address}")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(HistoricalTransactionContract[]))]
        public async Task<IActionResult> GetHistoryFromAddress([Required] string address,
            [Required, FromQuery] int take, 
            [FromQuery] string afterHash)
        {
            await Task.Yield();

            return Ok();
        }
    }
}
