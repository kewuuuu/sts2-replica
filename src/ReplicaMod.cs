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
using Sts2Replica.Art;
using Sts2Replica.Config;
using Sts2Replica.Localization;
using Sts2Replica.Models;
using Sts2Replica.Net;

namespace Sts2Replica;

[ModInitializer("Init")]
public static class ReplicaMod
{
    public const string ModId = "sts2-replica";

    public static void Init()
    {
        ReplicaConfig config = ReplicaConfigStore.Load();
#if RITSULIB
        // 仅当本机构建引用了 RitsuLib 时才有这段代码；运行期 RitsuLib 不在也只会留一条警告。
        Integration.RitsuLibSettingsPage.TryRegister();
#endif
        PatchLocalization();
        ModHelper.AddModelToPool<SharedRelicPool, GoodCardReplica>();
        RunManager.Instance.RunStarted += OnRunStarted;
        Log.Info("[sts2-replica] initialized; GoodCardReplica queued for SharedRelicPool, "
            + "charge_required=" + config.ChargeRequired
            + ", offer_count=" + config.OfferCount
            + ", start_with_relic=" + config.StartWithRelic);
        Log.Info("[sts2-replica] local config fingerprint " + ReplicaRuntimeConfig.Fingerprint(config));
    }

    /// <summary>A DLL-only mod owns its Harmony instance: ModManager only auto-patches assemblies without an initializer.</summary>
    private static void PatchLocalization()
    {
        try
        {
            new Harmony(ModId).PatchAll(Assembly.GetExecutingAssembly());
            Log.Info("[sts2-replica] patches applied; relic art hooks "
                + ReplicaArt.DescribeHooks());
        }
        catch (Exception exception)
        {
            Log.Error("[sts2-replica] localization patch failed: " + exception);
        }
    }

    /// <summary>
    /// Hooked to RunStarted, which also fires when a save is loaded (RunManager.cs:788, NGame.cs:1182).
    /// The config exchange therefore runs on every start, including a client that joins or reloads.
    /// </summary>
    private static void OnRunStarted(RunState state)
    {
        try
        {
            ReplicaRuntimeConfig.ClearOverride();
            // 读档与新开都走这条路径：上一局的快照先作废，再由下面的分支按此刻的配置重新冻结。
            ReplicaRuntimeConfig.EndRun();
            ReplicaConfigSync.AttachForRun(state);
            if (!ReplicaRuntimeConfig.IsMultiplayerClient)
            {
                // 单人局与房主：本机的 Effective 就是本局的答案，此刻起固定。
                // 联机客户端要等房主那份到达，由 ReplicaConfigSync 在采纳成功时冻结。
                ReplicaRuntimeConfig.BeginRun(ReplicaRuntimeConfig.Effective);
            }
        }
        catch (Exception exception)
        {
            Log.Error("[sts2-replica] config sync failed at run start: " + exception);
        }
        if (ReplicaRuntimeConfig.IsMultiplayerClient)
        {
            // The host's start_with_relic arrives one round trip later, and granting from the local file
            // here would leave the two ends with different relic sets. OnHostConfigAdopted grants instead.
            return;
        }
        if (!ReplicaRuntimeConfig.Effective.StartWithRelic)
        {
            return;
        }
        TaskHelper.RunSafely(GrantStartingRelics(state));
    }

    /// <summary>
    /// Called once a client has adopted the host's values. RelicCmd.Obtain sends nothing (RelicCmd.cs:35-55),
    /// so granting later than the host still ends in the same relic set on both ends.
    /// </summary>
    internal static void OnHostConfigAdopted(RunState state)
    {
        if (!ReplicaRuntimeConfig.Effective.StartWithRelic)
        {
            return;
        }
        TaskHelper.RunSafely(GrantStartingRelics(state));
    }

    private static async Task GrantStartingRelics(RunState state)
    {
        try
        {
            // RunStarted also fires when a save is loaded (RunManager.cs:788, NGame.cs:1182), where a
            // relic the player dropped earlier would be handed out again. A fresh run sits at floor 0.
            if (state.TotalFloor > 0)
            {
                return;
            }
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
