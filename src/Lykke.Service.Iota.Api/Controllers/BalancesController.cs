using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using System.Net;
using System.Linq;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using Common;
using Common.Log;
using Lykke.Common.Api.Contract.Responses;
using Lykke.Service.BlockchainApi.Contract;
using Lykke.Service.BlockchainApi.Contract.Balances;
using Lykke.Service.Iota.Api.Core.Repositories;
using Lykke.Service.Iota.Api.Helpers;
using Lykke.Service.Iota.Api.Services;

namespace Lykke.Service.Iota.Api.Controllers
{
    [Route("api/balances")]
    public class BalancesController : Controller
    {
        private readonly ILog _log;
        private readonly IIotaService _iotaService;
        private readonly IBalanceRepository _balanceRepository;
        private readonly IBalancePositiveRepository _balancePositiveRepository;

        public BalancesController(ILog log, 
            IIotaService iotaService,
            IBalanceRepository balanceRepository,
            IBalancePositiveRepository balancePositiveRepository)
        {
            _log = log;
            _iotaService = iotaService;
            _balanceRepository = balanceRepository;
            _balancePositiveRepository = balancePositiveRepository;
        }

        [HttpGet]
        public async Task<PaginationResponse<WalletBalanceContract>> Get([Required, FromQuery] int take, [FromQuery] string continuation)
        {
            var result = await _balancePositiveRepository.GetAsync(take, continuation);
            
            return PaginationResponse.From(
                result.ContinuationToken, 
                result.Entities.Select(f => f.ToWalletBalanceContract()).ToArray()
            );
        }

        [HttpPost("{address}/observation")]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        public async Task<IActionResult> AddToObservations([Required] string address)
        {
            if (string.IsNullOrEmpty(address))
            {
                return BadRequest(ErrorResponse.Create($"{nameof(address)} is null or empty"));
            }
            if (!_iotaService.ValidateAddress(address))
            {
                return BadRequest(ErrorResponse.Create($"{nameof(address)} is not valid"));
            }

            var balance = await _balanceRepository.GetAsync(address);
            if (balance != null)
            {
                return new StatusCodeResult(StatusCodes.Status409Conflict);
            }

            await _log.WriteInfoAsync(nameof(BalancesController), nameof(AddToObservations),
                new { address = address }.ToJson(), "Add address to observations");

            await _balanceRepository.AddAsync(address);

            return Ok();
        }

        [HttpDelete("{address}/observation")]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        public async Task<IActionResult> DeleteFromObservations([Required] string address)
        {
            if (string.IsNullOrEmpty(address))
            {
                return BadRequest(ErrorResponse.Create($"{nameof(address)} is null or empty"));
            }

            var balance = await _balanceRepository.GetAsync(address);
            if (balance == null)
            {
                return new StatusCodeResult(StatusCodes.Status204NoContent);
            }

            await _log.WriteInfoAsync(nameof(BalancesController), nameof(DeleteFromObservations),
                new { address = address }.ToJson(), "Delete address from observations");

            await _balancePositiveRepository.DeleteAsync(address);
            await _balanceRepository.DeleteAsync(address);

            return Ok();
        }
    }
}
