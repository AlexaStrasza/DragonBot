using DragonBot.Context;
using DragonBot.Helpers;
using DragonBot.Models;
using DragonBot.ViewModel;
using DSharpPlus.Entities;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using static DragonBot.Helpers.Enums;

namespace DragonBot
{
    public interface IPointDistributionService
    {
        Task<int> GetPointsAsync(ulong discordId);
        Task<PointDifferenceViewModel> ChangePointsAsync(ulong discordId, int amount, string description);
        Task<PointDifferenceWeeklyViewModel> GrantWeeklyPoints(ulong discordId, int amount, int weeklyLimit, TripType tripType, string description);
    }

    public class PointDistributionService : IPointDistributionService
    {
        private readonly DbContextOptions<DragonContext> _options;
        private readonly IClanMemberService _memberService;
        public PointDistributionService(DbContextOptions<DragonContext> options, IClanMemberService memberService)
        {
            _options = options;
            _memberService = memberService;
        }

        public async Task<int> GetPointsAsync(ulong discordId)
        {
            using var context = new DragonContext(_options);

            ClanMember member = await _memberService.GetOrCreateMemberAsync(discordId);

            return member.ClanPoints;
        }

        public async Task<PointDifferenceViewModel> ChangePointsAsync(ulong discordId, int amount, string description)
        {
            try
            {

                using var context = new DragonContext(_options);
                ClanMember member = await _memberService.GetOrCreateMemberAsync(discordId).ConfigureAwait(false);

                int oldValue = member.ClanPoints;

                member.ClanPoints += amount;

                // Prevent Negatives
                if (member.ClanPoints < 0)
                {
                    member.ClanPoints = 0;
                }

                int newValue = member.ClanPoints;
                int change = newValue - oldValue;

                member.Vouches.Add(new Vouch()
                {
                    PointValue = change,
                    VouchDescription = description
                });

                //context.Update(member);
                context.ClanMembers.Update(member);

                await context.SaveChangesAsync().ConfigureAwait(false);

                return new PointDifferenceViewModel()
                {
                    pointsOld = oldValue,
                    pointsNew = newValue,
                    pointsChange = change
                };
            }
            catch (Exception ex)
            {
                throw new Exception(ex.ToString());
            }
        }

        public async Task<PointDifferenceWeeklyViewModel> GrantWeeklyPoints(ulong discordId, int amount, int weeklyLimit, TripType tripType, string description)
        {
            try
            {
                using var context = new DragonContext(_options);
                ClanMember member = await _memberService.GetOrCreateMemberAsync(discordId).ConfigureAwait(false);

                switch (tripType)
                {
                    case TripType.PvmTrip:
                        int maxPointsAllowedPvM = 0;
                        if (Helper.IsSameWeek(member.LastUpdatePvM, DateTime.Now))
                        {
                            if (member.WeekPointsPvM >= weeklyLimit) maxPointsAllowedPvM = 0;
                            else if (member.WeekPointsPvM + amount > weeklyLimit)
                            {
                                // Gives remainder of allowed points
                                maxPointsAllowedPvM = 100 - member.WeekPointsPvM;
                            }
                            else maxPointsAllowedPvM = weeklyLimit;
                        }
                        else
                        {
                            member.WeekPointsPvM = 0;
                            maxPointsAllowedPvM = weeklyLimit;
                        }

                        if (maxPointsAllowedPvM == 0)
                        {
                            // at or over limit do not grant points, give error response
                            return new PointDifferenceWeeklyViewModel()
                            {
                                errored = false,
                                note = "Weekly point limit for PvM reached.",
                                pointsThisWeek = member.WeekPointsPvM,
                                differenceViewModel = new PointDifferenceViewModel()
                                {
                                    pointsChange = 0,
                                    pointsOld = member.ClanPoints,
                                    pointsNew = member.ClanPoints
                                }
                            };
                        }
                        else
                        {
                            int pointsToGive = 0;
                            // not at limit yet, grant as many as limit allows
                            if (amount > maxPointsAllowedPvM)
                                pointsToGive = maxPointsAllowedPvM;
                            else
                                pointsToGive = amount;

                            member.WeekPointsPvM += pointsToGive;
                            member.LastUpdatePvM = DateTime.Now;
                            context.ClanMembers.Update(member);
                            await context.SaveChangesAsync().ConfigureAwait(false);

                            PointDifferenceViewModel difference = await ChangePointsAsync(discordId, pointsToGive, description);

                            return new PointDifferenceWeeklyViewModel()
                            {
                                errored = false,
                                note = "success",
                                pointsThisWeek = member.WeekPointsPvM,
                                differenceViewModel = difference
                            };
                        }
                    case TripType.PkingTrip:
                        int maxPointsAllowedPvP = 0;
                        if (Helper.IsSameWeek(member.LastUpdatePvP, DateTime.Now))
                        {
                            if (member.WeekPointsPvP >= weeklyLimit) maxPointsAllowedPvP = 0;
                            else if (member.WeekPointsPvP + amount > weeklyLimit)
                            {
                                // Gives remainder of allowed points
                                maxPointsAllowedPvP = 100 - member.WeekPointsPvP;
                            }
                            else maxPointsAllowedPvP = weeklyLimit;
                        }
                        else
                        {
                            member.WeekPointsPvM = 0;
                            maxPointsAllowedPvM = weeklyLimit;
                        }

                        if (maxPointsAllowedPvP == 0)
                        {
                            // at or over limit do not grant points, give error response
                            return new PointDifferenceWeeklyViewModel()
                            {
                                errored = false,
                                note = "Weekly point limit for PvP reached.",
                                pointsThisWeek = member.WeekPointsPvP,
                                differenceViewModel = new PointDifferenceViewModel()
                                {
                                    pointsChange = 0,
                                    pointsOld = member.ClanPoints,
                                    pointsNew = member.ClanPoints
                                }
                            };
                        }
                        else
                        {
                            int pointsToGive = 0;
                            // not at limit yet, grant as many as limit allows
                            if (amount > maxPointsAllowedPvP)
                                pointsToGive = maxPointsAllowedPvP;
                            else
                                pointsToGive = amount;

                            member.WeekPointsPvP += pointsToGive;
                            member.LastUpdatePvP = DateTime.Now;
                            context.ClanMembers.Update(member);
                            await context.SaveChangesAsync().ConfigureAwait(false);

                            PointDifferenceViewModel difference = await ChangePointsAsync(discordId, pointsToGive, description);

                            return new PointDifferenceWeeklyViewModel()
                            {
                                errored = false,
                                note = "success",
                                pointsThisWeek = member.WeekPointsPvP,
                                differenceViewModel = difference
                            };
                        }
                }

                return new PointDifferenceWeeklyViewModel()
                {
                    errored = true,
                    note = "An error has occurred.",
                    pointsThisWeek = member.WeekPointsPvM,
                    differenceViewModel = new PointDifferenceViewModel()
                    {
                        pointsChange = 0,
                        pointsOld = member.ClanPoints,
                        pointsNew = member.ClanPoints
                    }
                };
            }
            catch (Exception ex)
            {
                throw new Exception(ex.ToString());
            }
        }
    }
}