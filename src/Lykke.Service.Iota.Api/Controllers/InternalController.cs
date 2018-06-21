using Lykke.Service.Iota.Api.Core.Repositories;
using Lykke.Service.Iota.Api.Core.Services;
using Lykke.Service.Iota.Api.Core.Shared;
using Lykke.Service.Iota.Api.Models;
using Microsoft.AspNetCore.Mvc;
using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace Lykke.Service.Iota.Api.Controllers
{
    [Route("api/internal")]
    public class InternalController : Controller
    {
        private readonly INodeClient _nodeClient;
        private readonly IAddressRepository _addressRepository;
        private readonly IAddressInputRepository _addressInputRepository;
        private readonly IIotaService _iotaService;

        public InternalController(INodeClient nodeClient,
            IAddressRepository addressRepository, 
            IAddressInputRepository addressInputRepository,
            IIotaService iotaService)
        {
            _nodeClient = nodeClient;
            _addressRepository = addressRepository;
            _addressInputRepository = addressInputRepository;
            _iotaService = iotaService;
        }

        /// <summary>
        /// Saves real address and index for provided virtual Iota address
        /// </summary>
        [HttpPost("virtual-address/{address}")]
        public async Task<IActionResult> PostVirtualAddress([Required] string address, 
            [FromBody] VirtualAddressRequest virtualAddressRequest)
        {
            await _addressRepository.SaveAsync(address, virtualAddressRequest.RealAddress, virtualAddressRequest.Index, false);
            await _addressInputRepository.SaveAsync(address, virtualAddressRequest.RealAddress, virtualAddressRequest.Index);

            return Ok();
        }

        /// <summary>
        /// Returns latest real address for the provided virtual Iota address
        /// </summary>
        [HttpGet("virtual-address/{address}/real")]
        public async Task<IActionResult> GetVirtualAddressRealAddress([Required] string address)
        {
            var addressInputs = await _addressInputRepository.GetAsync(address);
            if (addressInputs == null || addressInputs.Count() == 0)
            {
                return NotFound();
            }

            var latestIndex = addressInputs.Max(f => f.Index);
            var latestAddress = addressInputs.First(f => f.Index == latestIndex);

            return Ok(latestAddress.Address);
        }

        /// <summary>
        /// Returns available inputs for the provided virtual Iota address
        /// </summary>
        [HttpGet("virtual-address/{address}/inputs")]
        public async Task<AddressInput[]> GetVirtualAddressIndex([Required] string address)
        {
            return await _iotaService.GetVirtualAddressInputs(address);
        }

        [HttpGet("address/{hash}/can-recieve")]
        public async Task<bool> PromoteBundle([Required] string hash)
        {
            var result = await _nodeClient.WereAddressesSpentFrom(hash);

            return !result;
        }

        [HttpGet("bundle/{hash}/reattach")]
        public async Task ReattachBundle([Required] string hash)
        {
            await _nodeClient.Reattach(hash);
        }

        [HttpGet("tx/{hash}/promote")]
        public async Task PromoteBundle([Required] string hash, [Required] int attemts)
        {
            await _nodeClient.Promote(hash, attemts);
        }

        [HttpGet("tx/{hash}/included")]
        public async Task<bool> BundleIncluded([Required] string hash)
        {
            return await _nodeClient.TransactionIncluded(hash);
        }
    }
}
