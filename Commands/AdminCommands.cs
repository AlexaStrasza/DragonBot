using Azure;
using DragonBot.Attributes;
using DragonBot.Context;
using DragonBot.Enums;
using DragonBot.Helpers;
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
    //[SlashRequirePermissions(Permissions.Administrator)]
    [RequireStaffRank(StaffRanks.Dev, StaffRanks.Owner, StaffRanks.Admin, StaffRanks.Moderator)]
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
            await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder()
            {
                IsEphemeral = true,
            }.WithContent("Please provide a list of users using mentions (@). To cancel type \"Cancel\"."));

            var interactivity = ctx.Client.GetInteractivity();
            var messageResult = await interactivity.WaitForMessageAsync(
                x => x.ChannelId == ctx.Channel.Id &&
                x.Author.Id == ctx.User.Id);
            string embedDescription = "";

            if (!messageResult.TimedOut)
            {
                if (messageResult.Result.Content.Contains("Cancel") || messageResult.Result.Content.Contains("cancel"))
                {
                    await ctx.EditResponseAsync(new DiscordWebhookBuilder(new DiscordMessageBuilder().AddEmbed(new DiscordEmbedBuilder()
                    {
                        Title = $"Cancelling.",
                        Description = $"No points awarded."
                    })));
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
                    await ctx.Channel.SendMessageAsync(new DiscordMessageBuilder().AddEmbed(embed));
                    await ctx.Interaction.DeleteOriginalResponseAsync();
                    await UpdateHighScoresList(ctx.Guild);
                }
                else
                {
                    await ctx.EditResponseAsync(new DiscordWebhookBuilder(new DiscordMessageBuilder().AddEmbed(new DiscordEmbedBuilder()
                    {
                        Title = $"No users mentioned.",
                        Description = $"Cancelling."
                    }))).ConfigureAwait(false);
                }

                await messageResult.Result.DeleteAsync().ConfigureAwait(false);
            }
            else
            {
                await ctx.EditResponseAsync(new DiscordWebhookBuilder(new DiscordMessageBuilder().AddEmbed(new DiscordEmbedBuilder()
                {
                    Title = $"Timed out.",
                    Description = $"Cancelling."
                })));
            }
        }

        [SlashCommand("batchvouch", "Give a member points for pvp or pvm trips")]
        public async Task VouchPoints(InteractionContext ctx,
            [Option("triptype", "Pvm or Pk Trip")] TripType tripType,
            [Option("host-or-attendee", "If the user hosted or joined as an attendee")] TripAttendeeType tripAttendee,
            [Option("description", "Point award description")] string description)
        {
            await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder()
            {
                IsEphemeral = true,
            }.WithContent("Please provide a list of users using mentions (@). To cancel type \"Cancel\"."));

            var interactivity = ctx.Client.GetInteractivity();
            var messageResult = await interactivity.WaitForMessageAsync(
                x => x.ChannelId == ctx.Channel.Id &&
                x.Author.Id == ctx.User.Id);

            int pointsToGive = 0;
            string embedDescription = "";

            int weeklyLimit = 0;
            switch (tripType)
            {
                case TripType.PvmTrip:
                    switch (tripAttendee)
                    {
                        case TripAttendeeType.Attendee:
                            pointsToGive = _configRanks.WeeklyAttendeeRewardAmountPvM;
                            embedDescription = description + " - (Attendee)";
                            break;
                        case TripAttendeeType.Host:
                            pointsToGive = _configRanks.WeeklyHostRewardAmountPvM;
                            embedDescription = description + " - (Hosting)";
                            break;
                    }

                    weeklyLimit = _configRanks.WeeklyPointLimitPvM;
                    break;
                case TripType.PkingTrip:
                    switch (tripAttendee)
                    {
                        case TripAttendeeType.Attendee:
                            pointsToGive = _configRanks.WeeklyAttendeeRewardAmountPvP;
                            embedDescription = description + " - (Attendee)";
                            break;
                        case TripAttendeeType.Host:
                            pointsToGive = _configRanks.WeeklyHostRewardAmountPvP;
                            embedDescription = description + " - (Hosting)";
                            break;
                    }
                    weeklyLimit = _configRanks.WeeklyPointLimitPvP;
                    break;
                case TripType.Raiding:
                    switch (tripAttendee)
                    {
                        case TripAttendeeType.Attendee:
                            pointsToGive = _configRanks.WeeklyAttendeeRewardAmountRaids;
                            embedDescription = description + " - (Attendee)";
                            break;
                        case TripAttendeeType.Host:
                            pointsToGive = _configRanks.WeeklyHostRewardAmountRaids;
                            embedDescription = description + " - (Hosting)";
                            break;
                    }
                    weeklyLimit = _configRanks.WeeklyPointLimitRaids;
                    break;
                case TripType.Misc:
                    switch (tripAttendee)
                    {
                        case TripAttendeeType.Attendee:
                            pointsToGive = _configRanks.WeeklyAttendeeRewardAmountMisc;
                            embedDescription = description + " - (Attendee)";
                            break;
                        case TripAttendeeType.Host:
                            pointsToGive = _configRanks.WeeklyHostRewardAmountMisc;
                            embedDescription = description + " - (Hosting)";
                            break;
                    }
                    weeklyLimit = _configRanks.WeeklyPointLimitMisc;
                    break;
            }

            if (!messageResult.TimedOut)
            {
                if (messageResult.Result.Content.Contains("Cancel") || messageResult.Result.Content.Contains("cancel"))
                {
                    await ctx.EditResponseAsync(new DiscordWebhookBuilder(new DiscordMessageBuilder().AddEmbed(new DiscordEmbedBuilder()
                    {
                        Title = $"Cancelling.",
                        Description = $"No points awarded."
                    })));
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
                        PointDifferenceWeeklyViewModel difference = await _pointDistributionService.GrantWeeklyPoints(user.Id, pointsToGive, weeklyLimit, tripType, embedDescription);

                        if (!difference.errored)
                        {
                            await UpdateRankAsync(ctx, difference.differenceViewModel, user.Id);

                            string change = $"+{difference.differenceViewModel.pointsChange}";

                            if (difference.pointsThisWeek >= weeklyLimit)
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

                    await ctx.Channel.SendMessageAsync(new DiscordMessageBuilder().AddEmbed(embed)).ConfigureAwait(false);
                    await ctx.Interaction.DeleteOriginalResponseAsync().ConfigureAwait(false);
                    await UpdateHighScoresList(ctx.Guild);
                }
                else
                {
                    await ctx.EditResponseAsync(new DiscordWebhookBuilder(new DiscordMessageBuilder().AddEmbed(new DiscordEmbedBuilder()
                    {
                        Title = $"No users mentioned.",
                        Description = $"Cancelling."
                    }))).ConfigureAwait(false);
                }

                await messageResult.Result.DeleteAsync().ConfigureAwait(false);
            }
            else
            {
                await ctx.EditResponseAsync(new DiscordWebhookBuilder(new DiscordMessageBuilder().AddEmbed(new DiscordEmbedBuilder()
                {
                    Title = $"Timed out.",
                    Description = $"Cancelling."
                }))).ConfigureAwait(false);
            }
        }

        [SlashCommand("editpoints", "Changes clanpoints for given member")]
        public async Task AddPoints(InteractionContext ctx,
            [Option("Member", "The Member to edit points of")] DiscordUser user,
            [Option("Points", "Amount of points to award a Clan Member")] long points,
            [Option("Description", "Point award description")] string description)
        {
            await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder().WithContent("Applying point changes..."));

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

            await ctx.EditResponseAsync(new DiscordWebhookBuilder(new DiscordMessageBuilder().AddEmbed(embed)));
            await UpdateHighScoresList(ctx.Guild);
        }

        [SlashCommand("vouch", "Give a member points for pvp or pvm trips")]
        public async Task VouchPoints(InteractionContext ctx,
            [Option("member", "The Member to edit points of")] DiscordUser user,
            [Option("triptype", "Pvm or Pk Trip")] TripType tripType,
            [Option("host-or-attendee", "If the user hosted or joined as an attendee")] TripAttendeeType tripAttendee,
            [Option("description", "Point award description")] string description)
        {
            await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder().WithContent("Applying point changes...")).ConfigureAwait(false);

            await _memberService.GetOrCreateMemberAsync(user.Id).ConfigureAwait(false);
            int pointsToGive = 0;
            string infoDescription = "";

            int weeklyLimit = 0;
            switch (tripType)
            {
                case TripType.PvmTrip:
                    switch (tripAttendee)
                    {
                        case TripAttendeeType.Attendee:
                            pointsToGive = _configRanks.WeeklyAttendeeRewardAmountPvM;
                            infoDescription = description + " - (Attendee)";
                            break;
                        case TripAttendeeType.Host:
                            pointsToGive = _configRanks.WeeklyHostRewardAmountPvM;
                            infoDescription = description + " - (Hosting)";
                            break;
                    }
                    weeklyLimit = _configRanks.WeeklyPointLimitPvM;
                    break;
                case TripType.PkingTrip:
                    switch (tripAttendee)
                    {
                        case TripAttendeeType.Attendee:
                            pointsToGive = _configRanks.WeeklyAttendeeRewardAmountPvP;
                            infoDescription = description + " - (Attendee)";
                            break;
                        case TripAttendeeType.Host:
                            pointsToGive = _configRanks.WeeklyHostRewardAmountPvP;
                            infoDescription = description + " - (Hosting)";
                            break;
                    }
                    weeklyLimit = _configRanks.WeeklyPointLimitPvP;
                    break;
                case TripType.Raiding:
                    switch (tripAttendee)
                    {
                        case TripAttendeeType.Attendee:
                            pointsToGive = _configRanks.WeeklyAttendeeRewardAmountRaids;
                            infoDescription = description + " - (Attendee)";
                            break;
                        case TripAttendeeType.Host:
                            pointsToGive = _configRanks.WeeklyHostRewardAmountRaids;
                            infoDescription = description + " - (Hosting)";
                            break;
                    }
                    weeklyLimit = _configRanks.WeeklyPointLimitRaids;
                    break;
                case TripType.Misc:
                    switch (tripAttendee)
                    {
                        case TripAttendeeType.Attendee:
                            pointsToGive = _configRanks.WeeklyAttendeeRewardAmountMisc;
                            infoDescription = description + " - (Attendee)";
                            break;
                        case TripAttendeeType.Host:
                            pointsToGive = _configRanks.WeeklyHostRewardAmountMisc;
                            infoDescription = description + " - (Hosting)";
                            break;
                    }
                    weeklyLimit = _configRanks.WeeklyPointLimitMisc;
                    break;
            }
                            

            PointDifferenceWeeklyViewModel difference = await _pointDistributionService.GrantWeeklyPoints(user.Id, pointsToGive, weeklyLimit, tripType, infoDescription).ConfigureAwait(false);

            if (!difference.errored)
            {
                await UpdateRankAsync(ctx, difference.differenceViewModel, user.Id).ConfigureAwait(false);

                string change = $"+{difference.differenceViewModel.pointsChange}";

                if (difference.pointsThisWeek >= weeklyLimit)
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

                await ctx.EditResponseAsync(new DiscordWebhookBuilder(new DiscordMessageBuilder().AddEmbed(embed))).ConfigureAwait(false);

                await UpdateHighScoresList(ctx.Guild);
            }
            else
            {
                await ctx.EditResponseAsync(new DiscordWebhookBuilder(new DiscordMessageBuilder().AddEmbed(new DiscordEmbedBuilder()
                {
                    Title = "An error has occurred.",
                    Description = "Please contact the bot developer."
                }))).ConfigureAwait(false);
            }
        }

        //[SlashCommand("editsplits", "Add or remove to someones total split amount")]
        //public async Task AddSplit(InteractionContext ctx,
        //    [Option("member", "The Member to edit splits of")] DiscordUser user,
        //    [Option("split", "The size of the split")] ulong splitValue,
        //    [Option("description", "Split description")] string description)
        //{
        //    await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder().WithContent("Applying split changes..."));

        //    await _memberService.GetOrCreateMemberAsync(user.Id).ConfigureAwait(false);
        //    SplitDifferenceViewModel difference = await _pointDistributionService.ChangeSplitAsync(user.Id, splitValue).ConfigureAwait(false);

        //    //await UpdateRankAsync(ctx, difference, user.Id);

        //    string change = "";
        //    if (difference.pointsChange > 0)
        //    {
        //        change += "+" + difference.pointsChange;
        //    }
        //    else change += difference.pointsChange;

        //    DiscordEmbedBuilder embed = new DiscordEmbedBuilder()
        //    {
        //        Title = $"Changes applied to:"
        //    };

        //    embed.AddField("​​​User", user.Mention, true);
        //    embed.AddField("Split amount Δ", change, true);
        //    embed.AddField("Total split", difference.pointsNew.ToString(), true);

        //    embed.AddField("Description:", description);

        //    await ctx.EditResponseAsync(new DiscordWebhookBuilder(new DiscordMessageBuilder().AddEmbed(embed)));
        //}

        public async Task UpdateRankAsync(InteractionContext ctx, PointDifferenceViewModel difference, ulong userId)
        {
            DiscordMember member = await ctx.Guild.GetMemberAsync(userId);

            var currentRankIds = member.Roles.Select(role => role.Id);
            var newRank = Helper.FindAppropriateRank(difference.pointsNew);

            var rankRoleIds = _configRanks.Ranks.Select(rank => rank.RoleId);

            // Remove any incorrect roles that the user shouldn't have
            var rolesToRemove = currentRankIds.Intersect(rankRoleIds).Except(new[] { newRank.RoleId });
            foreach (ulong roleId in rolesToRemove)
            {
                await member.RevokeRoleAsync(ctx.Guild.GetRole(roleId)).ConfigureAwait(false);
            }

            // Add the correct rank role if it's missing
            if (!currentRankIds.Contains(newRank.RoleId))
            {
                DiscordRole newRole = ctx.Guild.GetRole(newRank.RoleId);
                await member.GrantRoleAsync(newRole);
                var channel = ctx.Guild.GetChannel(1144424054984556595);

                DiscordButtonComponent buttonConfirm = new DiscordButtonComponent(ButtonStyle.Success, "confirmRankup", "Confirm Rank Updated Ingame");
                DiscordMessageBuilder builder = new DiscordMessageBuilder();
                DiscordEmbedBuilder embed = new DiscordEmbedBuilder()
                {
                    Title = "Member Rank Changed!"
                };

                embed.Description = $"Member {member.Mention} Rank changed to {newRole.Mention}";

                builder.AddEmbed(embed);
                builder.AddComponents(buttonConfirm);
                await channel.SendMessageAsync(builder).ConfigureAwait(false);
            }
        }

        private async Task UpdateHighScoresList(DiscordGuild guild)
        {
            var channel = guild.GetChannel(1159872652928884756);

            IReadOnlyList<DiscordMessage> messages = await channel.GetMessagesAsync(1);

            List<ClanMember> members = await _memberService.GetAllMembers();

            DiscordEmbedBuilder embed = new DiscordEmbedBuilder();

            string rankList = "";
            string userList = "";
            string pointList = "";

            int startAt = 0;

            List<ClanMember> subList = members.Take(25).ToList();

            foreach (var member in subList)
            {
                startAt++;
                rankList += $"{startAt}\n";
                //userList += $"{ctx.Client.GetUserAsync(member.DiscordId).Result.Mention}\n";
                userList += $"<@!{member.DiscordId}>\n";
                pointList += $"{member.ClanPoints}\n";

            }
            embed.AddField("​​​Rank", rankList, true);
            embed.AddField("Clan Member", userList, true);
            embed.AddField("Points", pointList, true);

            string dateString = $"<t:{Helper.ConvertToUnixTimestamp(DateTime.Now)}:D>";

            if (messages.Count > 0)
            {
                await messages[0].ModifyAsync(x =>
                {
                    x.Embed = embed;
                    x.Content = $"Last updated on: {dateString}";
                });
            }
            else
            {
                await channel.SendMessageAsync(new DiscordMessageBuilder().WithContent($"Last updated on: {dateString}").AddEmbed(embed));
            }
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
