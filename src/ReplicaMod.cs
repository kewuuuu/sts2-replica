using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Modding;
using Sts2Replica.Models;

namespace Sts2Replica;

[ModInitializer("Init")]
public static class ReplicaMod
{
    public static void Init()
    {
        ModHelper.AddModelToPool<SharedRelicPool, PlaceholderRelic>();
        Log.Info("[sts2-replica] initializer invoked; PlaceholderRelic queued for SharedRelicPool");
    }
}
