﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Composition;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using ArchiSteamFarm.Core;
using ArchiSteamFarm.Localization;
using ArchiSteamFarm.Plugins.Interfaces;
using ArchiSteamFarm.Steam;
using ArchiSteamFarm.Steam.Interaction;
using ArchiSteamFarm.Steam.Storage;
using JetBrains.Annotations;
using SteamKit2;

namespace GetEmail {
	[Export(typeof(IPlugin))]
	[UsedImplicitly]
	public class GetEmailPlugin : IBot, IBotSteamClient, IBotCommand {
		private readonly ConcurrentDictionary<Bot, MailHandler> registeredHandlers = new();

		public Task OnLoaded() {
			ASF.ArchiLogger.LogGenericInfo($"{Name} by Vital7 | Support & source code: https://github.com/Vital7/{Name}");
			return Task.CompletedTask;
		}

		public string Name => nameof(GetEmail);
		public Version Version => Assembly.GetExecutingAssembly().GetName().Version ?? throw new InvalidOperationException(nameof(Version));

		public Task OnBotDestroy(Bot bot)
		{
			registeredHandlers.TryRemove(bot, out _);

			return Task.CompletedTask;
		}

		public Task OnBotInit(Bot bot)
		{
			registeredHandlers.TryAdd(bot, new MailHandler());

			return Task.CompletedTask;
		}

		[CLSCompliant(false)]
		public async Task<string?> OnBotCommand(Bot bot, ulong steamID, string message, string[] args) {
			if (bot == null) {
				throw new ArgumentNullException(nameof(bot));
			}

			if (string.IsNullOrEmpty(message)) {
				throw new ArgumentNullException(nameof(message));
			}

			if (args == null) {
				throw new ArgumentNullException(nameof(args));
			}

			return args[0].ToUpperInvariant() switch {
				"EMAIL" when args.Length > 1 => await ResponseEmail(steamID, Utilities.GetArgsAsText(args, 1, ",")).ConfigureAwait(false),
				"EMAIL" => ResponseEmail(bot, steamID),
				_ => null
			};
		}

		public Task OnBotSteamCallbacksInit(Bot bot, CallbackManager callbackManager) => Task.CompletedTask;

		public Task<IReadOnlyCollection<ClientMsgHandler>?> OnBotSteamHandlersInit(Bot bot) => Task.FromResult<IReadOnlyCollection<ClientMsgHandler>?>(new[] {registeredHandlers[bot]});

		private string? ResponseEmail(Bot bot, ulong steamID) {
			if (!bot.HasAccess(steamID, BotConfig.EAccess.Master)) {
				return null;
			}

			if (!registeredHandlers.TryGetValue(bot, out MailHandler? handler)) {
				return bot.Commands.FormatBotResponse(string.Format(CultureInfo.CurrentCulture, Strings.WarningFailedWithError, nameof(registeredHandlers)));
			}

			return bot.Commands.FormatBotResponse(string.IsNullOrEmpty(handler.EmailAddress) ? Strings.WarningFailed : handler.EmailAddress!);
		}

		private async Task<string?> ResponseEmail(ulong steamID, string botNames) {
			HashSet<Bot>? bots = Bot.GetBots(botNames);
			if ((bots == null) || (bots.Count == 0)) {
				return ASF.IsOwner(steamID) ? Commands.FormatStaticResponse(string.Format(CultureInfo.CurrentCulture, Strings.BotNotFound, botNames)) : null;
			}

			IList<string?> results = await Utilities.InParallel(bots.Select(bot => Task.Run(() => ResponseEmail(bot, steamID)))).ConfigureAwait(false);

			List<string> responses = new(results.Where(result => !string.IsNullOrEmpty(result))!);

			return responses.Count > 0 ? string.Join(Environment.NewLine, responses) : null;
		}
	}
}
