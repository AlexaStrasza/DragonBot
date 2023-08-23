using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DragonBot
{
    public struct ConfigRanks
    {
        [JsonProperty("ranks")]
        public List<Rank> Ranks { get; private set; }
        [JsonProperty("weeklyHost")]
        public int WeeklyHostRewardAmount { get; private set; }
        [JsonProperty("weeklyAttendee")]
        public int WeeklyAttendeeRewardAmount { get; private set; }
        [JsonProperty("weeklyLimit")]
        public int WeeklyPointLimit { get; private set; }
    }
}
