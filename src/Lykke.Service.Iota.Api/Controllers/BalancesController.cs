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
using Lykke.Service.Iota.Api.Core.Services;
using Lykke.Common.Log;

namespace Lykke.Service.Iota.Api.Controllers
{
    [Route("api/balances")]
    public class BalancesController : Controller
    {
        private readonly ILog _log;
        private readonly IIotaService _iotaService;
        private readonly IBalanceRepository _balanceRepository;
        private readonly IBalancePositiveRepository _balancePositiveRepository;

        public BalancesController(ILogFactory logFactory, 
            IIotaService iotaService,
            IBalanceRepository balanceRepository,
            IBalancePositiveRepository balancePositiveRepository)
        {
            _log = logFactory.CreateLog(this);
            _iotaService = iotaService;
            _balanceRepository = balanceRepository;
            _balancePositiveRepository = balancePositiveRepository;
        }

        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(PaginationResponse<WalletBalanceContract>))]
        public async Task<IActionResult> Get([Required, FromQuery] int take, [FromQuery] string continuation)
        {
            if (!ModelState.IsValidTakeParameter(take))
            {
                return BadRequest(ModelState.ToErrorResponse());
            }

            var result = await _balancePositiveRepository.GetAsync(take, continuation);
            
            return Ok(PaginationResponse.From(
                result.ContinuationToken, 
                result.Entities.Select(f => f.ToWalletBalanceContract()).ToArray()
            ));
        }

        [HttpPost("{address}/observation")]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        public async Task<IActionResult> AddToObservations([Required] string address)
        {
            if (!ModelState.IsValidAddress(address))
            {
                return BadRequest(ModelState.ToErrorResponse());
            }

            var balance = await _balanceRepository.GetAsync(address);
            if (balance != null)
            {
                return new StatusCodeResult(StatusCodes.Status409Conflict);
            }

            _log.Info("Add address to observations", new { address = address });

            await _balanceRepository.AddAsync(address);

            return Ok();
        }

        [HttpDelete("{address}/observation")]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        public async Task<IActionResult> DeleteFromObservations([Required] string address)
        {
            if (!ModelState.IsValidAddress(address))
            {
                return BadRequest(ModelState.ToErrorResponse());
            }

            var balance = await _balanceRepository.GetAsync(address);
            if (balance == null)
            {
                return new StatusCodeResult(StatusCodes.Status204NoContent);
            }

            _log.Info("Delete address from observations", new { address = address });

            await _balancePositiveRepository.DeleteAsync(address);
            await _balanceRepository.DeleteAsync(address);

            return Ok();
        }
    }
}
