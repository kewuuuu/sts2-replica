using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using Sts2Replica.Core;

namespace Sts2Replica.Engine;

/// <summary>Engine adapter: maps a live <see cref="CardModel"/> onto the engine-free <see cref="CardFact"/>.</summary>
public static class CardFactMapper
{
	public static CardFact ToFact(CardModel card)
	{
		return new CardFact(card.Id.Entry, TierOf(card));
	}

	public static CardTier TierOf(CardModel card)
	{
		CardPoolModel pool = card.Pool;
		if (pool is ColorlessCardPool)
		{
			return CardTier.Colorless;
		}
		if (pool is CurseCardPool)
		{
			return CardTier.Curse;
		}
		if (pool is StatusCardPool)
		{
			return CardTier.Status;
		}
		if (pool is QuestCardPool)
		{
			return CardTier.Quest;
		}
		return card.Rarity switch
		{
			CardRarity.Basic => CardTier.Basic,
			CardRarity.Common => CardTier.Common,
			CardRarity.Uncommon => CardTier.Uncommon,
			CardRarity.Rare => CardTier.Rare,
			CardRarity.Status => CardTier.Status,
			CardRarity.Quest => CardTier.Quest,
			CardRarity.Curse => CardTier.Curse,
			_ => CardTier.Other
		};
	}
}
