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
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Saves.Runs;
using Sts2Replica.Config;
using Sts2Replica.Engine;
using Sts2Replica.Net;

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

	// A charge-less run (charge_required = 0) has no progress to show; the vanilla-style 0 badge would only confuse.
	public override bool ShowCounter => ReplicaRuntimeConfig.ForThisRun.ChargeRequired > 0;

	/// <summary>
	/// Fallback values for the description placeholders, so a failed RelicDescriptionPatch shows numbers
	/// instead of literal braces. These are built once (RelicModel.cs:291-305) - RelicDescriptionPatch is
	/// what keeps the text in step with the run's frozen settings.
	/// </summary>
	protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
	{
		new DynamicVar("Charges", ReplicaRuntimeConfig.Effective.ChargeRequired),
		new DynamicVar("Options", ReplicaRuntimeConfig.Effective.OfferCount),
		new DynamicVar("Picks", ReplicaRuntimeConfig.Effective.SamplePicks)
	};

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
		// The numbers this run froze at run start: a host or a singleplayer run froze its own file, a client
		// froze the host's copy. A settings change made mid-run deliberately does not move this value, so
		// both ends keep answering the same question the same way.
		ReplicaConfig config = ReplicaRuntimeConfig.ForThisRun;
		try
		{
			// No-op once the host's config is in hand; a client's single ask can be dropped while the host
			// has no handler yet (NetMessageBus.cs:88), and this is the first place the values are used.
			ReplicaConfigSync.RequestConfigIfMissing();
		}
		catch (Exception exception)
		{
			// A broken sync must never cost the player a card reward.
			Log.Error("[sts2-replica] config self-check failed: " + exception);
		}
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
			ReplicaConfig config = ReplicaRuntimeConfig.ForThisRun;
			Player owner = base.Owner;
			Rng rng = new Rng(owner, base.Id, (ulong)owner.RunState.TotalFloor);
			List<CardCreationResult> candidates = await ReplicaDeckSampler.BuildCandidatesAsync(owner, config, rng);
			if (candidates.Count == 0)
			{
				Charges = previous;
				return;
			}
			int maxPicks = MaxPicks(config, previous, candidates.Count);
			if (maxPicks < 1)
			{
				// The button is only injected while one sample is affordable, so this cannot happen through
				// the reward screen; opening a 0-slot grid would only cancel itself, so keep the charge.
				Charges = previous;
				Log.Error("[sts2-replica] sampling requested with no affordable charge; nothing was offered");
				return;
			}
			LocString prompt = new LocString("relics", base.Id.Entry + ".selectionScreenPrompt");
			// min 0 / max maxPicks keeps the "take up to maxPicks, or cancel and get the charge back" contract:
			// min != max forces manual confirmation (CardSelectorPrefs.cs:79), so the confirm button is what
			// ends the choice, and SeaGlass.cs:89 is the vanilla precedent for a multi-select grid.
			CardSelectorPrefs prefs = new CardSelectorPrefs(prompt, 0, maxPicks)
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
			if (picked.Count > maxPicks)
			{
				// The grid stops at MaxSelect; this only keeps the charge arithmetic from paying for cards
				// the player could not have selected.
				picked = picked.Take(maxPicks).ToList();
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
			Charges = RemainingCharges(config, previous, picked.Count);
		}
		catch (Exception exception)
		{
			Charges = previous;
			Log.Error("[sts2-replica] sampling failed: " + exception);
		}
	}

	/// <summary>
	/// How many cards one sample may take: the configured cap, the number of candidates, and what the
	/// charge pays for. <c>charge_required = 0</c> is the free mode and skips the division.
	/// </summary>
	private static int MaxPicks(ReplicaConfig config, int charges, int candidateCount)
	{
		int picks = Math.Min(config.SamplePicks, candidateCount);
		if (config.ChargeRequired > 0)
		{
			picks = Math.Min(picks, charges / config.ChargeRequired);
		}
		return Math.Max(0, picks);
	}

	/// <summary>
	/// The charge left after taking <paramref name="taken"/> cards: one <c>charge_required</c> per card, so
	/// an extra card costs an extra charge. Free mode keeps the counter cleared to 0, as it was before
	/// multi-pick.
	/// </summary>
	private static int RemainingCharges(ReplicaConfig config, int previous, int taken)
	{
		if (config.ChargeRequired <= 0)
		{
			return 0;
		}
		return Math.Max(0, previous - config.ChargeRequired * taken);
	}
}
