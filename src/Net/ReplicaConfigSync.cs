using System;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Multiplayer.Serialization;
using MegaCrit.Sts2.Core.Runs;
using Sts2Replica.Config;

namespace Sts2Replica.Net;

/// <summary>
/// Exchanges the host's mod parameters once per run. Each side both asks and answers, so whichever side
/// reaches RunStarted first still converges: a message that arrives before the other side registered its
/// handler is dropped by the vanilla bus (NetMessageBus.cs:88), and the opposite direction re-sends.
/// </summary>
public static class ReplicaConfigSync
{
	private static INetGameService? _attached;
	private static RunState? _runState;
	private static bool _loggedMessageIds;
	private static bool _adoptedHostConfig;
	private static bool _configReRequested;

	public static void AttachForRun(RunState state)
	{
		_runState = state;
		// RunStarted fires again on load and the override was cleared just before this call, so the
		// one-shot self-heal starts over from "nothing adopted yet".
		_adoptedHostConfig = false;
		_configReRequested = false;
		INetGameService? service = RunManager.Instance.NetService;
		NetGameType type = service?.Type ?? NetGameType.None;
		LogMessageIdsOnce();
		if (service == null || (type != NetGameType.Host && type != NetGameType.Client))
		{
			// Singleplayer and replays have no peer above them; the loaded file is already the answer.
			Log.Info("[sts2-replica] singleplayer: settings stay local; " + ReplicaRuntimeConfig.Fingerprint(ReplicaRuntimeConfig.Effective));
			return;
		}
		if (!ReferenceEquals(_attached, service))
		{
			// RunStarted fires again on load; registering twice would run every handler twice.
			_attached = service;
			if (type == NetGameType.Host)
			{
				service.RegisterMessageHandler<ReplicaConfigRequestMessage>(OnConfigRequested);
			}
			else
			{
				service.RegisterMessageHandler<ReplicaConfigMessage>(OnConfigReceived);
			}
			Log.Info("[sts2-replica] config sync handlers registered; this peer is the " + type);
		}
		if (!service.IsConnected)
		{
			// Registration is a local dictionary add (NetMessageBus.cs:127-144), so the handler above is
			// already armed; only the traffic on the wire has to wait for the transport.
			Log.Warn("[sts2-replica] config sync skipped: the net service is not connected yet");
			return;
		}
		if (type == NetGameType.Host)
		{
			BroadcastConfig(service);
			return;
		}
		service.SendMessage(new ReplicaConfigRequestMessage());
		Log.Info("[sts2-replica] client: asked the host for its config");
	}

	private static void BroadcastConfig(INetGameService service)
	{
		ReplicaConfig config = ReplicaRuntimeConfig.Effective;
		try
		{
			service.SendMessage(ReplicaConfigMessage.From(config));
			Log.Info("[sts2-replica] host: broadcast config " + ReplicaRuntimeConfig.Fingerprint(config));
		}
		catch (Exception exception)
		{
			Log.Error("[sts2-replica] broadcasting the config failed: " + exception);
		}
	}

	private static void OnConfigRequested(ReplicaConfigRequestMessage message, ulong senderId)
	{
		INetGameService? service = RunManager.Instance.NetService;
		if (service == null || service.Type != NetGameType.Host)
		{
			return;
		}
		// Every ask is answered with a fresh broadcast, which is what lets a late joiner catch up.
		Log.Info("[sts2-replica] config requested by player " + senderId);
		BroadcastConfig(service);
	}

	private static void OnConfigReceived(ReplicaConfigMessage message, ulong senderId)
	{
		if (!ReplicaRuntimeConfig.TryAdoptFromHost(message.ToConfig(), senderId))
		{
			return;
		}
		_adoptedHostConfig = true;
		RunState? state = _runState;
		if (state != null)
		{
			// A client suppresses the RunStarted grant because it cannot know the host's value yet.
			ReplicaMod.OnHostConfigAdopted(state);
		}
	}

	/// <summary>
	/// Self-heal for a client whose ask raced the host's handler registration: vanilla drops a message
	/// with no handler (NetMessageBus.cs:88), and without this the peer would keep its local numbers for
	/// the whole run. One attempt per run, made from gameplay rather than from the run-start hook.
	/// </summary>
	public static void RequestConfigIfMissing()
	{
		if (_configReRequested || _adoptedHostConfig || !ReplicaRuntimeConfig.IsMultiplayerClient)
		{
			return;
		}
		INetGameService? service = RunManager.Instance.NetService;
		if (service == null || service.Type != NetGameType.Client)
		{
			return;
		}
		// Set before sending: a refused send must not become one attempt per card reward.
		_configReRequested = true;
		try
		{
			service.SendMessage(new ReplicaConfigRequestMessage());
			Log.Warn("[sts2-replica] host config missing at first card reward; re-requested (local values in effect until it arrives)");
		}
		catch (Exception exception)
		{
			Log.Error("[sts2-replica] re-requesting the host config failed: " + exception);
		}
	}

	private static void LogMessageIdsOnce()
	{
		if (_loggedMessageIds)
		{
			return;
		}
		// MessageTypes.Initialize runs after ModManager.Initialize (OneTimeInitialization.cs:59 then :84),
		// so the ids are unreadable during Init and are read here instead, once, inside a guard.
		if (!TryLogMessageId(typeof(ReplicaConfigMessage), () => MessageTypes.TypeToId<ReplicaConfigMessage>()))
		{
			return;
		}
		if (!TryLogMessageId(typeof(ReplicaConfigRequestMessage), () => MessageTypes.TypeToId<ReplicaConfigRequestMessage>()))
		{
			return;
		}
		_loggedMessageIds = true;
	}

	private static bool TryLogMessageId(Type messageType, Func<int> id)
	{
		try
		{
			Log.Info("[sts2-replica] message type " + messageType.Name + " id=" + id());
			return true;
		}
		catch (Exception exception)
		{
			// A missing type reaches here as whatever the caches throw: MessageTypes before Initialize
			// (MessageTypes.cs:21-24, InvalidOperationException) and NetTypeCache's plain
			// Dictionary<Type,int> indexer (NetTypeCache.cs:14, :54, KeyNotFoundException). Nothing may
			// escape the RunStarted hook, and the next run start retries instead of losing the id.
			Log.Warn("[sts2-replica] message type not registered: " + messageType.Name + ": " + exception.Message);
			return false;
		}
	}
}
