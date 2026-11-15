using System;
using HarmonyLib;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Logging;

namespace Sts2Replica.Localization;

/// <summary>
/// Runs after LocManager.Initialize (LocManager.cs:195-203), which is the earliest point where
/// the tables exist but no relic UI has read them yet.
/// </summary>
[HarmonyPatch(typeof(LocManager), nameof(LocManager.Initialize))]
internal static class LocBootstrap
{
	private static void Postfix()
	{
		try
		{
			ReplicaLoc.InjectAll();
			LocManager.Instance.SubscribeToLocaleChange(ReplicaLoc.InjectAll);
		}
		catch (Exception exception)
		{
			Log.Error("[sts2-replica] localization bootstrap failed: " + exception);
		}
	}
}
