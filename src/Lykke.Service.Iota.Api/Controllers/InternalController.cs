using Lykke.Service.Iota.Api.Core.Repositories;
using Lykke.Service.Iota.Api.Models;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;

namespace Lykke.Service.Iota.Api.Controllers
{
    [Route("api/internal")]
    public class InternalController : Controller
    {
        private readonly IAddressRepository _addressRepository;
        private readonly IAddressVirtualRepository _addressVirtualRepository;

        public InternalController(IAddressRepository addressRepository, 
            IAddressVirtualRepository addressVirtualRepository)
        {
            _addressRepository = addressRepository;
            _addressVirtualRepository = addressVirtualRepository;
        }

        /// <summary>
        /// Saves real address and index for provided virtual Iota address
        /// </summary>
        [HttpPost("virtual-address/{address}")]
        public async Task<IActionResult> PostVirtualAddress([Required] string address, 
            [Required] VirtualAddressRequest virtualAddressRequest)
        {
            await _addressRepository.SaveAsync(address, virtualAddressRequest.RealAddress, virtualAddressRequest.Index);
            await _addressVirtualRepository.SaveAsync(address, virtualAddressRequest.Index);

            return Ok();
        }

        /// <summary>
        /// Returns latest used index for the provided virtual Iota address
        /// </summary>
        [HttpGet("virtual-address/{address}/index")]
        public async Task<long> GetVirtualAddressIndex([Required] string address)
        {
            var addressVirtual = await _addressVirtualRepository.GetAsync(address);
            if (addressVirtual == null)
            {
                return 0;
            }

            return addressVirtual.LatestAddressIndex;
        }
    }
}
