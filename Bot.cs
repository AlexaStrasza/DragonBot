using DragonBot.Commands;
using DragonBot.ConfigFiles;
using DragonBot.Helpers;
using DragonBot.Models;
using DragonBot.Services;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.CommandsNext.Exceptions;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using DSharpPlus.Interactivity;
using DSharpPlus.Interactivity.EventHandling;
using DSharpPlus.Interactivity.Extensions;
using DSharpPlus.SlashCommands;
using DSharpPlus.SlashCommands.Attributes;
using DSharpPlus.SlashCommands.EventArgs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using System;
using System.Text;

namespace DragonBot
{
    public class Bot
    {
        public DiscordClient Client { get; private set; }
        public InteractivityExtension Interactivity { get; private set; }
        public CommandsNextExtension Commands { get; private set; }
        public IServiceProvider _services { get; private set; }

        private readonly IClanMemberService _clanMemberService;
        private readonly IPointDistributionService _pointDistributionService;
        private readonly IVouchService _vouchService;

        public Bot(IServiceProvider services, IClanMemberService memberService, IPointDistributionService pointsService, IVouchService vouchService)
        {
            _services = services;
            _clanMemberService = memberService;
            _pointDistributionService = pointsService;
            _vouchService = vouchService;
        }

        public async Task RunAsync()
        {
            var json = string.Empty;

            using (FileStream fs = File.OpenRead("config.json"))
            using (StreamReader sr = new StreamReader(fs, new UTF8Encoding(false)))
                json = sr.ReadToEnd();

            ConfigJson configJson = JsonConvert.DeserializeObject<ConfigJson>(json);

            DiscordConfiguration config = new DiscordConfiguration()
            {
                Intents = DiscordIntents.All,
                Token = configJson.Token,
                TokenType = TokenType.Bot,
                AutoReconnect = true,
                MinimumLogLevel = LogLevel.Debug,
            };

            Client = new DiscordClient(config);

            Client.Ready += OnClientReady;



            Client.UseInteractivity(new InteractivityConfiguration()
            {

            });

            Client.ComponentInteractionCreated += OnButtonPressed;
            Client.GuildMemberRemoved += OnMemberLeave;
            var commandsConfig = new CommandsNextConfiguration()
            {
                StringPrefixes = new string[] { configJson.Prefix },
                CaseSensitive = false,
                EnableDms = false,
                EnableMentionPrefix = true,
                Services = _services,

            };

            Commands = Client.UseCommandsNext(commandsConfig);
            var slashCommandsConfig = Client.UseSlashCommands(new SlashCommandsConfiguration()
            {
                Services = _services
            });

            slashCommandsConfig.RegisterCommands<AdminCommands>(1132841029192658954);
            slashCommandsConfig.RegisterCommands<AdminCommands>(1133706557104861294);
            slashCommandsConfig.RegisterCommands<UserCommands>(1132841029192658954);
            slashCommandsConfig.RegisterCommands<UserCommands>(1133706557104861294);
            //Commands.RegisterCommands<TeamCommands>();
            slashCommandsConfig.SlashCommandErrored += OnSlashCommandError;
            Commands.CommandErrored += OnCommandError;

            await Client.ConnectAsync();
            await Task.Delay(-1);
        }

        private async Task OnMemberLeave(DiscordClient sender, GuildMemberRemoveEventArgs e)
        {
            await _clanMemberService.ToggleMember(e.Member.Id, false);
        }

        private async Task OnButtonPressed(DiscordClient sender, ComponentInteractionCreateEventArgs e)
        {

            switch (e.Interaction.Data.CustomId)
            {
                case "applyAccept":
                    await HandleApplyAcceptButtonAsync(sender, e);
                    break;
                case "applyDeny":
                    await HandleApplyDenyButtonAsync(sender, e);
                    break;
                case "applyCancel":
                    await HandleApplyCancelButtonAsync(sender, e);
                    break;
                case "confirmRankup":
                    await HandleConfirmRankup(sender, e);
                    break;
                case "sampleButton":
                    var modalButton = new DiscordButtonComponent(ButtonStyle.Secondary, "closeModalButton", "Close Modal");
                    var modalBuilder = new DiscordMessageBuilder()
                        .WithContent("This is a modal!")
                    .AddComponents(modalButton);

                    await e.Interaction.CreateResponseAsync(InteractionResponseType.Modal, new DiscordInteractionResponseBuilder(modalBuilder));
                    break;
            }
        }

        private async Task HandleConfirmRankup(DiscordClient sender, ComponentInteractionCreateEventArgs e)
        {
            await e.Message.ModifyAsync(m =>
            {
                m.ClearComponents();
                m.Content = $"Confirmed by {e.User.Mention}";
            });
        }

        private async Task HandleApplyAcceptButtonAsync(DiscordClient sender, ComponentInteractionCreateEventArgs e)
        {
            var member = await e.Guild.GetMemberAsync(e.Interaction.User.Id);
            if (Helper.IsAdmin(e.Guild, member))
            {
                ulong AAapplicantId = ulong.Parse(Helper.GetNumbers(e.Message.Embeds[0].Description));

                string content = e.Message.Embeds[0].Fields[2].Value;

                string[] strings = content.Split('\n');

                string rsn = strings[0];
                int combatLevel = int.Parse(strings[1]);
                int totalLevel = int.Parse(strings[2]);
                ulong referreeId = 0;
                string recruitedFrom = "";
                Console.WriteLine("===================================================================");
                Console.WriteLine("New applicant");
                Console.WriteLine("===================================================================");
                Console.WriteLine("Content of strings[3]:");
                Console.WriteLine(strings[3]);
                Console.WriteLine("Check contains <@!");
                Console.WriteLine(strings[3].Contains("<@!"));
                Console.WriteLine("strings.Length");
                Console.WriteLine(strings.Length);
                if (strings.Length >= 4)
                {
                    Console.WriteLine("get numbers");
                    Console.WriteLine(Helper.GetNumbers(strings[3]));
                }

                if (strings.Length >= 4 && strings[3].Contains("<@!"))
                {
                    referreeId = ulong.Parse(Helper.GetNumbers(strings[3]));
                    await _clanMemberService.GetOrCreateMemberAsync(referreeId);
                    await _pointDistributionService.ChangePointsAsync(referreeId, 5, "Recruitment Bonus");
                    await _clanMemberService.AddReferral(referreeId);
                }
                else if (strings.Length >= 4 && !strings[3].IsNullOrEmpty())
                {
                    recruitedFrom = strings[3];
                }

                await _clanMemberService.GetOrCreateMemberAsync(AAapplicantId, rsn);
                await _pointDistributionService.ChangePointsAsync(AAapplicantId, 50, "Clan Application Accepted");

                var embedAccept = new DiscordEmbedBuilder()
                {
                    Title = "Application Accepted",
                    Description = $"<@!{AAapplicantId}>",
                    Color = DiscordColor.SpringGreen,
                };

                string fieldContext = "RSN:\n" +
                          "Combat Level:\n" +
                          "Total Level:";

                string fieldValues = $"{rsn}\n" +
                                     $"{combatLevel}\n" +
                                     $"{totalLevel}";

                if (referreeId != 0)
                {
                    fieldContext += $"\nRecruited By:";
                    fieldValues += $"\n<@!{referreeId}>";
                }

                if (!recruitedFrom.IsNullOrEmpty())
                {
                    fieldContext += $"\nRecruited From:";
                    fieldValues += $"\n{recruitedFrom}";
                }

                embedAccept.AddField("Application Details", fieldContext, true);
                embedAccept.AddField("​​​​​\u200b", "​​​​​\u200b", true);
                embedAccept.AddField("​​​​​\u200b", fieldValues, true);
                embedAccept.AddField("Accepted On:", $"<t:{(int)DateTime.Now.Subtract(new DateTime(1970, 1, 1)).TotalSeconds}:D>\n", true);

                await e.Interaction.CreateResponseAsync(InteractionResponseType.UpdateMessage, new DiscordInteractionResponseBuilder(new DiscordMessageBuilder().AddEmbed(embedAccept)));

                DiscordMember applicant = await e.Guild.GetMemberAsync(AAapplicantId);

                //await applicant.RevokeRoleAsync(e.Guild.GetRole(1133480882972414114));
                await applicant.GrantRoleAsync(e.Guild.GetRole(1134194679773139026)); // Assign new member role
                //await applicant.GrantRoleAsync(e.Guild.GetRole(1140406825943044246)); // Assign new member role
                await applicant.ModifyAsync(e => e.Nickname = rsn);
            }
            else
            {

                var embedAcceptError = new DiscordEmbedBuilder()
                {
                    Title = "Permission Error",
                    Description = "You do not have permission to accept new members."
                };

                await e.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder(new DiscordMessageBuilder().AddEmbed(embedAcceptError)) { IsEphemeral = true });
            }
        }

        private async Task HandleApplyDenyButtonAsync(DiscordClient sender, ComponentInteractionCreateEventArgs e)
        {
            var member = await e.Guild.GetMemberAsync(e.Interaction.User.Id);
            if (Helper.IsAdmin(e.Guild, member)) // check admin
            {
                ulong ADapplicantId = ulong.Parse(Helper.GetNumbers(e.Message.Embeds[0].Description));

                string content = e.Message.Embeds[0].Fields[2].Value;

                string[] strings = content.Split('\n');

                string rsn = strings[0];
                int combatLevel = int.Parse(strings[1]);
                int totalLevel = int.Parse(strings[2]);
                ulong referreeId = 0;
                string recruitedFrom = "";
                if (strings.Length == 4 && strings[3].Contains("<@!"))
                {
                    referreeId = ulong.Parse(Helper.GetNumbers(strings[3]));
                }
                else if (strings.Length == 4 && !strings[3].IsNullOrEmpty())
                {
                    recruitedFrom = strings[3];
                }

                var embedDeny = new DiscordEmbedBuilder()
                {
                    Title = "Application Denied",
                    Description = $"<@!{ADapplicantId}>",
                    Color = DiscordColor.Red
                };

                string fieldContext = "RSN:\n" +
                          "Combat Level:\n" +
                          "Total Level:";

                string fieldValues = $"{rsn}\n" +
                                     $"{combatLevel}\n" +
                                     $"{totalLevel}";

                if (referreeId != 0)
                {
                    fieldContext += $"\nRecruited By:";
                    fieldValues += $"\n<@!{referreeId}>";
                }

                if (!recruitedFrom.IsNullOrEmpty())
                {
                    fieldContext += $"\nRecruited From:";
                    fieldValues += $"\n{recruitedFrom}";
                }

                embedDeny.AddField("Application Details", fieldContext, true);
                embedDeny.AddField("​​​​​\u200b", "​​​​​\u200b", true);
                embedDeny.AddField("​​​​​\u200b", fieldValues, true);

                DiscordButtonComponent buttonDelete = new DiscordButtonComponent(ButtonStyle.Danger, "deleteMessage", "Delete");
                var messageBuilder = new DiscordMessageBuilder();
                messageBuilder.AddComponents(buttonDelete);
                messageBuilder.AddEmbed(embedDeny);
                await e.Interaction.CreateResponseAsync(InteractionResponseType.UpdateMessage, new DiscordInteractionResponseBuilder(messageBuilder));
            }
        }

        private async Task HandleApplyCancelButtonAsync(DiscordClient sender, ComponentInteractionCreateEventArgs e)
        {
            var member = await e.Guild.GetMemberAsync(e.Interaction.User.Id);
            ulong applicantId = ulong.Parse(Helper.GetNumbers(e.Message.Embeds[0].Description));
            if (e.Interaction.User.Id == applicantId || Helper.IsAdmin(e.Guild, member)) // check if admin presses button or user who made the application
            {
                var embedCancel = new DiscordEmbedBuilder()
                {
                    Title = "Cancelled",
                    Description = "Your application has been cancelled."
                };

                await e.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder(new DiscordMessageBuilder().AddEmbed(embedCancel)) { IsEphemeral = true });
                await e.Message.DeleteAsync();
            }
        }

        private static async Task OnSlashCommandError(SlashCommandsExtension sender, SlashCommandErrorEventArgs e)
        {
            if (e.Exception is SlashExecutionChecksFailedException slex)
            {
                foreach (var check in slex.FailedChecks)
                {
                    if (check is SlashRequirePermissionsAttribute att)
                    {
                        await e.Context.CreateResponseAsync(new DiscordInteractionResponseBuilder()
                        {
                            IsEphemeral = true,
                        }.WithContent("You do not have permission to execute this command."));
                    }
                }
            }
            throw new Exception(e.Exception.ToString());
        }

        private Task OnClientReady(DiscordClient s, ReadyEventArgs e)
        {
            return Task.CompletedTask;
        }

        private static async Task OnCommandError(CommandsNextExtension sender, CommandErrorEventArgs e)
        {
            if (e.Exception is ChecksFailedException)
            {
                var castedException = (ChecksFailedException)e.Exception;
                string cooldownTimer = string.Empty;

                foreach (var check in castedException.FailedChecks)
                {
                    var cooldown = (CooldownAttribute)check;
                    TimeSpan timeLeft = cooldown.GetRemainingCooldown(e.Context);
                    cooldownTimer = timeLeft.ToString(@"hh\:mm\:ss");
                }

                var cooldownMessage = new DiscordEmbedBuilder()
                {
                    Title = "Wait for the Cooldown to End",
                    Description = "Remaining Time: " + cooldownTimer,
                    Color = DiscordColor.Red
                };

                await e.Context.Channel.SendMessageAsync(embed: cooldownMessage);
            }
        }
    }
}
