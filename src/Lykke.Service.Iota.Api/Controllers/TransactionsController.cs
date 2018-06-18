using System;
using System.Net;
using System.Linq;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using Common;
using Common.Log;
using Lykke.Common.Api.Contract.Responses;
using Lykke.Service.BlockchainApi.Contract.Transactions;
using Lykke.Service.BlockchainApi.Contract;
using Lykke.Service.Iota.Api.Core.Domain;
using Lykke.Service.Iota.Api.Core.Repositories;
using Lykke.Service.Iota.Api.Helpers;
using Lykke.Service.Iota.Api.Core.Shared;
using Lykke.Service.Iota.Api.Core.Services;
using Lykke.Service.Iota.Api.Services;

namespace Lykke.Service.Iota.Api.Controllers
{
    [Route("api/transactions")]
    public class TransactionsController : Controller
    {
        private readonly ILog _log;
        private readonly IBuildRepository _buildRepository;
        private readonly IAddressInputRepository _addressInputRepository;
        private readonly IBroadcastRepository _broadcastRepository;
        private readonly IBroadcastInProgressRepository _broadcastInProgressRepository;
        private readonly INodeClient _nodeClient;
        private readonly IIotaService _iotaService;

        public TransactionsController(ILog log, 
            IBuildRepository buildRepository,
            IAddressInputRepository addressInputRepository,
            IBroadcastRepository broadcastRepository,
            IBroadcastInProgressRepository broadcastInProgressRepository,
            INodeClient nodeClient,
            IIotaService iotaService)
        {
            _log = log.CreateComponentScope(nameof(TransactionsController));
            _buildRepository = buildRepository;
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

            var addressInputs = await _addressInputRepository.GetAsync(request.FromAddress);
            if (!addressInputs.Any())
            {
                return BadRequest(ErrorResponse.Create($"{nameof(request.FromAddress)} was not found"));
            }

            var fromAddressBalance = await _iotaService.GetVirtualAddressBalance(request.FromAddress);
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

            _log.WriteInfo(nameof(Broadcast), request.ToJson(), "Broadcast transaction");

            var result = await _nodeClient.Broadcast(context.Transactions);

            await _broadcastRepository.AddAsync(request.OperationId, result.Hash, result.Block);
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

            await _log.WriteInfoAsync(nameof(TransactionsController), nameof(DeleteBroadcast),
                new { operationId = operationId }.ToJson(), 
                "Delete broadcast");

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
            await Task.Yield();

            return Ok();
        }
    }
}
