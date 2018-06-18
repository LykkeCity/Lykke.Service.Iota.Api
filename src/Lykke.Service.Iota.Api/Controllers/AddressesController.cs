using Lykke.Service.BlockchainApi.Contract.Addresses;
using Lykke.Service.Iota.Api.Core.Services;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using System.Net;

namespace Lykke.Service.Iota.Api.Controllers
{
    [Route("api/addresses")]
    public class AddressesController : Controller
    {
        private readonly IIotaService _iotaService;

        public AddressesController(IIotaService iotaService)
        {
            _iotaService = iotaService;
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
    }
}
