using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Multiplayer.Serialization;
using MegaCrit.Sts2.Core.Multiplayer.Transport;
using Sts2Replica.Config;

namespace Sts2Replica.Net;

/// <summary>
/// Host -> clients: the mod parameters this run is played with. Mod message types are discovered and
/// id-assigned by name (MessageTypes.cs:13-19, NetTypeCache.cs:22), so this name must stay unique
/// across all installed mods.
/// </summary>
public class ReplicaConfigMessage : INetMessage, IPacketSerializable
{
	public int ChargeRequired { get; set; }

	public int OfferCount { get; set; }

	public int WeightCommon { get; set; }

	public int WeightUncommon { get; set; }

	public int WeightRare { get; set; }

	public int WeightColorless { get; set; }

	public int WeightCurse { get; set; }

	public bool StartWithRelic { get; set; }

	public bool IncludeBasic { get; set; }

	public bool IncludeStatus { get; set; }

	public bool IncludeQuest { get; set; }

	public bool Dedupe { get; set; }

	// The host is the only sender, and NetHostGameService only echoes messages it receives
	// (NetHostGameService.cs:159), so this flag has no local effect here.
	public bool ShouldBroadcast => true;

	// A dropped config would leave the two ends judging card rewards with different numbers.
	public NetTransferMode Mode => NetTransferMode.Reliable;

	// Info: one message per run start, which is "very infrequent" by the interface's own definition
	// (INetMessage.cs:20-22), and the line is what proves the sync reached the other end.
	public LogLevel LogLevel => LogLevel.Info;

	public bool ShouldBuffer => true;

	public static ReplicaConfigMessage From(ReplicaConfig config)
	{
		return new ReplicaConfigMessage
		{
			ChargeRequired = config.ChargeRequired,
			OfferCount = config.OfferCount,
			WeightCommon = config.WeightCommon,
			WeightUncommon = config.WeightUncommon,
			WeightRare = config.WeightRare,
			WeightColorless = config.WeightColorless,
			WeightCurse = config.WeightCurse,
			StartWithRelic = config.StartWithRelic,
			IncludeBasic = config.IncludeBasic,
			IncludeStatus = config.IncludeStatus,
			IncludeQuest = config.IncludeQuest,
			Dedupe = config.Dedupe
		};
	}

	public ReplicaConfig ToConfig()
	{
		return new ReplicaConfig
		{
			ChargeRequired = ChargeRequired,
			OfferCount = OfferCount,
			WeightCommon = WeightCommon,
			WeightUncommon = WeightUncommon,
			WeightRare = WeightRare,
			WeightColorless = WeightColorless,
			WeightCurse = WeightCurse,
			StartWithRelic = StartWithRelic,
			IncludeBasic = IncludeBasic,
			IncludeStatus = IncludeStatus,
			IncludeQuest = IncludeQuest,
			Dedupe = Dedupe
		};
	}

	public void Serialize(PacketWriter writer)
	{
		// The order below is the wire format: append only, and keep Deserialize in step with it.
		writer.WriteInt(ChargeRequired);
		writer.WriteInt(OfferCount);
		writer.WriteInt(WeightCommon);
		writer.WriteInt(WeightUncommon);
		writer.WriteInt(WeightRare);
		writer.WriteInt(WeightColorless);
		writer.WriteInt(WeightCurse);
		writer.WriteBool(StartWithRelic);
		writer.WriteBool(IncludeBasic);
		writer.WriteBool(IncludeStatus);
		writer.WriteBool(IncludeQuest);
		writer.WriteBool(Dedupe);
	}

	public void Deserialize(PacketReader reader)
	{
		ChargeRequired = reader.ReadInt();
		OfferCount = reader.ReadInt();
		WeightCommon = reader.ReadInt();
		WeightUncommon = reader.ReadInt();
		WeightRare = reader.ReadInt();
		WeightColorless = reader.ReadInt();
		WeightCurse = reader.ReadInt();
		StartWithRelic = reader.ReadBool();
		IncludeBasic = reader.ReadBool();
		IncludeStatus = reader.ReadBool();
		IncludeQuest = reader.ReadBool();
		Dedupe = reader.ReadBool();
	}
}
