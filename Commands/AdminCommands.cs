﻿using DragonBot.Attributes;
using DragonBot.Context;
using DragonBot.Models;
using DragonBot.Services;
using DragonBot.ViewModel;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.CommandsNext.Converters;
using DSharpPlus.Entities;
using DSharpPlus.Interactivity;
using DSharpPlus.Interactivity.Enums;
using DSharpPlus.Interactivity.Extensions;
using DSharpPlus.SlashCommands;
using DSharpPlus.SlashCommands.Attributes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query.Internal;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Channels;
using static DragonBot.Helpers.Enums;

namespace DragonBot.Commands
{
    [SlashRequirePermissions(Permissions.Administrator)]
    public class AdminCommands : ApplicationCommandModule
    {
        private readonly IClanMemberService _memberService;
        private readonly IPointDistributionService _pointDistributionService;
        private readonly IVouchService _vouchService;
        private readonly ConfigRanks _configRanks;
        public AdminCommands(IClanMemberService memberService, IPointDistributionService pointsService, IVouchService vouchService)
        {
            _memberService = memberService;
            _pointDistributionService = pointsService;
            _vouchService = vouchService;

            var json = string.Empty;

            using (FileStream fs = File.OpenRead("configRanks.json"))
            using (StreamReader sr = new StreamReader(fs, new UTF8Encoding(false)))
                json = sr.ReadToEnd();

            _configRanks = JsonConvert.DeserializeObject<ConfigRanks>(json);
        }

        [SlashCommand("batcheditpoints", "Changes clanpoints for given members")]
        public async Task BatchAddPoints(InteractionContext ctx,
            [Option("Points", "Amount of points to give. Can be negative")] long points,
            [Option("Description", "Point award description")] string description)
        {
            await ctx.CreateResponseAsync(new DiscordInteractionResponseBuilder()
            {
                IsEphemeral = true,
            }.WithContent("Please provide a list of users using mentions (@). To cancel type \"Cancel\"."));

            var interactivity = ctx.Client.GetInteractivity();
            var messageResult = await interactivity.WaitForMessageAsync(
                x => x.ChannelId == ctx.Channel.Id &&
                x.Author.Id == ctx.User.Id);
            string embedDescription = "";

            if (messageResult.Result.Content.Contains("Cancel") || messageResult.Result.Content.Contains("cancel"))
            {
                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder(new DiscordMessageBuilder().AddEmbed(new DiscordEmbedBuilder()
                {
                    Title = $"Cancelling.",
                    Description = $"No points awarded."
                }))
                { IsEphemeral = true });
            }
            else if (messageResult.Result.MentionedUsers.Count > 0)
            {
                DiscordEmbedBuilder embed = new DiscordEmbedBuilder()
                {
                    Title = $"Changes applied to:",
                };

                string userList = "";
                string pointList = "";
                string newPointsList = "";

                foreach (var user in messageResult.Result.MentionedUsers)
                {
                    await _memberService.GetOrCreateMemberAsync(user.Id).ConfigureAwait(false);
                    PointDifferenceViewModel difference = await _pointDistributionService.ChangePointsAsync(user.Id, (int)points, description).ConfigureAwait(false);

                    string change = "";
                    if (difference.pointsChange > 0)
                    {
                        change += "+" + difference.pointsChange;
                    }
                    else change += difference.pointsChange;

                    embedDescription += $"{user.Mention} **{change}** (new total: **{difference.pointsNew}**)\n";

                    await UpdateRankAsync(ctx, difference, user.Id);

                    userList += $"{user.Mention}\n";
                    pointList += $"{change}\n";
                    newPointsList += $"{difference.pointsNew}\n";
                }

                embed.AddField("​​​User", userList, true);
                embed.AddField("Points Δ", pointList, true);
                embed.AddField("New Point Total", newPointsList, true);

                if (!description.IsNullOrEmpty())
                {
                    embed.AddField("Description:", description);
                }
                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder(new DiscordMessageBuilder().AddEmbed(embed)));
            }
            else
            {
                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder(new DiscordMessageBuilder().AddEmbed(new DiscordEmbedBuilder()
                {
                    Title = $"No users mentioned.",
                    Description = $"Cancelling."
                }))
                { IsEphemeral = true });
            }

            await messageResult.Result.DeleteAsync();
            await ctx.Interaction.DeleteOriginalResponseAsync();
        }

        [SlashCommand("batchvouch", "Give a member points for pvp or pvm trips")]
        public async Task VouchPoints(InteractionContext ctx,
            [Option("triptype", "Pvm or Pk Trip")] TripType tripType,
            [Option("host-or-attendee", "If the user hosted or joined as an attendee")] TripAttendeeType tripAttendee,
            [Option("description", "Point award description")] string description)
        {
            await ctx.CreateResponseAsync(new DiscordInteractionResponseBuilder()
            {
                IsEphemeral = true,
            }.WithContent("Please provide a list of users using mentions (@). To cancel type \"Cancel\"."));

            var interactivity = ctx.Client.GetInteractivity();
            var messageResult = await interactivity.WaitForMessageAsync(
                x => x.ChannelId == ctx.Channel.Id &&
                x.Author.Id == ctx.User.Id);

            int pointsToGive = 0;
            string embedDescription = "";

            switch (tripAttendee)
            {
                case TripAttendeeType.Attendee:
                    pointsToGive = _configRanks.WeeklyAttendeeRewardAmount;
                    embedDescription = description + " - (Attendee)";
                    break;
                case TripAttendeeType.Host:
                    pointsToGive = _configRanks.WeeklyHostRewardAmount;
                    embedDescription = description + " - (Hosting)";
                    break;
            }

            if (messageResult.Result.Content.Contains("Cancel") || messageResult.Result.Content.Contains("cancel"))
            {
                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder(new DiscordMessageBuilder().AddEmbed(new DiscordEmbedBuilder()
                {
                    Title = $"Cancelling.",
                    Description = $"No points awarded."
                }))
                { IsEphemeral = true });
            }
            else if (messageResult.Result.MentionedUsers.Count > 0)
            {
                DiscordEmbedBuilder embed = new DiscordEmbedBuilder()
                {
                    Title = $"Changes applied to:",
                };

                string userList = "";
                string pointList = "";
                string newPointsList = "";

                foreach (var user in messageResult.Result.MentionedUsers)
                {
                    PointDifferenceWeeklyViewModel difference = await _pointDistributionService.GrantWeeklyPoints(user.Id, pointsToGive, _configRanks.WeeklyPointLimit, tripType, embedDescription);

                    if (!difference.errored)
                    {
                        await UpdateRankAsync(ctx, difference.differenceViewModel, user.Id);

                        string change = $"+{difference.differenceViewModel.pointsChange}";

                        if (difference.pointsThisWeek >= _configRanks.WeeklyPointLimit)
                        {
                            change += $" (point limit reached)";
                        }

                        userList += $"{user.Mention}\n";
                        pointList += $"{change}\n";
                        newPointsList += $"{difference.differenceViewModel.pointsNew}\n";
                    }
                }

                embed.AddField("​​​User", userList, true);
                embed.AddField("Points Δ", pointList, true);
                embed.AddField("New Point Total", newPointsList, true);
                embed.AddField("Description:", embedDescription);

                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder(new DiscordMessageBuilder().AddEmbed(embed)));
            }
            else
            {
                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder(new DiscordMessageBuilder().AddEmbed(new DiscordEmbedBuilder()
                {
                    Title = $"No users mentioned.",
                    Description = $"Cancelling."
                }))
                { IsEphemeral = true });
            }

            await messageResult.Result.DeleteAsync();
            await ctx.Interaction.DeleteOriginalResponseAsync();
        }

        [SlashCommand("editpoints", "Changes clanpoints for given member")]
        public async Task AddPoints(InteractionContext ctx,
            [Option("Member", "The Member to edit points of")] DiscordUser user,
            [Option("Points", "Amount of points to award a Clan Member")] long points,
            [Option("Description", "Point award description")] string description)
        {

            await _memberService.GetOrCreateMemberAsync(user.Id).ConfigureAwait(false);
            PointDifferenceViewModel difference = await _pointDistributionService.ChangePointsAsync(user.Id, (int)points, description).ConfigureAwait(false);

            await UpdateRankAsync(ctx, difference, user.Id);

            string change = "";
            if (difference.pointsChange > 0)
            {
                change += "+" + difference.pointsChange;
            }
            else change += difference.pointsChange;

            DiscordEmbedBuilder embed = new DiscordEmbedBuilder()
            {
                Title = $"Changes applied to:"
            };

            embed.AddField("​​​User", user.Mention, true);
            embed.AddField("Points Δ", change, true);
            embed.AddField("New Point Total", difference.pointsNew.ToString(), true);

            embed.AddField("Description:", description);

            await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder(new DiscordMessageBuilder().AddEmbed(embed)));
        }

        [SlashCommand("vouch", "Give a member points for pvp or pvm trips")]
        public async Task VouchPoints(InteractionContext ctx,
            [Option("member", "The Member to edit points of")] DiscordUser user,
            [Option("triptype", "Pvm or Pk Trip")] TripType tripType,
            [Option("host-or-attendee", "If the user hosted or joined as an attendee")] TripAttendeeType tripAttendee,
            [Option("description", "Point award description")] string description)
        {
            await _memberService.GetOrCreateMemberAsync(user.Id).ConfigureAwait(false);
            int pointsToGive = 0;
            string infoDescription = "";


            switch (tripAttendee)
            {
                case TripAttendeeType.Attendee:
                    pointsToGive = _configRanks.WeeklyAttendeeRewardAmount;
                    infoDescription = description + " - (Attendee)";
                    break;
                case TripAttendeeType.Host:
                    pointsToGive = _configRanks.WeeklyHostRewardAmount;
                    infoDescription = description + " - (Hosting)";
                    break;
            }

            PointDifferenceWeeklyViewModel difference = await _pointDistributionService.GrantWeeklyPoints(user.Id, pointsToGive, _configRanks.WeeklyPointLimit, tripType, infoDescription);
            //PointDifferenceViewModel difference = await _pointDistributionService.ChangePointsAsync(user.Id, (int)points, description).ConfigureAwait(false);

            if (!difference.errored)
            {
                await UpdateRankAsync(ctx, difference.differenceViewModel, user.Id);

                string change = $"+{difference.differenceViewModel.pointsChange}";

                if (difference.pointsThisWeek >= _configRanks.WeeklyPointLimit)
                {
                    change += $" (point limit reached)";
                }


                DiscordEmbedBuilder embed = new DiscordEmbedBuilder()
                {
                    Title = $"Changes applied to:"
                };

                embed.AddField("​​​User", user.Mention, true);
                embed.AddField("Points Δ", change, true);
                embed.AddField("New Point Total", difference.differenceViewModel.pointsNew.ToString(), true);


                embed.AddField("Description:", infoDescription);

                await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder(new DiscordMessageBuilder().AddEmbed(embed)));
            }
            else
            {
                await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder(new DiscordMessageBuilder().AddEmbed(new DiscordEmbedBuilder()
                    {
                        Title = "An error has occurred.",
                        Description = "Please contact the bot developer."
                    })));
            }
        }

        [SlashCommand("history", "Show a users Point history")]
        public async Task ShowHistory(InteractionContext ctx,
        [Option("Members", "Displays point history of given member")] DiscordUser user)
        {
            try
            {
                var interactivity = ctx.Client.GetInteractivity();


                List<Page> pages = new List<Page>();

                List<Vouch> vouches = await _vouchService.GetVouchesOfUserAsync(user.Id);

                int totalLines = vouches.Count;

                int totalPages = 0;

                if (totalLines == 0)
                {
                    await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder(new DiscordMessageBuilder().AddEmbed(new DiscordEmbedBuilder()
                    {
                        Title = $"No History.",
                        Description = $"There was no point history found for {user.Mention}"
                    })));
                }
                else
                {
                    if (totalLines <= 10 && totalLines > 0)
                    {
                        totalPages = 1;
                    }
                    else
                    {
                        totalPages = (totalLines / 10) + 1;
                    }

                    for (int page = 0; page < totalPages; page++)
                    {
                        DiscordEmbedBuilder embed = new DiscordEmbedBuilder();

                        embed.AddField("​​​Point history of:", user.Mention, true);
                        embed.AddField("\u200B", "\u200B", true);
                        embed.AddField("\u200B", "\u200B", true);

                        string dateList = "";
                        string pointList = "";
                        string descriptionList = "";

                        int startAt = page * 10;

                        List<Vouch> subList = vouches.Skip(startAt).Take(10).ToList();

                        foreach (var vouch in subList)
                        {
                            startAt++;
                            dateList += $"<t:{(int)vouch.DateTime.Subtract(new DateTime(1970, 1, 1)).TotalSeconds}:D>\n";

                            if (vouch.PointValue > 0)
                                pointList += $"+{vouch.PointValue}\n";
                            else
                                pointList += $"{vouch.PointValue}\n";
                            if (vouch.VouchDescription.IsNullOrEmpty()) descriptionList += "\u200B\n";
                            else descriptionList += $"{vouch.VouchDescription}\n";

                        }
                        embed.AddField("​​​Date", dateList, true);
                        embed.AddField("Points Δ", pointList, true);
                        embed.AddField("Description", descriptionList, true);

                        embed.WithFooter($"Page {page + 1} of {totalPages}");

                        pages.Add(new Page("", embed));
                    }

                    await interactivity.SendPaginatedResponseAsync(ctx.Interaction, true, ctx.User, pages, /*buttons*/ null, PaginationBehaviour.Ignore, ButtonPaginationBehavior.DeleteMessage, asEditResponse: false);
                }

            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }

        public async Task UpdateRankAsync(InteractionContext ctx, PointDifferenceViewModel difference, ulong userId)
        {
            DiscordMember member = await ctx.Guild.GetMemberAsync(userId);

            var currentRankIds = member.Roles.Select(role => role.Id);
            var newRank = FindAppropriateRank(difference.pointsNew);

            var rankRoleIds = _configRanks.Ranks.Select(rank => rank.RoleId);

            // Remove any incorrect roles that the user shouldn't have
            var rolesToRemove = currentRankIds.Intersect(rankRoleIds).Except(new[] { newRank.RoleId });
            foreach (ulong roleId in rolesToRemove)
            {
                await member.RevokeRoleAsync(ctx.Guild.GetRole(roleId));
            }

            // Add the correct rank role if it's missing
            if (!currentRankIds.Contains(newRank.RoleId))
            {
                DiscordRole newRole = ctx.Guild.GetRole(newRank.RoleId);
                await member.GrantRoleAsync(newRole);
                var channel = ctx.Guild.GetChannel(1141378825532473428);

                DiscordButtonComponent buttonDelete = new DiscordButtonComponent(ButtonStyle.Success, "deleteMessage", "Confirm Rank Updated Ingame");
                DiscordMessageBuilder builder = new DiscordMessageBuilder();
                DiscordEmbedBuilder embed = new DiscordEmbedBuilder()
                {
                    Title = "Member Rank Changed!"
                };

                embed.Description = $"Member {member.Mention} Rank changed to {newRole.Mention}";

                builder.AddEmbed(embed);
                builder.AddComponents(buttonDelete);
                await channel.SendMessageAsync(builder);
            }
        }

        private Rank FindAppropriateRank(int userPoints)
        {
            Rank selectedRank = new Rank();

            foreach (var rank in _configRanks.Ranks)
            {
                if (userPoints >= rank.PointRequirement && (selectedRank.PointRequirement == 0 || rank.PointRequirement > selectedRank.PointRequirement))
                {
                    selectedRank = rank;
                }
            }

            return selectedRank;
        }

        //[SlashCommand("openmodal", "Opens a sample modal")]
        //public async Task OpenModalCommand(InteractionContext ctx)
        //{

        //    var button = new DiscordButtonComponent(ButtonStyle.Primary, "sampleButton", "Click Me!");

        //    var builder = new DiscordInteractionResponseBuilder()
        //        .WithContent("Click the button to open the modal!")
        //        .AddComponents(button);

        //    await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, builder);
        //}

        //[SlashCommand("nick", "change nickname test")]
        //public async Task SetNick(InteractionContext ctx, [Option("user", "user to edit")] DiscordUser member, [Option("string", "new nick")] string nickname)
        //{
        //    DiscordMember discordMember = await ctx.Guild.GetMemberAsync(member.Id);
        //    var oldNick = discordMember.Nickname;
        //    try
        //    {
        //        await discordMember.ModifyAsync(x => x.Nickname = nickname);
        //        var newNick = discordMember.Nickname;
        //        await ctx.Channel.SendMessageAsync($"{ctx.Member.Username} changed {oldNick}'s nickname to: {newNick}.").ConfigureAwait(false);
        //        newNick = "";
        //    }
        //    catch (Exception e)
        //    {
        //        await ctx.Channel.SendMessageAsync
        //            ($"An error occured: {e}").ConfigureAwait(false);
        //    }
        //}
    }
}
