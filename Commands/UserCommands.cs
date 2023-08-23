using Azure;
using DragonBot.Models;
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
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DragonBot.Commands
{
    public class UserCommands : ApplicationCommandModule
    {
        private readonly IClanMemberService _memberService;
        public UserCommands(IClanMemberService context)
        {
            _memberService = context;
        }

        [SlashCommand("highscores", "Display the clan points highscores")]
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

            var interactivity = ctx.Client.GetInteractivity();
            

            List<Page> pages = new List<Page>();

            List<ClanMember> members = await _memberService.GetAllMembers();

            int totalLines = members.Count;

            int totalPages = (totalLines / 10) + 1;

            for (int page = 0; page < totalPages; page++)
            {
                DiscordEmbedBuilder embed = new DiscordEmbedBuilder();

                string rankList = "";
                string userList = "";
                string pointList = "";

                int startAt = page * 10;

                List<ClanMember> subList = members.Skip(startAt).Take(10).ToList();

                foreach (var member in subList)
                {
                    startAt++;
                    rankList += $"{startAt}\n";
                    userList += $"{ctx.Client.GetUserAsync(member.DiscordId).Result.Mention}\n";
                    pointList += $"{member.ClanPoints}\n";

                }
                embed.AddField("​​​Rank", rankList, true);
                embed.AddField("Clan Member", userList, true);
                embed.AddField("Points", pointList, true);

                embed.WithFooter($"Page {page + 1} of {totalPages}");

                pages.Add(new Page("", embed));
            }

            await interactivity.SendPaginatedResponseAsync(ctx.Interaction, true, ctx.User, pages, /*buttons*/ null, PaginationBehaviour.Ignore, ButtonPaginationBehavior.DeleteMessage, asEditResponse: false);
        }

        [SlashCommand("getpoints", "Show a users Point total")]
        public async Task GetPoints(InteractionContext ctx,
            [Option("Members", "Displays point history of given member")] DiscordUser user = null)
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
                })) { IsEphemeral = true } );
        }

        [SlashCommand("apply", "Apply to the clan. Required ")]
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

        [SlashCommand("setrsn", "set your rsn")]
        public async Task SetNick(InteractionContext ctx,
            [Option("string", "your rsn")] string rsn)
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
    }
}
