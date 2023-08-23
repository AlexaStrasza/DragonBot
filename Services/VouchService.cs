using DragonBot.Context;
using DragonBot.Models;
using DragonBot.ViewModel;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DragonBot.Services
{
    public interface IVouchService
    {
        Task<List<Vouch>> GetVouchesOfUserAsync(ulong discordId);
    }

    public class VouchService : IVouchService
    {
        private readonly DbContextOptions<DragonContext> _options;

        public VouchService(DbContextOptions<DragonContext> options)
        {
            _options = options;
        }

        public async Task<List<Vouch>> GetVouchesOfUserAsync(ulong discordId)
        {
            using var context = new DragonContext(_options);
            var vouches = await context.Vouches.Include(v => v.ClanMember).Where(x => x.ClanMember.DiscordId == discordId).ToListAsync().ConfigureAwait(false);
            return vouches;
        }
    }
}
