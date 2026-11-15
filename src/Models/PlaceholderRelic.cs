using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Models;

namespace Sts2Replica.Models;

// Icon assets are not shipped yet: RelicModel derives icon paths from Id.Entry and
// ResolvedBigIconPath falls back to powers/missing_power.png when they are absent.
public sealed class PlaceholderRelic : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Common;

    public override bool ShouldReceiveCombatHooks => false;
}
