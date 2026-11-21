using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using Sts2Replica.Models;

namespace Sts2Replica.Art;

/// <summary>
/// RelicModel.Icon / IconOutline / BigIcon are plain non-virtual getters (RelicModel.cs:144-148) reading
/// res:// paths built by ImageHelper, which a DLL-only mod cannot populate: the atlas loader answers with
/// its "NOPE" placeholder. ReplicaMod.PatchAll discovers these classes, so no entry point change is needed.
/// Only GoodCardReplica is replaced; every other relic, and every failure path, keeps the vanilla texture.
/// No HarmonyPriority here on purpose: another content mod may postfix the same getters (RitsuLib does), and
/// the safe winner is the node-level patch in RelicNodeArtPatch, which runs after the whole getter chain.
/// </summary>
[HarmonyPatch(typeof(RelicModel), nameof(RelicModel.Icon), MethodType.Getter)]
internal static class RelicIconPatch
{
	private static void Postfix(RelicModel __instance, ref Texture2D __result)
	{
		if (__instance is not GoodCardReplica)
		{
			return;
		}
		Texture2D? texture = ReplicaArt.Icon(__instance);
		if (texture != null)
		{
			__result = texture;
		}
	}
}

[HarmonyPatch(typeof(RelicModel), nameof(RelicModel.IconOutline), MethodType.Getter)]
internal static class RelicIconOutlinePatch
{
	private static void Postfix(RelicModel __instance, ref Texture2D __result)
	{
		if (__instance is not GoodCardReplica)
		{
			return;
		}
		Texture2D? texture = ReplicaArt.IconOutline(__instance);
		if (texture != null)
		{
			__result = texture;
		}
	}
}

[HarmonyPatch(typeof(RelicModel), nameof(RelicModel.BigIcon), MethodType.Getter)]
internal static class RelicBigIconPatch
{
	private static void Postfix(RelicModel __instance, ref Texture2D __result)
	{
		if (__instance is not GoodCardReplica)
		{
			return;
		}
		Texture2D? texture = ReplicaArt.BigIcon(__instance);
		if (texture != null)
		{
			__result = texture;
		}
	}
}
