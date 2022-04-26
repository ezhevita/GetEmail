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
using JetBrains.Annotations;
using SteamKit2;

namespace GetEmail;

[Export(typeof(IPlugin))]
[UsedImplicitly]
public class GetEmailPlugin : IBot, IBotSteamClient, IBotCommand2 {
	private readonly ConcurrentDictionary<Bot, MailHandler> registeredHandlers = new();

	public Task OnLoaded() {
		ASF.ArchiLogger.LogGenericInfo($"{Name} by Vital7 | Support & source code: https://github.com/Vital7/{Name}");
		return Task.CompletedTask;
	}

	public string Name => nameof(GetEmail);
	public Version Version => Assembly.GetExecutingAssembly().GetName().Version ?? throw new InvalidOperationException(nameof(Version));

	public async Task<string?> OnBotCommand(Bot bot, EAccess access, string message, string[] args, ulong steamID = 0)
	{
		return args[0].ToUpperInvariant() switch {
			"EMAIL" when args.Length > 1 => await ResponseEmail(access, Utilities.GetArgsAsText(args, 1, ","), steamID).ConfigureAwait(false),
			"EMAIL" => ResponseEmail(bot, access),
			_ => null
		};
	}

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

	public Task OnBotSteamCallbacksInit(Bot bot, CallbackManager callbackManager) => Task.CompletedTask;

	public Task<IReadOnlyCollection<ClientMsgHandler>?> OnBotSteamHandlersInit(Bot bot) => Task.FromResult<IReadOnlyCollection<ClientMsgHandler>?>(new[] {registeredHandlers[bot]});

	private string? ResponseEmail(Bot bot, EAccess access) {
		if (access < EAccess.Master) {
			return null;
		}

		if (!registeredHandlers.TryGetValue(bot, out MailHandler? handler)) {
			return bot.Commands.FormatBotResponse(string.Format(CultureInfo.CurrentCulture, Strings.WarningFailedWithError, nameof(registeredHandlers)));
		}

		return bot.Commands.FormatBotResponse(string.IsNullOrEmpty(handler.EmailAddress) ? Strings.WarningFailed : handler.EmailAddress!);
	}

	private async Task<string?> ResponseEmail(EAccess access, string botNames, ulong steamID) {
		HashSet<Bot>? bots = Bot.GetBots(botNames);
		if ((bots == null) || (bots.Count == 0)) {
			return access >= EAccess.Owner ? Commands.FormatStaticResponse(string.Format(CultureInfo.CurrentCulture, Strings.BotNotFound, botNames)) : null;
		}

		IList<string?> results = await Utilities.InParallel(bots.Select(bot => Task.Run(() => ResponseEmail(bot, Commands.GetProxyAccess(bot, access, steamID))))).ConfigureAwait(false);

		List<string> responses = new(results.Where(result => !string.IsNullOrEmpty(result))!);

		return responses.Count > 0 ? string.Join(Environment.NewLine, responses) : null;
	}
}