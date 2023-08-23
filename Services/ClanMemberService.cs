using DragonBot.Context;
using DragonBot.Models;
using DSharpPlus.Entities;
using Microsoft.EntityFrameworkCore;

namespace DragonBot
{
    public interface IClanMemberService
    {
        Task<ClanMember> GetOrCreateMemberAsync(ulong discordId);
        Task<ClanMember> GetOrCreateMemberAsync(ulong discordId, string rsn);
        Task<List<ClanMember>> GetAllMembers();
        Task AddReferral(ulong discordId);
        Task ChangeRSN(ulong discordId, string rsn);
    }

    public class ClanMemberService : IClanMemberService
    {
        private readonly DbContextOptions<DragonContext> _options;

        public ClanMemberService(DbContextOptions<DragonContext> options)
        {
            _options = options;
        }

        public async Task<ClanMember> GetOrCreateMemberAsync(ulong discordId)
        {
            return await GetOrCreateMemberAsync(discordId, null);
        }

        public async Task<ClanMember> GetOrCreateMemberAsync(ulong discordId, string rsn)
        {
            using var context = new DragonContext(_options);

            var member = await context.ClanMembers.FirstOrDefaultAsync(x => x.DiscordId == discordId).ConfigureAwait(false);

            if (member != null)
            {
                return member;
            }

            member = new ClanMember
            {
                DiscordId = discordId,
                Vouches = new List<Vouch>(),
                RSN = rsn
            };

            //context.Add(member);

            context.ClanMembers.Add(member);

            await context.SaveChangesAsync().ConfigureAwait(false);

            return member;
        }

        public async Task<List<ClanMember>> GetAllMembers()
        {
            using var context = new DragonContext(_options);

            var members = await context.ClanMembers.Include(c => c.Vouches).ToListAsync().ConfigureAwait(false);
            return members.OrderByDescending(x => x.ClanPoints).ToList();
        }

        public async Task AddReferral(ulong discordId)
        {
            using var context = new DragonContext(_options);

            var member = await context.ClanMembers.FirstOrDefaultAsync(x => x.DiscordId == discordId).ConfigureAwait(false);
            member.Referrals++;

            context.ClanMembers.Update(member);

            await context.SaveChangesAsync().ConfigureAwait(false);
        }

        public async Task ChangeRSN(ulong discordId, string rsn)
        {
            using var context = new DragonContext(_options);

            var member = await context.ClanMembers.FirstOrDefaultAsync(x => x.DiscordId == discordId).ConfigureAwait(false);
            member.RSN = rsn;

            context.ClanMembers.Update(member);

            await context.SaveChangesAsync().ConfigureAwait(false);
        }
    }
}