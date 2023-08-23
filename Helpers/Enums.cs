using DSharpPlus.SlashCommands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DragonBot.Helpers
{
    public class Enums
    {
        public enum TripType
        {
            [ChoiceName("Pvm Trip")]
            PvmTrip,
            [ChoiceName("Pking Trip")]
            PkingTrip
        }

        public enum TripAttendeeType
        {
            [ChoiceName("Attendee")]
            Attendee,
            [ChoiceName("Host")]
            Host
        }
    }
}
