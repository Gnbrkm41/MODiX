﻿using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Humanizer;

using Modix.Data.Repositories;
using Modix.Services.Core;
using Modix.Services.Utilities;

namespace Modix.Modules
{
    public sealed class GuildInfoModule : ModuleBase
    {
        //optimization: UtcNow is slow and the module is created per-request
        private readonly DateTime _utcNow = DateTime.UtcNow;

        public GuildInfoModule(
            IGuildService guildService,
            IMessageRepository messageRepository,
            IHttpClientFactory httpClientFactory)
        {
            GuildService = guildService;
            MessageRepository = messageRepository;
            HttpClientFactory = httpClientFactory;
        }

        private IGuildService GuildService { get; }

        private IMessageRepository MessageRepository { get; }

        private IHttpClientFactory HttpClientFactory { get; }

        [Command("guildinfo")]
        [Alias("serverinfo")]
        [Summary("Retrieves information about the supplied guild, or the current guild if one is not provided.")]
        public async Task GetUserInfoAsync(
            [Summary("The unique Discord snowflake ID of the guild to retrieve information about, if any.")]
                ulong? guildId = null)
        {
            var timer = Stopwatch.StartNew();

            var resolvedGuildId = guildId ?? Context.Guild.Id;

            var guildResult = await GuildService.GetGuildInformationAsync(resolvedGuildId);

            if (guildResult.IsError)
            {
                var errorDescription = guildResult.IsError
                    ? guildResult.Error
                    : "Sorry, we don't have any data for that guild - and we couldn't find any, either.";

                await ReplyAsync(embed: new EmbedBuilder()
                    .WithTitle("Retrieval Error")
                    .WithColor(Color.Red)
                    .WithDescription(errorDescription)
                    .AddField("Guild Id", resolvedGuildId)
                    .Build());

                return;
            }

            var embedBuilder = new EmbedBuilder()
                .WithAuthor(guildResult.Guild.Name, guildResult.Guild.IconUrl)
                .WithThumbnailUrl(guildResult.Guild.IconUrl)
                .WithTimestamp(_utcNow);

            await WithDominantColorAsync(embedBuilder, guildResult.Guild);

            var stringBuilder = new StringBuilder();

            AppendGuildInformation(stringBuilder, guildResult.Guild);

            if (guildResult.Guild is SocketGuild socketGuild)
            {
                await AppendGuildParticipationAsync(stringBuilder, socketGuild);
                AppendMemberInformation(stringBuilder, socketGuild);
            }

            embedBuilder.WithDescription(stringBuilder.ToString());

            timer.Stop();
            embedBuilder.WithFooter($"Completed after {timer.ElapsedMilliseconds} ms");

            await ReplyAsync(embed: embedBuilder.Build());
        }

        public async Task WithDominantColorAsync(EmbedBuilder embedBuilder, IGuild guild)
        {
            if (guild.IconUrl is string iconUrl)
            {
                using (var httpStream = await HttpClientFactory.CreateClient().GetStreamAsync(iconUrl))
                using (var iconStream = new MemoryStream())
                {
                    await httpStream.CopyToAsync(iconStream);

                    var icon = new Image(iconStream);

                    embedBuilder.WithColor(FormatUtilities.GetDominantColor(icon));
                }
            }
        }

        public void AppendGuildInformation(StringBuilder stringBuilder, IGuild guild)
        {
            stringBuilder
                .AppendLine(Format.Bold("\u276F Guild Information"))
                .AppendLine($"ID: {guild.Id}")
                .AppendLine($"Owner: {MentionUtils.MentionUser(guild.OwnerId)}")
                .AppendLine($"Created: {FormatUtilities.FormatTimeAgo(_utcNow, guild.CreatedAt)}")
                .AppendLine();
        }

        public async Task AppendGuildParticipationAsync(StringBuilder stringBuilder, SocketGuild guild)
        {
            var weekTotal = await MessageRepository.GetTotalMessageCountAsync(guild.Id, TimeSpan.FromDays(7));
            var monthTotal = await MessageRepository.GetTotalMessageCountAsync(guild.Id, TimeSpan.FromDays(30));

            var channelCounts = await MessageRepository.GetTotalMessageCountByChannelAsync(guild.Id, TimeSpan.FromDays(30));
            var orderedChannelCounts = channelCounts.OrderByDescending(x => x.Value);
            var mostActiveChannel = orderedChannelCounts.First();
            var leastActiveChannel = orderedChannelCounts.Last();

            stringBuilder
                .AppendLine(Format.Bold("\u276F Guild Participation"))
                .AppendLine($"Last 7 days: {"message".ToQuantity(weekTotal, "n0")}")
                .AppendLine($"Last 30 days: {"message".ToQuantity(monthTotal, "n0")}")
                .AppendLine($"Avg. per day: {"message".ToQuantity(monthTotal / 30, "n0")}")
                .AppendLine($"Most active channel: {MentionUtils.MentionChannel(mostActiveChannel.Key)} ({"message".ToQuantity(mostActiveChannel.Value, "n0")} in 30 days)")
                .AppendLine();
        }

        public void AppendMemberInformation(StringBuilder stringBuilder, SocketGuild guild)
        {
            var memberCount = guild.Users.Count;
            var bots = guild.Users.Count(x => x.IsBot);
            var humans = memberCount - bots;

            var roles = guild.Roles
                .Where(x => x.Id != guild.EveryoneRole.Id)
                .OrderByDescending(x => x.Position);

            stringBuilder
                .AppendLine(Format.Bold("\u276F Member Information"))
                .AppendLine($"Members: {memberCount}")
                .AppendLine($"Humans: {humans}")
                .AppendLine($"Bots: {bots}")
                .AppendLine($"Roles: {roles.Select(x => x.Mention).Humanize()}")
                .AppendLine();
        }
    }
}