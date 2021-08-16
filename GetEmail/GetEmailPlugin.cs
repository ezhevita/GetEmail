using System;
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
		private readonly ConcurrentDictionary<Bot, MailHandler> RegisteredHandlers = new();

		public void OnLoaded() {
			Assembly assembly = Assembly.GetExecutingAssembly();
			string repository = assembly
				.GetCustomAttributes<AssemblyMetadataAttribute>()
				.First(x => x.Key == "RepositoryUrl")
				.Value ?? throw new InvalidOperationException(nameof(AssemblyMetadataAttribute));

			const string git = ".git";
			int index = repository.IndexOf(git, StringComparison.Ordinal);
			if (index >= 0) {
				repository = repository[..(index + 1)];
			}

			string company = assembly
				.GetCustomAttribute<AssemblyCompanyAttribute>()?.Company ?? throw new InvalidOperationException(nameof(AssemblyCompanyAttribute));

			ASF.ArchiLogger.LogGenericInfo(Name + " by " + company + " | Support & source code: " + repository);
		}

		public string Name => nameof(GetEmail);
		public Version Version => Assembly.GetExecutingAssembly().GetName().Version ?? throw new InvalidOperationException(nameof(Version));

		public void OnBotDestroy(Bot bot) {
			RegisteredHandlers.TryRemove(bot, out _);
		}

		public void OnBotInit(Bot bot) {
			RegisteredHandlers.TryAdd(bot, new MailHandler());
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

		public void OnBotSteamCallbacksInit(Bot bot, CallbackManager callbackManager) { }

		public IReadOnlyCollection<ClientMsgHandler> OnBotSteamHandlersInit(Bot bot) => new[] { RegisteredHandlers[bot] };

		private string? ResponseEmail(Bot bot, ulong steamID) {
			if (!bot.HasAccess(steamID, BotConfig.EAccess.Master)) {
				return null;
			}

			if (!RegisteredHandlers.TryGetValue(bot, out MailHandler? handler)) {
				return bot.Commands.FormatBotResponse(string.Format(CultureInfo.CurrentCulture, Strings.WarningFailedWithError, nameof(RegisteredHandlers)));
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
