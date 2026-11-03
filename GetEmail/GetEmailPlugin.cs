using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Composition;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading;
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
internal sealed class GetEmailPlugin : IBot, IBotSteamClient, IBotCommand, IGitHubPluginUpdates {
	private readonly ConcurrentDictionary<Bot, MailHandler> _registeredHandlers = new();

	public Task OnLoaded(CancellationToken cancellationToken) {
		ASF.ArchiLogger.LogGenericInfo($"{Name} by ezhevita | Support & source code: https://github.com/ezhevita/{Name}");
		return Task.CompletedTask;
	}

	public string Name => nameof(GetEmail);
	public string RepositoryName => "ezhevita/GetEmail";
	public Version Version => Assembly.GetExecutingAssembly().GetName().Version ?? throw new InvalidOperationException(nameof(Version));

	public async Task<string?> OnBotCommand(Bot bot, EBotUserAccess access, string message, string[] args, ulong steamID = 0, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(bot);
		ArgumentNullException.ThrowIfNull(args);
		ArgumentException.ThrowIfNullOrEmpty(message);

		return args[0].ToUpperInvariant() switch {
			"EMAIL" when args.Length > 1 => await ResponseEmail(access, Utilities.GetArgsAsText(args, 1, ","), steamID).ConfigureAwait(false),
			"EMAIL" => ResponseEmail(bot, access),
			_ => null
		};
	}

	public Task OnBotDestroy(Bot bot, CancellationToken cancellationToken)
	{
		_registeredHandlers.TryRemove(bot, out _);

		return Task.CompletedTask;
	}

	public Task OnBotInit(Bot bot, CancellationToken cancellationToken)
	{
		_registeredHandlers.TryAdd(bot, new MailHandler());

		return Task.CompletedTask;
	}

	public Task OnBotSteamCallbacksInit(Bot bot, CallbackManager callbackManager, CancellationToken cancellationToken) => Task.CompletedTask;

	public Task<IReadOnlyCollection<ClientMsgHandler>?> OnBotSteamHandlersInit(Bot bot, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyCollection<ClientMsgHandler>?>([_registeredHandlers[bot]]);

	private string? ResponseEmail(Bot bot, EBotUserAccess access) {
		if (access < EBotUserAccess.Master) {
			return null;
		}

		if (!_registeredHandlers.TryGetValue(bot, out var handler)) {
			return bot.Commands.FormatBotResponse(string.Format(CultureInfo.CurrentCulture, Strings.WarningFailedWithError, nameof(_registeredHandlers)));
		}

		return bot.Commands.FormatBotResponse(string.IsNullOrEmpty(handler.EmailAddress) ? Strings.WarningFailed : handler.EmailAddress!);
	}

	private async Task<string?> ResponseEmail(EBotUserAccess access, string botNames, ulong steamID) {
		var bots = Bot.GetBots(botNames);
		if ((bots == null) || (bots.Count == 0)) {
			return access >= EBotUserAccess.Owner ? Commands.FormatStaticResponse(string.Format(CultureInfo.CurrentCulture, Strings.BotNotFound, botNames)) : null;
		}

		IList<string?> results = await Utilities.InParallel(bots.Select(bot => Task.Run(() => ResponseEmail(bot, Commands.GetProxyAccess(bot, access, steamID))))).ConfigureAwait(false);

		List<string> responses = new(results.Where(result => !string.IsNullOrEmpty(result))!);

		return responses.Count > 0 ? string.Join(Environment.NewLine, responses) : null;
	}
}
