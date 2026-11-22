using System;
using System.Text.Json;

namespace Sts2Replica.Config;

/// <summary>
/// Mod settings. Parsing is pure: any missing, malformed or out-of-range value falls back
/// to the default from docs/spec.md. Values are clamped, never trusted.
/// </summary>
public sealed class ReplicaConfig
{
	/// <summary>
	/// Upper bound for the candidate count. <c>CardSelectCmd.FromSimpleGridForRewards</c> imposes no count limit of
	/// its own; 1..3 is kept because the grid selection UI with more than three offers is unverified in game.
	/// </summary>
	public const int OfferCountMax = 3;
	/// <summary>
	/// Upper bound for how many candidates one sample may take. The candidate list itself is capped at
	/// <see cref="OfferCountMax"/>, so 1..3 cannot exceed what the grid can offer.
	/// </summary>
	public const int SamplePicksMax = 3;
	public const int DefaultChargeRequired = 3;
	public const int DefaultOfferCount = 2;
	public const int DefaultSamplePicks = 1;
	public const int DefaultWeightCommon = 6;
	public const int DefaultWeightUncommon = 4;
	public const int DefaultWeightRare = 2;
	public const int DefaultWeightColorless = 1;
	public const int DefaultWeightCurse = 3;
	private const int MaxWeight = 1000000;

	public int ChargeRequired { get; init; } = DefaultChargeRequired;

	public int OfferCount { get; init; } = DefaultOfferCount;

	/// <summary>How many candidates one sample may take at most; the charge pays for each card taken.</summary>
	public int SamplePicks { get; init; } = DefaultSamplePicks;

	public int WeightCommon { get; init; } = DefaultWeightCommon;

	public int WeightUncommon { get; init; } = DefaultWeightUncommon;

	public int WeightRare { get; init; } = DefaultWeightRare;

	public int WeightColorless { get; init; } = DefaultWeightColorless;

	public int WeightCurse { get; init; } = DefaultWeightCurse;

	public bool StartWithRelic { get; init; }

	public bool IncludeBasic { get; init; } = true;

	public bool IncludeStatus { get; init; }

	public bool IncludeQuest { get; init; }

	public bool Dedupe { get; init; } = true;

	public static ReplicaConfig Default => new ReplicaConfig();

	/// <summary>Sum of the configured rarity weights; 0 means "fall back to equal probability".</summary>
	public int TotalWeight => WeightCommon + WeightUncommon + WeightRare + WeightColorless + WeightCurse;

	/// <summary>
	/// The values a peer may actually use. Only the network path needs this: a value read from a file
	/// goes through <see cref="Parse"/>, but one that arrives as a message has bypassed it.
	/// </summary>
	public ReplicaConfig Normalized()
	{
		return new ReplicaConfig
		{
			ChargeRequired = Math.Max(0, ChargeRequired),
			OfferCount = Math.Clamp(OfferCount, 1, OfferCountMax),
			SamplePicks = Math.Clamp(SamplePicks, 1, SamplePicksMax),
			WeightCommon = Math.Clamp(WeightCommon, 0, MaxWeight),
			WeightUncommon = Math.Clamp(WeightUncommon, 0, MaxWeight),
			WeightRare = Math.Clamp(WeightRare, 0, MaxWeight),
			WeightColorless = Math.Clamp(WeightColorless, 0, MaxWeight),
			WeightCurse = Math.Clamp(WeightCurse, 0, MaxWeight),
			StartWithRelic = StartWithRelic,
			IncludeBasic = IncludeBasic,
			IncludeStatus = IncludeStatus,
			IncludeQuest = IncludeQuest,
			Dedupe = Dedupe
		};
	}

	public static ReplicaConfig Parse(string? json)
	{
		ReplicaConfig defaults = Default;
		if (string.IsNullOrWhiteSpace(json))
		{
			return defaults;
		}
		JsonDocument document;
		try
		{
			document = JsonDocument.Parse(json, new JsonDocumentOptions
			{
				AllowTrailingCommas = true,
				CommentHandling = JsonCommentHandling.Skip
			});
		}
		catch (JsonException)
		{
			return defaults;
		}
		using (document)
		{
			JsonElement root = document.RootElement;
			if (root.ValueKind != JsonValueKind.Object)
			{
				return defaults;
			}
			return new ReplicaConfig
			{
				ChargeRequired = Math.Max(0, ReadInt(root, "charge_required", defaults.ChargeRequired)),
				OfferCount = Math.Clamp(ReadInt(root, "offer_count", defaults.OfferCount), 1, OfferCountMax),
				SamplePicks = Math.Clamp(ReadInt(root, "sample_picks", defaults.SamplePicks), 1, SamplePicksMax),
				WeightCommon = ReadWeight(root, "weight_common", defaults.WeightCommon),
				WeightUncommon = ReadWeight(root, "weight_uncommon", defaults.WeightUncommon),
				WeightRare = ReadWeight(root, "weight_rare", defaults.WeightRare),
				WeightColorless = ReadWeight(root, "weight_colorless", defaults.WeightColorless),
				WeightCurse = ReadWeight(root, "weight_curse", defaults.WeightCurse),
				StartWithRelic = ReadBool(root, "start_with_relic", defaults.StartWithRelic),
				IncludeBasic = ReadBool(root, "include_basic", defaults.IncludeBasic),
				IncludeStatus = ReadBool(root, "include_status", defaults.IncludeStatus),
				IncludeQuest = ReadBool(root, "include_quest", defaults.IncludeQuest),
				Dedupe = ReadBool(root, "dedupe", defaults.Dedupe)
			};
		}
	}

	private static bool TryFind(JsonElement root, string name, out JsonElement value)
	{
		foreach (JsonProperty property in root.EnumerateObject())
		{
			if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
			{
				value = property.Value;
				return true;
			}
		}
		value = default;
		return false;
	}

	private static int ReadInt(JsonElement root, string name, int fallback)
	{
		if (!TryFind(root, name, out JsonElement value) || value.ValueKind != JsonValueKind.Number)
		{
			return fallback;
		}
		return value.TryGetInt32(out int parsed) ? parsed : fallback;
	}

	private static int ReadWeight(JsonElement root, string name, int fallback)
	{
		return Math.Clamp(ReadInt(root, name, fallback), 0, MaxWeight);
	}

	private static bool ReadBool(JsonElement root, string name, bool fallback)
	{
		if (!TryFind(root, name, out JsonElement value))
		{
			return fallback;
		}
		return value.ValueKind switch
		{
			JsonValueKind.True => true,
			JsonValueKind.False => false,
			JsonValueKind.Number => value.TryGetInt32(out int number) && number != 0,
			JsonValueKind.String => bool.TryParse(value.GetString(), out bool parsed) ? parsed : fallback,
			_ => fallback
		};
	}
}
