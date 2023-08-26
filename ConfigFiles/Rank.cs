using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DragonBot
{
    public struct Rank
    {
        [JsonProperty("rank")]
        public string RankName { get; private set; }
        [JsonProperty("pointRequirement")]
        public int PointRequirement { get; private set; }
        [JsonProperty("roleId")]
        public ulong RoleId { get; private set; }

        public bool IsDefault { get; set; }
    }
}
