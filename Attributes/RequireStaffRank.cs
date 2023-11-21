using DragonBot.Enums;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.SlashCommands;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DragonBot.Attributes
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
    public class RequireStaffRank : SlashCheckBaseAttribute
    {
        private StaffRanks[] _ranks { get; }

        public RequireStaffRank(params StaffRanks[] Ranks)
        {
            _ranks = Ranks;
        }

        public override Task<bool> ExecuteChecksAsync(InteractionContext ctx)
        {
            if (ctx.Guild == null || ctx.Member == null)
            {
                return Task.FromResult(false);
            }

            bool foundRank = false;

            foreach (var item in ctx.Member.Roles)
            {
                switch (item.Id)
                {
                    case RankIds.devId:
                        if (_ranks.Contains(StaffRanks.Dev))
                        {
                            foundRank = true;
                        }
                        break;
                    case RankIds.ownerId:
                        if (_ranks.Contains(StaffRanks.Dev))
                        {
                            foundRank = true;
                        }
                        break;
                    case RankIds.adminId:
                        if (_ranks.Contains(StaffRanks.Dev))
                        {
                            foundRank = true;
                        }
                        break;
                    case RankIds.moderatorId:
                        if (_ranks.Contains(StaffRanks.Dev))
                        {
                            foundRank = true;
                        }
                        break;
                }
            }

            return Task.FromResult(foundRank);
        }

        private struct RankIds
        {
            public RankIds()
            {
            }

            public const long devId = 1139575841228070972;
            public const long ownerId = 1132893402204213351;
            public const long adminId = 1133071740797472808;
            public const long moderatorId = 1174393813968629822;

        }
    }
}
