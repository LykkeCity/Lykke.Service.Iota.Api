using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;

namespace Lykke.Service.Iota.Api.Models
{
    [DataContract]
    public class VirtualAddressRequest
    {
        [DataMember]
        [Required]
        public string RealAddress { get; set; }

        [DataMember]
        [Required]
        public int Index { get; set; }
    }
}
