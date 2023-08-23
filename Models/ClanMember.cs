using DSharpPlus.Entities;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DragonBot.Models
{
    public class ClanMember : Entity
    {
        public ClanMember()
        {
            Vouches = new List<Vouch>();
        }
        public ulong DiscordId { get; set; }
        public int ClanPoints { get; set; }
        public ICollection<Vouch> Vouches { get; set; }
        public string RSN { get; set; }
        public int Referrals { get; set; }
        public DateTime LastUpdatePvM { get; set; }
        public int WeekPointsPvM { get; set; }
        public DateTime LastUpdatePvP { get; set; }
        public int WeekPointsPvP { get; set; }
    }
}
