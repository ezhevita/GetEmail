using System;
using SteamKit2;
using SteamKit2.Internal;

namespace GetEmail {
	public class MailHandler : ClientMsgHandler {
		public string? EmailAddress { get; private set; }

		public override void HandleMsg(IPacketMsg packetMsg) {
			if (packetMsg == null) {
				throw new ArgumentNullException(nameof(packetMsg));
			}

			if (packetMsg.MsgType != EMsg.ClientEmailAddrInfo) {
				return;
			}

			ClientMsgProtobuf<CMsgClientEmailAddrInfo> info = new(packetMsg);
			EmailAddress = info.Body.email_address;
		}
	}
}
