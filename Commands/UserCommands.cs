using Azure;
using DragonBot.Helpers;
using DragonBot.Models;
using DragonBot.Services;
using DSharpPlus;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using DSharpPlus.Exceptions;
using DSharpPlus.Interactivity;
using DSharpPlus.Interactivity.Enums;
using DSharpPlus.Interactivity.EventHandling;
using DSharpPlus.Interactivity.Extensions;
using DSharpPlus.SlashCommands;
using DSharpPlus.SlashCommands.Attributes;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DragonBot.Commands
{
    public class UserCommands : ApplicationCommandModule
    {
        private readonly IClanMemberService _memberService;
        private readonly IVouchService _vouchService;
        private readonly int linesPerPage;
        public UserCommands(IClanMemberService context, IVouchService vouchService)
        {
            _memberService = context;
            _vouchService = vouchService;
            linesPerPage = 25;
        }

        [SlashCommand("highscores", "Display the clan points highscores.")]
        public async Task ShowHighscores(InteractionContext ctx)
        {
            //DiscordInteractionResponseBuilder responseBuilder = new DiscordInteractionResponseBuilder().WithContent("Retrieving Highscores...");
            //await ctx.CreateResponseAsync(responseBuilder);
            //await ctx.Interaction.DeleteOriginalResponseAsync();

            //PaginationButtons buttons = new PaginationButtons()
            //{
            //    Left = new DiscordButtonComponent(ButtonStyle.Secondary, "left", "", false, new DiscordComponentEmoji(DiscordEmoji.FromName(ctx.Client, ":whip:"))),
            //    Right = new DiscordButtonComponent(ButtonStyle.Secondary, "right", "", false, new DiscordComponentEmoji(DiscordEmoji.FromName(ctx.Client, ":point_right:"))),
            //    SkipLeft = new DiscordButtonComponent(ButtonStyle.Secondary, "skipleft", "", false, new DiscordComponentEmoji(DiscordEmoji.FromName(ctx.Client, ":point_up_2:"))),
            //    SkipRight = new DiscordButtonComponent(ButtonStyle.Secondary, "skipright", "", false, new DiscordComponentEmoji(DiscordEmoji.FromName(ctx.Client, ":point_down:"))),
            //    Stop = new DiscordButtonComponent(ButtonStyle.Danger, "stop", "", false, new DiscordComponentEmoji(DiscordEmoji.FromName(ctx.Client, ":punch:"))),
            //};



            List<Page> pages = new List<Page>();

            List<ClanMember> members = await _memberService.GetAllMembers();

            int totalLines = members.Count;

            if (totalLines == 0)
            {
                await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder(new DiscordMessageBuilder().AddEmbed(new DiscordEmbedBuilder()
                {
                    Title = $"No Highscores found.",
                    Description = $"There are no registered users on the highscores list."
                }))
                { IsEphemeral = true });
            }
            else
            {
                int totalPages = totalLines / linesPerPage;
                int remainder = totalLines % linesPerPage;

                if (remainder > 0)
                {
                    totalPages++;
                }

                for (int page = 0; page < totalPages; page++)
                {
                    DiscordEmbedBuilder embed = new DiscordEmbedBuilder();

                    string rankList = "";
                    string userList = "";
                    string pointList = "";

                    int startAt = page * linesPerPage;

                    List<ClanMember> subList = members.Skip(startAt).Take(linesPerPage).ToList();

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

                    embed.WithFooter($"Page {page + 1} of {totalPages}");

                    pages.Add(new Page("", embed));
                }

                var interactivity = ctx.Client.GetInteractivity();

                await interactivity.SendPaginatedResponseAsync(ctx.Interaction, true, ctx.User, pages, /*buttons*/ null, PaginationBehaviour.Ignore, ButtonPaginationBehavior.DeleteMessage, asEditResponse: false);
            }
        }

        [SlashCommand("points", "Show a members Point total.")]
        public async Task GetPoints(InteractionContext ctx,
            [Option("Members", "Displays point history of given member.")] DiscordUser user = null)
        {
            ClanMember member = null;
            if (user != null)
                member = await _memberService.GetOrCreateMemberAsync(user.Id).ConfigureAwait(false);
            else
                member = await _memberService.GetOrCreateMemberAsync(ctx.User.Id).ConfigureAwait(false);

            await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder(new DiscordMessageBuilder().AddEmbed(new DiscordEmbedBuilder()
                {
                    Title = $"Points",
                    Description = $"{ctx.Client.GetUserAsync(member.DiscordId).Result.Mention} has {member.ClanPoints} points"
                }))
                { IsEphemeral = true });
        }

        [SlashCommand("apply", "Apply to the clan. Required to either fill in recruitedBy or recruitedFrom.")]
        public async Task Apply(InteractionContext ctx,
            [Option("RSN", "Your RSN")] string rsn,
            [Option("cmblevel", "Your Combat level")] long combatLevel,
            [Option("totallevel", "Your total level")] long totalLevel,
            [Option("recruitedby", "Who recruited you if you were recruited ingame")] DiscordUser recruitedBy = null,
            [Option("recruitedfrom", "Where you got recruited from or where you found our clan")] string recruitedFrom = null)
        {
            if (recruitedBy == null && recruitedFrom == null)
            {
                var embed = new DiscordEmbedBuilder()
                {
                    Title = "Error creating application.",
                    Description = "Please provide one of the following in your application: recruitedBy, recruitedFrom"
                };

                var message = new DiscordMessageBuilder().AddEmbed(embed);

                await ctx.CreateResponseAsync(new DiscordInteractionResponseBuilder(message) { IsEphemeral = true });
            }
            else
            {

                DiscordButtonComponent buttonAccept = new DiscordButtonComponent(ButtonStyle.Success, "applyAccept", "Accept");
                DiscordButtonComponent buttonDecline = new DiscordButtonComponent(ButtonStyle.Danger, "applyDeny", "Deny");
                DiscordButtonComponent buttonCancel = new DiscordButtonComponent(ButtonStyle.Secondary, "applyCancel", "Cancel");

                string fieldContext = "RSN:\n" +
                                      "Combat Level:\n" +
                                      "Total Level:";

                string fieldValues = $"{rsn}\n" +
                                     $"{(int)combatLevel}\n" +
                                     $"{(int)totalLevel}";

                if (recruitedBy != null)
                {
                    fieldContext += $"\nRecruited By:";
                    fieldValues += $"\n{recruitedBy.Mention}";
                }

                if (recruitedFrom != null)
                {
                    fieldContext += $"\nRecruited From:";
                    fieldValues += $"\n{recruitedFrom}";
                }

                var embed = new DiscordEmbedBuilder()
                {
                    Title = "New Applicant",
                    Description = ctx.User.Mention
                };

                embed.AddField("Application Details", fieldContext, true);
                embed.AddField("​​​​​\u200b", "​​​​​\u200b", true);
                embed.AddField("​​​​​\u200b", fieldValues, true);

                var message = new DiscordMessageBuilder().AddComponents(buttonAccept, buttonDecline, buttonCancel).AddEmbed(embed);

                await ctx.CreateResponseAsync(new DiscordInteractionResponseBuilder(message));
            }
        }

        [SlashCommand("setrsn", "Set your rsn.")]
        public async Task SetNick(InteractionContext ctx,
            [Option("username", "your rsn")] string rsn)
        {
            DiscordMember discordMember = await ctx.Guild.GetMemberAsync(ctx.User.Id);

            await _memberService.ChangeRSN(ctx.User.Id, rsn);
            string description = $"{discordMember.Mention}\n" +
                                 $"Your RSN has been set to: **{rsn}**";

            try
            {
                await discordMember.ModifyAsync(x => x.Nickname = rsn);

            }
            catch (Exception e)
            {
                if (e is UnauthorizedException slex)
                {
                    description += "\n\nData was saved succesfully, but the bot was unable to update your profile automatically.\n" +
                                   "Please ask a moderator for help.";
                }
            }

            var embed = new DiscordEmbedBuilder()
            {
                Title = "RSN Updated.",
                Description = description
            };
            var message = new DiscordMessageBuilder().AddEmbed(embed);

            await ctx.CreateResponseAsync(new DiscordInteractionResponseBuilder(message));
        }

        [SlashCommand("history", "Displays a members point history.")]
        public async Task ShowHistory(InteractionContext ctx,
        [Option("member", "Displays point history of given member")] DiscordUser user = null)
        {
            try
            {
                DiscordUser userToDisplay = null;
                bool permissionError = false;

                if (user == null)
                {
                    userToDisplay = ctx.User;
                }
                else
                {
                    DiscordMember member = await ctx.Guild.GetMemberAsync(user.Id);
                    if (Helper.IsAdmin(ctx.Guild, member))
                    {
                        userToDisplay = user;
                    }
                    else
                    {
                        userToDisplay = ctx.User;
                        permissionError = true;
                    }
                }

                List<Page> pages = new List<Page>();

                List<Vouch> vouches = await _vouchService.GetVouchesOfUserAsync(userToDisplay.Id);

                int totalLines = vouches.Count;

                if (totalLines == 0)
                {
                    await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder(new DiscordMessageBuilder().AddEmbed(new DiscordEmbedBuilder()
                    {
                        Title = $"No History.",
                        Description = $"There was no point history found for {userToDisplay.Mention}"
                    }))
                    { IsEphemeral = true });
                }
                else
                {
                    int totalPages = totalLines / linesPerPage;
                    int remainder = totalLines % linesPerPage;

                    if (remainder > 0)
                    {
                        totalPages++;
                    }

                    for (int page = 0; page < totalPages; page++)
                    {
                        DiscordEmbedBuilder embed = new DiscordEmbedBuilder();

                        embed.AddField("​​​Point history of:", userToDisplay.Mention, true);
                        embed.AddField("\u200B", "\u200B", true);
                        embed.AddField("\u200B", "\u200B", true);

                        string dateList = "";
                        string pointList = "";
                        string descriptionList = "";

                        int startAt = page * linesPerPage;

                        List<Vouch> subList = vouches.Skip(startAt).Take(linesPerPage).ToList();

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

                        string footer = "";
                        if (permissionError)
                        {
                            footer += $"No permission to view others history. Displaying your own.\n";
                        }
                        footer += $"Page {page + 1} of {totalPages}";

                        embed.WithFooter(footer);

                        pages.Add(new Page("", embed));
                    }

                    var interactivity = ctx.Client.GetInteractivity();

                    await interactivity.SendPaginatedResponseAsync(ctx.Interaction, true, ctx.User, pages, /*buttons*/ null, PaginationBehaviour.Ignore, ButtonPaginationBehavior.DeleteMessage, asEditResponse: false);

                }
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }
    }
}
