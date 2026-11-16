using System;
using System.Reflection;
using MegaCrit.Sts2.Core.Logging;
using STS2RitsuLib.Settings;
using STS2RitsuLib.Utils.Persistence;
using Sts2Replica.Config;

namespace Sts2Replica.Integration;

/// <summary>
/// 把 <see cref="ReplicaConfig"/> 里玩家可调的 8 个键注册成 RitsuLib 设置界面里的一页。
/// 只有 csproj 打开 EnableRitsuLib 时才参与编译；运行期还要 RitsuLib 程序集确实加载了才会注册，
/// 任何失败都被 <see cref="TryRegister"/> 挡成一条警告，模组其余部分不受影响。
/// </summary>
internal static class RitsuLibSettingsPage
{
    private const string SettingsAssemblyName = "STS2-RitsuLib.Settings";
    private const string PageId = "replica-settings";

    /// <summary>充能阈值滑块的范围。键本身只钳制下限（<c>Math.Max(1, ...)</c>），上限是设置页的取值区间。</summary>
    private const int ChargeRequiredMin = 1;
    private const int ChargeRequiredMax = 20;

    /// <summary>五档权重滑块的上限。默认值最大为 6，100 已足够拉开差距，也避免滑块步数过多。</summary>
    private const int WeightMax = 100;

    private static bool _loggedReadOnly;

    /// <summary>
    /// 设置页入口：RitsuLib 没加载时只留一条警告；注册过程中任何异常也只留一条警告。
    /// </summary>
    public static void TryRegister()
    {
        if (!IsSettingsAssemblyLoaded())
        {
            Log.Warn("[sts2-replica] RitsuLib settings page not registered: " + SettingsAssemblyName + " is not loaded");
            return;
        }
        try
        {
            Register();
            Log.Info("[sts2-replica] RitsuLib settings page registered: mod_id=" + ReplicaMod.ModId + ", page_id=" + PageId);
        }
        catch (Exception exception)
        {
            Log.Warn("[sts2-replica] RitsuLib settings page not registered: " + exception.Message);
        }
    }

    /// <summary>
    /// 探测 RitsuLib 设置程序集是否已加载。只看程序集清单，不触发任何 RitsuLib 类型解析：
    /// 这段代码必须在「RitsuLib 不存在」时也能安全执行。
    /// </summary>
    private static bool IsSettingsAssemblyLoaded()
    {
        Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
        foreach (Assembly assembly in assemblies)
        {
            string? name = assembly.GetName().Name;
            if (string.Equals(name, SettingsAssemblyName, StringComparison.Ordinal))
            {
                return true;
            }
        }
        return false;
    }

    private static void Register()
    {
        ModSettingsPageBuilder builder = new ModSettingsPageBuilder(ReplicaMod.ModId, PageId);
        builder.WithTitle(ModSettingsText.Literal("好牌多抓"));
        builder.WithDescription(ModSettingsText.Literal(
            "改动会立刻写回 sts2-replica.json。取样相关数值在下一次判定时生效，开局自带遗物在下一次开局时生效。"
            + "联机时以房主配置为准：客户端这里显示的是房主的数值，写入不会改动本机文件。"));
        builder.WithModDisplayName(ModSettingsText.Literal("好牌多抓 / Good Card Replica"));

        builder.AddSection("sampling", section =>
        {
            section.WithTitle(ModSettingsText.Literal("充能与取样"));
            section.AddIntSlider(
                "charge_required",
                ModSettingsText.Literal("充能阈值 a"),
                IntBinding("charge_required", () => ReplicaRuntimeConfig.Effective.ChargeRequired, (target, value) => target.ChargeRequired = value),
                ChargeRequiredMin,
                ChargeRequiredMax,
                1,
                value => value.ToString(),
                ModSettingsText.Literal("攒满这么多层【制模】充能后，奖励界面的按钮变成【取样】。"));
            section.AddIntSlider(
                "offer_count",
                ModSettingsText.Literal("取样候选数 b"),
                IntBinding("offer_count", () => ReplicaRuntimeConfig.Effective.OfferCount, (target, value) => target.OfferCount = value),
                1,
                ReplicaConfig.OfferCountMax,
                1,
                value => value.ToString(),
                ModSettingsText.Literal("【取样】一次给出多少张候选。上限 3 与 JSON 配置的钳制一致。"));
            section.AddIntSlider(
                "weight_common",
                ModSettingsText.Literal("白卡权重 c"),
                IntBinding("weight_common", () => ReplicaRuntimeConfig.Effective.WeightCommon, (target, value) => target.WeightCommon = value),
                0,
                WeightMax,
                1,
                value => value.ToString(),
                ModSettingsText.Literal("白卡（含基础牌）被抽到的相对权重；0 表示该档不进池。"));
            section.AddIntSlider(
                "weight_uncommon",
                ModSettingsText.Literal("蓝卡权重 d"),
                IntBinding("weight_uncommon", () => ReplicaRuntimeConfig.Effective.WeightUncommon, (target, value) => target.WeightUncommon = value),
                0,
                WeightMax,
                1,
                value => value.ToString(),
                ModSettingsText.Literal("蓝卡被抽到的相对权重。"));
            section.AddIntSlider(
                "weight_rare",
                ModSettingsText.Literal("金卡权重 e"),
                IntBinding("weight_rare", () => ReplicaRuntimeConfig.Effective.WeightRare, (target, value) => target.WeightRare = value),
                0,
                WeightMax,
                1,
                value => value.ToString(),
                ModSettingsText.Literal("金卡被抽到的相对权重。"));
            section.AddIntSlider(
                "weight_colorless",
                ModSettingsText.Literal("无色牌权重 f"),
                IntBinding("weight_colorless", () => ReplicaRuntimeConfig.Effective.WeightColorless, (target, value) => target.WeightColorless = value),
                0,
                WeightMax,
                1,
                value => value.ToString(),
                ModSettingsText.Literal("无色牌被抽到的相对权重。"));
            section.AddIntSlider(
                "weight_curse",
                ModSettingsText.Literal("诅咒权重 g"),
                IntBinding("weight_curse", () => ReplicaRuntimeConfig.Effective.WeightCurse, (target, value) => target.WeightCurse = value),
                0,
                WeightMax,
                1,
                value => value.ToString(),
                ModSettingsText.Literal("诅咒牌被抽到的相对权重。"));
        });

        builder.AddSection("startup", section =>
        {
            section.WithTitle(ModSettingsText.Literal("开局"));
            section.AddToggle(
                "start_with_relic",
                ModSettingsText.Literal("开局自带遗物"),
                BoolBinding("start_with_relic", () => ReplicaRuntimeConfig.Effective.StartWithRelic, (target, value) => target.StartWithRelic = value),
                ModSettingsText.Literal("每个玩家在开局直接获得一个「好牌多抓」；下一次开局生效。"),
                () => true);
        });

        ModSettingsPage page = builder.Build();
        ModSettingsRegistry.Register(page);
    }

    private static IModSettingsValueBinding<int> IntBinding(
        string dataKey,
        Func<int> read,
        Action<DraftConfig, int> write)
    {
        return ModSettingsBindings.Callback<int>(
            ReplicaMod.ModId,
            dataKey,
            read,
            value => WriteLocal(dataKey, target => write(target, value)),
            () => { },
            SaveScope.Global);
    }

    private static IModSettingsValueBinding<bool> BoolBinding(
        string dataKey,
        Func<bool> read,
        Action<DraftConfig, bool> write)
    {
        return ModSettingsBindings.Callback<bool>(
            ReplicaMod.ModId,
            dataKey,
            read,
            value => WriteLocal(dataKey, target => write(target, value)),
            () => { },
            SaveScope.Global);
    }

    /// <summary>
    /// 每次改动都从「当前生效配置」拷一份候选、改一项、整份写回；写入走
    /// <see cref="ReplicaRuntimeConfig.TryWriteLocal"/>，所以联机客户端既不改本机 JSON，也不改页面显示值。
    /// </summary>
    private static void WriteLocal(string dataKey, Action<DraftConfig> change)
    {
        Log.Info("[sts2-replica] settings write " + dataKey);
        DraftConfig candidate = new DraftConfig(ReplicaRuntimeConfig.Effective);
        change(candidate);
        if (ReplicaRuntimeConfig.TryWriteLocal(candidate.ToConfig()))
        {
            return;
        }
        if (!ReplicaRuntimeConfig.IsMultiplayerClient || _loggedReadOnly)
        {
            return;
        }
        _loggedReadOnly = true;
        Log.Info("[sts2-replica] settings page is read-only this run: a multiplayer client follows the host's config");
    }

    /// <summary>
    /// 设置页的候选配置：8 个可玩项可变，另外 4 个过滤开关从生效配置原样带过，
    /// 写回时合成完整的 <see cref="ReplicaConfig"/>，不会丢键。
    /// </summary>
    private sealed class DraftConfig
    {
        public DraftConfig(ReplicaConfig source)
        {
            ChargeRequired = source.ChargeRequired;
            OfferCount = source.OfferCount;
            WeightCommon = source.WeightCommon;
            WeightUncommon = source.WeightUncommon;
            WeightRare = source.WeightRare;
            WeightColorless = source.WeightColorless;
            WeightCurse = source.WeightCurse;
            StartWithRelic = source.StartWithRelic;
            IncludeBasic = source.IncludeBasic;
            IncludeStatus = source.IncludeStatus;
            IncludeQuest = source.IncludeQuest;
            Dedupe = source.Dedupe;
        }

        public int ChargeRequired { get; set; }

        public int OfferCount { get; set; }

        public int WeightCommon { get; set; }

        public int WeightUncommon { get; set; }

        public int WeightRare { get; set; }

        public int WeightColorless { get; set; }

        public int WeightCurse { get; set; }

        public bool StartWithRelic { get; set; }

        public bool IncludeBasic { get; }

        public bool IncludeStatus { get; }

        public bool IncludeQuest { get; }

        public bool Dedupe { get; }

        public ReplicaConfig ToConfig()
        {
            return new ReplicaConfig
            {
                ChargeRequired = ChargeRequired,
                OfferCount = OfferCount,
                WeightCommon = WeightCommon,
                WeightUncommon = WeightUncommon,
                WeightRare = WeightRare,
                WeightColorless = WeightColorless,
                WeightCurse = WeightCurse,
                StartWithRelic = StartWithRelic,
                IncludeBasic = IncludeBasic,
                IncludeStatus = IncludeStatus,
                IncludeQuest = IncludeQuest,
                Dedupe = Dedupe
            };
        }
    }
}
