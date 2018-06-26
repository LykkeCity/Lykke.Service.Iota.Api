using Lykke.Service.BlockchainApi.Contract.Addresses;
using Lykke.Service.Iota.Api.Core.Domain;
using Lykke.Service.Iota.Api.Core.Repositories;
using Lykke.Service.Iota.Api.Core.Services;
using Lykke.Service.Iota.Api.Core.Shared;
using Lykke.Service.Iota.Api.Settings;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Tangle.Net.Utils;

namespace Lykke.Service.Iota.Api.Controllers
{
    [Route("api/addresses")]
    public class AddressesController : Controller
    {
        private readonly IIotaService _iotaService;
        private readonly INodeClient _nodeClient;
        private readonly IBalancePositiveRepository _balancePositiveRepository;
        private readonly IotaApiSettings _settings;

        public AddressesController(IIotaService iotaService,
            INodeClient nodeClient,
            IBalancePositiveRepository balancePositiveRepository,
            IotaApiSettings settings)
        {
            _iotaService = iotaService;
            _nodeClient = nodeClient;
            _balancePositiveRepository = balancePositiveRepository;
            _settings = settings;
        }

        [HttpGet("{address}/validity")]
        [ProducesResponseType(typeof(AddressValidationResponse), (int)HttpStatusCode.OK)]
        public IActionResult GetAddressValidity([Required] string address)
        {
            return Ok(new AddressValidationResponse()
            {
                IsValid = _iotaService.ValidateAddress(address)
            });
        }

        [HttpGet("{address}/balance")]
        public async Task<IActionResult> GetAddressBalance([Required] string address)
        {
            var isVirtualAddress = address.StartsWith(Consts.VirtualAddressPrefix);
            if (isVirtualAddress)
            {
                var isAddressCompromised = false;
                var inputs = await _iotaService.GetVirtualAddressInputs(address);
                foreach (var input in inputs.Where(f => f.Balance > 0))
                {
                    if (await _nodeClient.HasCashOutTransaction(input.Address))
                    {
                        isAddressCompromised = true;
                        break;
                    }
                }

                return Ok(new
                {
                    assetId = Asset.Miota.Id,
                    balance = await _iotaService.GetVirtualAddressBalance(address),
                    block = Timestamp.UnixSecondsTimestamp,
                    isAddressCompromised
                });
            }

            return Ok(new
            {
                assetId = Asset.Miota.Id,
                balance = await _nodeClient.GetAddressBalance(address, _settings.MinConfirmations),
                block = Timestamp.UnixSecondsTimestamp,
                isAddressCompromised = await _nodeClient.HasCashOutTransaction(address)
            });
        }

        [HttpGet("{address}/explorer-url")]
        public string[] GetAddressExplorerUrl([Required] string address)
        {
            return new string[]
            {
                _settings.ExplorerUrl.Replace("{address}", address)
            };
        }

        [HttpGet("{address}/underlying")]
        public async Task<IActionResult> GetAddressUnderlying([Required] string address)
        {
            var underlyingAddress = await _iotaService.GetRealAddress(address);

            return Ok(new
            {
                underlyingAddress
            });
        }
    }
}
