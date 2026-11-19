using HarmonyLib;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using Sts2Replica.Config;

namespace Sts2Replica.Models;

/// <summary>
/// RelicModel.Description is private and DynamicVars is built once per model instance
/// (RelicModel.cs:69, :291-305, cloned at :547), so the values baked into CanonicalVars are the ones
/// the game held when the canonical model was constructed. This postfix rewrites them on every read
/// instead, from the settings this run froze: the tooltip (RelicModel.cs:358-377) and the relic
/// compendium (NInspectRelicScreen.cs:408) both go through DynamicDescription, so both stay in step.
/// LocString.AddObj overwrites an existing key (LocString.cs:161-169), which is what makes this a
/// refresh rather than a collision.
/// </summary>
[HarmonyPatch(typeof(RelicModel), nameof(RelicModel.DynamicDescription), MethodType.Getter)]
internal static class RelicDescriptionPatch
{
	private static void Postfix(RelicModel __instance, LocString __result)
	{
		if (__instance is not GoodCardReplica)
		{
			return;
		}
		ReplicaConfig config = ReplicaRuntimeConfig.ForThisRun;
		__result.Add("Charges", (decimal)config.ChargeRequired);
		__result.Add("Options", (decimal)config.OfferCount);
	}
}
