using Lykke.Service.BlockchainApi.Contract.Addresses;
using Lykke.Service.Iota.Api.Core.Repositories;
using Lykke.Service.Iota.Api.Core.Services;
using Lykke.Service.Iota.Api.Settings;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Threading.Tasks;

namespace Lykke.Service.Iota.Api.Controllers
{
    [Route("api/addresses")]
    public class AddressesController : Controller
    {
        private readonly IIotaService _iotaService;
        private readonly INodeClient _nodeClient;
        private readonly IBalancePositiveRepository _balancePositiveRepository;
        private readonly IAddressVirtualRepository _addressVirtualRepository;
        private readonly IotaApiSettings _settings;

        public AddressesController(IIotaService iotaService,
            INodeClient nodeClient,
            IBalancePositiveRepository balancePositiveRepository,
            IAddressVirtualRepository addressVirtualRepository,
            IotaApiSettings settings)
        {
            _iotaService = iotaService;
            _nodeClient = nodeClient;
            _balancePositiveRepository = balancePositiveRepository;
            _addressVirtualRepository = addressVirtualRepository;
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
            return Ok(new
            {
                underlyingAddress = await _iotaService.GetRealAddress(address)
            });
        }

        [HttpGet("{address}/virtual")]
        public async Task<IActionResult> GetAddressVirtual([Required] string address)
        {
            return Ok(new
            {
                virtualAddress = await _addressVirtualRepository.GetVirtualAddressAsync(address)
            });
        }
    }
}
