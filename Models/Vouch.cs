using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DragonBot.Models
{
    public class Vouch : Entity
    {
        public Vouch()
        {
            DateTime = DateTime.Now;
        }

        public string VouchDescription { get; set; }
        public int PointValue { get; set; }
        public DateTime DateTime { get; set; }
        public int ClanMemberId { get; set; }
        public ClanMember ClanMember { get; set; }
    }
}
