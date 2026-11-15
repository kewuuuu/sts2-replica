using System;
using System.Reflection;
using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Runs;
using Sts2Replica.Config;
using Sts2Replica.Localization;
using Sts2Replica.Models;

namespace Sts2Replica;

[ModInitializer("Init")]
public static class ReplicaMod
{
    public const string ModId = "sts2-replica";

    public static void Init()
    {
        ReplicaConfig config = ReplicaConfigStore.Load();
        PatchLocalization();
        ModHelper.AddModelToPool<SharedRelicPool, GoodCardReplica>();
        RunManager.Instance.RunStarted += OnRunStarted;
        Log.Info("[sts2-replica] initialized; GoodCardReplica queued for SharedRelicPool, "
            + "charge_required=" + config.ChargeRequired
            + ", offer_count=" + config.OfferCount
            + ", start_with_relic=" + config.StartWithRelic);
    }

    /// <summary>A DLL-only mod owns its Harmony instance: ModManager only auto-patches assemblies without an initializer.</summary>
    private static void PatchLocalization()
    {
        try
        {
            new Harmony(ModId).PatchAll(Assembly.GetExecutingAssembly());
        }
        catch (Exception exception)
        {
            Log.Error("[sts2-replica] localization patch failed: " + exception);
        }
    }

    /// <summary>
    /// Every client derives the same starting state from the run seed, so each player in a
    /// multiplayer run is granted the relic locally (same shape as CursedRun.AfterActEntered).
    /// </summary>
    private static void OnRunStarted(RunState state)
    {
        if (!ReplicaConfigStore.Current.StartWithRelic)
        {
            return;
        }
        TaskHelper.RunSafely(GrantStartingRelics(state));
    }

    private static async Task GrantStartingRelics(RunState state)
    {
        try
        {
            RelicModel template = ModelDb.Relic<GoodCardReplica>();
            foreach (Player player in state.Players)
            {
                if (player.GetRelic<GoodCardReplica>() != null)
                {
                    continue;
                }
                await RelicCmd.Obtain(template.ToMutable(), player);
            }
        }
        catch (Exception exception)
        {
            Log.Error("[sts2-replica] granting the starting relic failed: " + exception);
        }
    }
}
