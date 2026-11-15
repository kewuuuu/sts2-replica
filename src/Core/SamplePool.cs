using System;
using System.Collections.Generic;
using Sts2Replica.Config;

namespace Sts2Replica.Core;

/// <summary>Engine-free view of one deck card: identity for de-duplication plus its weight bucket.</summary>
public readonly record struct CardFact(string Key, CardTier Tier);

/// <summary>
/// Pure sampling rules: which cards may be offered and how a weighted draw without
/// replacement is performed. No engine types, no randomness source of its own — the
/// caller passes the run-synchronised <c>nextInt</c>.
/// </summary>
public static class SamplePool
{
	public static bool IsEligible(CardFact card, ReplicaConfig config)
	{
		return card.Tier switch
		{
			CardTier.Other => false,
			CardTier.Basic => config.IncludeBasic,
			CardTier.Status => config.IncludeStatus,
			CardTier.Quest => config.IncludeQuest,
			_ => true
		};
	}

	/// <summary>Basic cards count as commons (docs/spec.md). Status/quest share the common weight.</summary>
	public static int WeightOf(CardTier tier, ReplicaConfig config)
	{
		return tier switch
		{
			CardTier.Common => config.WeightCommon,
			CardTier.Basic => config.WeightCommon,
			CardTier.Status => config.WeightCommon,
			CardTier.Quest => config.WeightCommon,
			CardTier.Uncommon => config.WeightUncommon,
			CardTier.Rare => config.WeightRare,
			CardTier.Colorless => config.WeightColorless,
			CardTier.Curse => config.WeightCurse,
			_ => 0
		};
	}

	/// <summary>
	/// Weight per deck entry; 0 means "not offered". When every configured weight is 0
	/// the table degrades to equal probability instead of offering nothing.
	/// </summary>
	public static IReadOnlyList<int> BuildWeights(IReadOnlyList<CardFact> cards, ReplicaConfig config)
	{
		bool uniformFallback = config.TotalWeight <= 0;
		int[] weights = new int[cards.Count];
		HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
		for (int i = 0; i < cards.Count; i++)
		{
			CardFact card = cards[i];
			if (!IsEligible(card, config))
			{
				continue;
			}
			if (config.Dedupe && !seen.Add(card.Key))
			{
				continue;
			}
			weights[i] = uniformFallback ? 1 : WeightOf(card.Tier, config);
		}
		return weights;
	}

	public static int CountEligible(IReadOnlyList<CardFact> cards, ReplicaConfig config)
	{
		IReadOnlyList<int> weights = BuildWeights(cards, config);
		int count = 0;
		for (int i = 0; i < weights.Count; i++)
		{
			if (weights[i] > 0)
			{
				count++;
			}
		}
		return count;
	}

	/// <summary>
	/// Weighted draw of at most <paramref name="count"/> distinct indices.
	/// <paramref name="nextInt"/> must be a run-synchronised generator.
	/// </summary>
	public static List<int> Pick(IReadOnlyList<CardFact> cards, ReplicaConfig config, int count, Func<int, int> nextInt)
	{
		int wanted = Math.Clamp(count, 0, ReplicaConfig.OfferCountMax);
		IReadOnlyList<int> weights = BuildWeights(cards, config);
		List<int> pool = new List<int>();
		List<long> poolWeights = new List<long>();
		for (int i = 0; i < weights.Count; i++)
		{
			if (weights[i] <= 0)
			{
				continue;
			}
			pool.Add(i);
			poolWeights.Add(weights[i]);
		}
		List<int> picked = new List<int>();
		for (int n = 0; n < wanted && pool.Count > 0; n++)
		{
			long total = 0;
			for (int i = 0; i < poolWeights.Count; i++)
			{
				total += poolWeights[i];
			}
			int slot;
			if (total <= 0)
			{
				slot = nextInt(pool.Count);
			}
			else
			{
				long roll = nextInt((int)Math.Min(total, int.MaxValue));
				slot = 0;
				while (slot < pool.Count - 1 && roll >= poolWeights[slot])
				{
					roll -= poolWeights[slot];
					slot++;
				}
			}
			slot = Math.Clamp(slot, 0, pool.Count - 1);
			picked.Add(pool[slot]);
			pool.RemoveAt(slot);
			poolWeights.RemoveAt(slot);
		}
		return picked;
	}
}
