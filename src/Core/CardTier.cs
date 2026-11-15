namespace Sts2Replica.Core;

/// <summary>Deck cards bucketed by the reward-weight table in docs/spec.md.</summary>
public enum CardTier
{
	Common,
	Uncommon,
	Rare,
	Colorless,
	Curse,
	Basic,
	Status,
	Quest,
	Other
}
