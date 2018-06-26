using Lykke.Service.BlockchainApi.Contract.Addresses;
using Lykke.Service.Iota.Api.Core.Services;
using Lykke.Service.Iota.Api.Settings;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using System.Net;

namespace Lykke.Service.Iota.Api.Controllers
{
    [Route("api/addresses")]
    public class AddressesController : Controller
    {
        private readonly IIotaService _iotaService;
        private readonly IotaApiSettings _settings;

        public AddressesController(IIotaService iotaService,
            IotaApiSettings settings)
        {
            _iotaService = iotaService;
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
        [ProducesResponseType(typeof(AddressValidationResponse), (int)HttpStatusCode.OK)]
        public string[] GetAddressExplorerUrl([Required] string address)
        {
            return new string[]
            {
                _settings.ExplorerUrl.Replace("{address}", address)
            };
        }
    }
}
