using Lykke.Service.Iota.Api.Core.Repositories;
using Lykke.Service.Iota.Api.Core.Services;
using Lykke.Service.Iota.Api.Shared;
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
        private readonly IAddressVirtualRepository _addressVirtualRepository;
        private readonly IIotaService _iotaService;

        public InternalController(INodeClient nodeClient,
            IAddressRepository addressRepository, 
            IAddressInputRepository addressInputRepository,
            IAddressVirtualRepository addressVirtualRepository,
            IIotaService iotaService)
        {
            _nodeClient = nodeClient;
            _addressRepository = addressRepository;
            _addressInputRepository = addressInputRepository;
            _addressVirtualRepository = addressVirtualRepository;
            _iotaService = iotaService;
        }

        /// <summary>
        /// Saves real address and index for provided virtual Iota address
        /// </summary>
        [HttpPost("virtual-address/{address}")]
        public async Task<IActionResult> PostVirtualAddress([Required] string address, 
            [FromBody] VirtualAddressRequest virtualAddressRequest)
        {
            await _addressRepository.SaveAsync(address, virtualAddressRequest.RealAddress, virtualAddressRequest.Index);
            await _addressInputRepository.SaveAsync(address, virtualAddressRequest.RealAddress, virtualAddressRequest.Index);
            await _addressVirtualRepository.SaveAsync(virtualAddressRequest.RealAddress, address);

            return Ok();
        }

        [HttpGet("virtual-address/{address}/inputs")]
        public async Task<AddressInput[]> GetVirtualAddressInputs([Required] string address)
        {
            return await _iotaService.GetVirtualAddressInputs(address);
        }

        [HttpGet("address/{hash}/balance")]
        public async Task<long> HasCashOut([Required] string hash, int threshold)
        {
            return await _nodeClient.GetAddressBalance(hash, threshold);
        }

        [HttpGet("address/{hash}/can-recieve")]
        public async Task<bool> HasCashOut([Required] string hash)
        {
            var result = await _nodeClient.HasCashOutTransaction(hash);

            return !result;
        }

        [HttpGet("address/{hash}/has-pending-tx")]
        public async Task<bool> HasPendingTx([Required] string hash)
        {
            return await _nodeClient.HasPendingTransaction(hash);
        }

        [HttpGet("bundle/{hash}/reattach")]
        public async Task ReattachBundle([Required] string hash)
        {
            await _nodeClient.Reattach(hash);
        }

        [HttpGet("tx/{hash}/promote")]
        public async Task PromoteBundle([Required] string hash, [Required] int attemts, [Required] int depth)
        {
            var info = await _nodeClient.GetBundleInfo(hash);
            if (!info.Included)
            {
                await _nodeClient.Promote(info.Txs, attemts, depth);
            }
        }

        [HttpGet("tx/{hash}/included")]
        public async Task<bool> BundleIncluded([Required] string hash)
        {
            return await _nodeClient.TransactionIncluded(hash);
        }

        [HttpGet("node/info")]
        public async Task<string> GetNodeInfo()
        {
            return await _nodeClient.GetNodeInfo();
        }
    }
}
