﻿using Lykke.Service.Iota.Api.Core.Repositories;
using Lykke.Service.Iota.Api.Core.Services;
using Lykke.Service.Iota.Api.Models;
using Lykke.Service.Iota.Api.Services;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;

namespace Lykke.Service.Iota.Api.Controllers
{
    [Route("api/internal")]
    public class InternalController : Controller
    {
        private readonly INodeClient _nodeClient;
        private readonly IAddressRepository _addressRepository;
        private readonly IAddressVirtualRepository _addressVirtualRepository;

        public InternalController(INodeClient nodeClient,
            IAddressRepository addressRepository, 
            IAddressVirtualRepository addressVirtualRepository)
        {
            _nodeClient = nodeClient;
            _addressRepository = addressRepository;
            _addressVirtualRepository = addressVirtualRepository;
        }

        /// <summary>
        /// Saves real address and index for provided virtual Iota address
        /// </summary>
        [HttpPost("virtual-address/{address}")]
        public async Task<IActionResult> PostVirtualAddress([Required] string address, 
            [FromBody] VirtualAddressRequest virtualAddressRequest)
        {
            await _addressRepository.SaveAsync(address, virtualAddressRequest.RealAddress, virtualAddressRequest.Index);
            await _addressVirtualRepository.SaveAsync(address, virtualAddressRequest.RealAddress, virtualAddressRequest.Index);

            return Ok();
        }

        /// <summary>
        /// Returns latest used index for the provided virtual Iota address
        /// </summary>
        [HttpGet("virtual-address/{address}/real")]
        public async Task<IActionResult> GetVirtualAddressRealAddress([Required] string address)
        {
            var addressVirtual = await _addressVirtualRepository.GetAsync(address);
            if (addressVirtual == null)
            {
                return NotFound();
            }

            return Ok(addressVirtual.LatestAddress);
        }

        /// <summary>
        /// Returns latest used index for the provided virtual Iota address
        /// </summary>
        [HttpGet("virtual-address/{address}/index")]
        public async Task<IActionResult> GetVirtualAddressIndex([Required] string address)
        {
            var addressVirtual = await _addressVirtualRepository.GetAsync(address);
            if (addressVirtual == null)
            {
                return NotFound();
            }

            return Ok(addressVirtual.LatestAddressIndex);
        }

        /// <summary>
        /// Returns virtual Iota address balance
        /// </summary>
        [HttpGet("virtual-address/{address}/balance")]
        public async Task<IActionResult> GetVirtualAddressBalance([Required] string address)
        {
            var addressVirtual = await _addressVirtualRepository.GetAsync(address);
            if (addressVirtual == null)
            {
                return NotFound();
            }

            var fromAddressBalance = await _nodeClient.GetAddressBalance(addressVirtual.LatestAddress);

            return Ok(fromAddressBalance);
        }
    }
}
