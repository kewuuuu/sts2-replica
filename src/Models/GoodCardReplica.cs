using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.CardRewardAlternatives;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Entities.Rewards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Saves.Runs;
using Sts2Replica.Config;
using Sts2Replica.Engine;

namespace Sts2Replica.Models;

/// <summary>
/// GoodCardReplica: a card reward can be spent to charge the relic; once charged, the same
/// reward slot instead becomes a sample button that offers deck cards as a reward.
/// </summary>
public sealed class GoodCardReplica : RelicModel
{
	private int _charges;

	public override RelicRarity Rarity => RelicRarity.Common;

	public override bool ShouldReceiveCombatHooks => false;

	public override bool ShowCounter => true;

	public override int DisplayAmount => _charges;

	[SavedProperty]
	public int Charges
	{
		get
		{
			return _charges;
		}
		set
		{
			AssertMutable();
			_charges = value;
			InvokeDisplayAmountChanged();
		}
	}

	public override bool TryModifyCardRewardAlternatives(Player player, CardReward cardReward, List<CardRewardAlternative> alternatives)
	{
		if (base.Owner != player)
		{
			return false;
		}
		if (alternatives.Count >= 2)
		{
			return false;
		}
		// PaelsWing appends its own option with no count check (Models.Relics/PaelsWing.cs:79) and
		// CardRewardAlternative.Generate throws once three survive (CardRewardAlternative.cs:74-77).
		// Listener order decides who trips it, and every peer trips it at the same time, so we yield the slot.
		if (player.GetRelic<PaelsWing>() != null)
		{
			return false;
		}
		ReplicaConfig config = ReplicaConfigStore.Current;
		// Always inject exactly one option, and never let a local setting decide whether one exists:
		// peers rebuild this list from their own config and sync only an index into it
		// (CardReward.cs:256), so a missing entry turns a pick into a bad-index error on the other side.
		if (Charges >= config.ChargeRequired && ReplicaDeckSampler.HasEligibleTemplate(player, config))
		{
			alternatives.Add(new CardRewardAlternative("SAMPLE", OnSample, PostAlternateCardRewardAction.EndSelectionAndCompleteReward));
			return true;
		}
		alternatives.Add(new CardRewardAlternative("MOLD", OnMold, PostAlternateCardRewardAction.EndSelectionAndCompleteReward));
		return true;
	}

	private Task OnMold()
	{
		Charges++;
		Flash();
		return Task.CompletedTask;
	}

	private async Task OnSample()
	{
		int previous = Charges;
		Charges = 0;
		try
		{
			ReplicaConfig config = ReplicaConfigStore.Current;
			Player owner = base.Owner;
			Rng rng = new Rng(owner, base.Id, (ulong)owner.RunState.TotalFloor);
			List<CardCreationResult> candidates = await ReplicaDeckSampler.BuildCandidatesAsync(owner, config, rng);
			if (candidates.Count == 0)
			{
				Charges = previous;
				return;
			}
			LocString prompt = new LocString("relics", base.Id.Entry + ".selectionScreenPrompt");
			// min 0 / max 1 keeps the "pick exactly one, or cancel and get the charge back" contract:
			// min != max forces manual confirmation (CardSelectorPrefs.cs), Cancelable mirrors CardUpgradeReward.cs:46.
			CardSelectorPrefs prefs = new CardSelectorPrefs(prompt, 0, 1)
			{
				Cancelable = true
			};
			IEnumerable<CardModel> chosen = await CardSelectCmd.FromSimpleGridForRewards(
				new BlockingPlayerChoiceContext(), candidates, owner, prefs);
			List<CardModel> picked = chosen.ToList();
			if (picked.Count == 0)
			{
				Charges = previous;
				return;
			}
			foreach (CardModel card in picked)
			{
				CardPileAddResult added = await CardPileCmd.Add(card, PileType.Deck);
				if (!added.success)
				{
					// A failed deck add means the player keeps nothing (CardReward.cs:279-281 only continues on
					// success), so the charge must not be spent for it.
					Charges = previous;
					Log.Error($"[sts2-replica] sampled card {card.Id} was not added to the deck (success=false); charge restored to {previous}.");
					return;
				}
				CardCmd.PreviewCardPileAdd(added);
			}
		}
		catch (Exception exception)
		{
			Charges = previous;
			Log.Error("[sts2-replica] sampling failed: " + exception);
		}
	}
}
