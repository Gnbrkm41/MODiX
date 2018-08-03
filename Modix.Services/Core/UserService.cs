﻿using System;
using System.Threading.Tasks;

using Discord;

using Modix.Data.Models.Core;
using Modix.Data.Repositories;

namespace Modix.Services.Core
{
    /// <inheritdoc />
    public class UserService : IUserService
    {
        /// <summary>
        /// Constructs a new <see cref="UserService"/> with the given injected dependencies.
        /// </summary>
        /// <param name="discordClient">The value to use for <see cref="DiscordClient"/>.</param>
        /// <param name="authorizationService">The value to use for <see cref="AuthorizationService"/>.</param>
        /// <param name="guildService">The value to use for <see cref="GuildService"/>.</param>
        /// <param name="userRepository">The value to use for <see cref="UserRepository"/>.</param>
        /// <exception cref="ArgumentNullException">Throws for all parameters.</exception>
        public UserService(IDiscordClient discordClient, IAuthorizationService authorizationService, IGuildService guildService, IUserRepository userRepository)
        {
            DiscordClient = discordClient ?? throw new ArgumentNullException(nameof(discordClient));
            AuthorizationService = authorizationService ?? throw new ArgumentNullException(nameof(authorizationService));
            GuildService = guildService ?? throw new ArgumentNullException(nameof(guildService));
            UserRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        }

        /// <inheritdoc />
        public async Task<IUser> GetUserAsync(ulong userId)
        {
            var user = (AuthorizationService.CurrentGuildId == null)
                ? await DiscordClient.GetUserAsync(userId)
                : await (await GuildService.GetGuildAsync(AuthorizationService.CurrentGuildId.Value))
                    .GetUserAsync(userId);

            if (user == null)
                throw new InvalidOperationException($"Discord user {userId} does not exist");

            await TrackUserAsync(user);

            return user;
        }

        /// <inheritdoc />
        public async Task<IGuildUser> GetGuildUserAsync(ulong guildId, ulong userId)
        {
            var guild = await GuildService.GetGuildAsync(guildId);
            if (guild == null)
                throw new InvalidOperationException($"Discord guild {guildId} does not exist");

            var user = await guild.GetUserAsync(userId);
            if (user == null)
                throw new InvalidOperationException($"Discord user {userId} does not exist");

            await TrackUserAsync(user);

            return user;
        }

        /// <inheritdoc />
        public async Task TrackUserAsync(IUser user)
        {
            var guildUser = user as IGuildUser;

            using (var transaction = await UserRepository.BeginCreateTransactionAsync())
            {
                if (!(await UserRepository.TryUpdateAsync(user.Id, data =>
                {
                    // Only update properties that we were given. Updates can be triggered from several different sources, not all of which have all the user's info.
                    if (user.Username != null)
                        data.Username = user.Username;
                    if(user.DiscriminatorValue != 0)
                        data.Discriminator = user.Discriminator;
                    if (guildUser != null)
                        data.Nickname = guildUser.Nickname;
                    data.LastSeen = DateTimeOffset.Now;
                })))
                {
                    await UserRepository.CreateAsync(new UserCreationData()
                    {
                        Id = user.Id,
                        Username = user.Username ?? "[UNKNOWN USERNAME]",
                        Discriminator = (user.DiscriminatorValue == 0) ? "????" : user.Discriminator,
                        Nickname = guildUser?.Nickname,
                        FirstSeen = DateTimeOffset.Now,
                        LastSeen = DateTimeOffset.Now
                    });
                }

                transaction.Commit();
            }
        }

        /// <summary>
        /// A <see cref="IDiscordClient"/> to be used to interact with the Discord API.
        /// </summary>
        internal protected IDiscordClient DiscordClient { get; }

        /// <summary>
        /// A <see cref="IAuthorizationService"/> to be used to interact with frontend authentication system, and perform authorization.
        /// </summary>
        internal protected IAuthorizationService AuthorizationService { get; }

        /// <summary>
        /// A <see cref="IGuildService"/> to be used to interact with Discord guild objects.
        /// </summary>
        internal protected IGuildService GuildService { get; }

        /// <summary>
        /// A <see cref="IUserRepository"/> to be used to interact with user data within a datastore.
        /// </summary>
        internal protected IUserRepository UserRepository { get; }
    }
}
