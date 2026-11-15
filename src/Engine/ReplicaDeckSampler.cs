using System.Collections.Generic;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Runs;
using Sts2Replica.Config;
using Sts2Replica.Core;

namespace Sts2Replica.Engine;

/// <summary>
/// Builds the sampled reward candidates: pick canonical templates out of the owner's deck,
/// instantiate fresh cards, run the ordinary reward upgrade roll and the reward-modifying hooks.
/// </summary>
public static class ReplicaDeckSampler
{
	/// <summary>Same scaling CardFactory uses for post-act upgrade odds (CardFactory.cs:25).</summary>
	private static readonly decimal UpgradedCardOddScaling =
		AscensionHelper.GetValueIfAscension(AscensionLevel.Scarcity, 0.125m, 0.25m);

	/// <summary>True when the deck can actually yield at least one candidate (weight, include-flag and de-dup rules applied).</summary>
	public static bool HasEligibleTemplate(Player player, ReplicaConfig config)
	{
		IReadOnlyList<CardModel> deck = player.Deck.Cards;
		List<CardFact> facts = new List<CardFact>(deck.Count);
		foreach (CardModel card in deck)
		{
			facts.Add(CardFactMapper.ToFact(card));
		}
		return SamplePool.CountEligible(facts, config) > 0;
	}

	/// <summary>
	/// Weighted, distinct template pick followed by fresh card creation, upgrade roll and reward hooks.
	/// <paramref name="rng"/> must be derived from run state so every client samples identically.
	/// </summary>
	public static async Task<List<CardCreationResult>> BuildCandidatesAsync(Player player, ReplicaConfig config, Rng rng)
	{
		List<CardCreationResult> results = new List<CardCreationResult>();
		foreach (CardModel template in PickTemplates(player, config, rng))
		{
			CardModel card = player.RunState.CreateCard(template, player);
			RollForUpgrade(player, card, rng);
			results.Add(new CardCreationResult(card));
		}
		if (results.Count > 0)
		{
			CardCreationOptions options = new CardCreationOptions(
				new[] { player.Character.CardPool },
				CardCreationSource.Other,
				CardRarityOddsType.RegularEncounter);
			if (Hook.TryModifyCardRewardOptions(player.RunState, player, results, options, out List<AbstractModel> modifiers))
			{
				await Hook.AfterModifyingCardRewardOptions(player.RunState, modifiers);
			}
		}
		return results;
	}

	private static List<CardModel> PickTemplates(Player player, ReplicaConfig config, Rng rng)
	{
		IReadOnlyList<CardModel> deck = player.Deck.Cards;
		List<CardFact> facts = new List<CardFact>(deck.Count);
		foreach (CardModel card in deck)
		{
			facts.Add(CardFactMapper.ToFact(card));
		}
		List<int> picked = SamplePool.Pick(facts, config, config.OfferCount, rng.NextInt);
		List<CardModel> templates = new List<CardModel>(picked.Count);
		foreach (int index in picked)
		{
			templates.Add(deck[index].CanonicalInstance);
		}
		return templates;
	}

	/// <summary>
	/// Mirrors CardFactory.RollForUpgrade (CardFactory.cs:293-311), which is private:
	/// draw first, then odds = actIndex * scaling for non-rares, then the hook, then compare.
	/// The reward path passes baseChance 0m.
	/// </summary>
	private static void RollForUpgrade(Player player, CardModel card, Rng rng)
	{
		float roll = rng.NextFloat();
		if (!card.IsUpgradable)
		{
			return;
		}
		decimal odds = 0m;
		if (card.Rarity != CardRarity.Rare)
		{
			odds += (decimal)player.RunState.CurrentActIndex * UpgradedCardOddScaling;
		}
		odds = Hook.ModifyCardRewardUpgradeOdds(player.RunState, player, card, odds);
		if ((decimal)roll <= odds)
		{
			CardCmd.Upgrade(card);
		}
	}
}
