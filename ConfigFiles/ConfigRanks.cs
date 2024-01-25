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

        [JsonProperty("weeklyHostPvP")]
        public int WeeklyHostRewardAmountPvP { get; private set; }
        [JsonProperty("weeklyHostPvM")]
        public int WeeklyHostRewardAmountPvM { get; private set; }
        [JsonProperty("weeklyHostRaids")]
        public int WeeklyHostRewardAmountRaids { get; private set; }
        [JsonProperty("weeklyHostMisc")]
        public int WeeklyHostRewardAmountMisc { get; private set; }
        [JsonProperty("weeklyAttendeePvP")] 
        public int WeeklyAttendeeRewardAmountPvP { get; private set; }
        [JsonProperty("weeklyAttendeePvM")]
        public int WeeklyAttendeeRewardAmountPvM { get; private set; }
        [JsonProperty("weeklyAttendeeRaids")]
        public int WeeklyAttendeeRewardAmountRaids { get; private set; }
        [JsonProperty("weeklyAttendeeMisc")]
        public int WeeklyAttendeeRewardAmountMisc { get; private set; }

        [JsonProperty("weeklyLimitPvM")]
        public int WeeklyPointLimitPvM { get; private set; }
        [JsonProperty("weeklyLimitPvP")]
        public int WeeklyPointLimitPvP { get; private set; }
        [JsonProperty("weeklyLimitRaids")]
        public int WeeklyPointLimitRaids { get; private set; }
        [JsonProperty("weeklyLimitMisc")]
        public int WeeklyPointLimitMisc { get; private set; }
    }
}
