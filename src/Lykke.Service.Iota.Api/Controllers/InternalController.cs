using Lykke.Service.Iota.Api.Core.Repositories;
using Lykke.Service.Iota.Api.Core.Services;
using Lykke.Service.Iota.Api.Shared;
using Lykke.Service.Iota.Api.Models;
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
            var obj = new { address, virtualAddressRequest.RealAddress, virtualAddressRequest.Index };

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
        public async Task<long> GetBalance([Required] string hash)
        {
            return await _nodeClient.GetAddressBalance(hash);
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
        public async Task PromoteBundle([Required] string hash, [Required] int attemts)
        {
            var info = await _nodeClient.GetBundleInfo(hash);

            await _nodeClient.Promote(info.Txs, attemts);
        }

        [HttpGet("node/info")]
        public async Task<object> GetNodeInfo()
        {
            return await _nodeClient.GetNodeInfo();
        }
    }
}
