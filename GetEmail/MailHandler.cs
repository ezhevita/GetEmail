using System;
using SteamKit2;
using SteamKit2.Internal;

namespace GetEmail;

public class MailHandler : ClientMsgHandler {
	public string? EmailAddress { get; private set; }

	public override void HandleMsg(IPacketMsg packetMsg) {
		ArgumentNullException.ThrowIfNull(packetMsg);

		if (packetMsg.MsgType != EMsg.ClientEmailAddrInfo) {
			return;
		}

		ClientMsgProtobuf<CMsgClientEmailAddrInfo> info = new(packetMsg);
		EmailAddress = info.Body.email_address;
	}
}
