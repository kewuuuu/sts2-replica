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
        DraftConfig draft = new DraftConfig(ReplicaConfigStore.Current);

        ModSettingsPageBuilder builder = new ModSettingsPageBuilder(ReplicaMod.ModId, PageId);
        builder.WithTitle(ModSettingsText.Literal("好牌多抓"));
        builder.WithDescription(ModSettingsText.Literal(
            "改动会立刻写回 sts2-replica.json。取样相关数值在下一次判定时生效，开局自带遗物在下一次开局时生效。"));
        builder.WithModDisplayName(ModSettingsText.Literal("好牌多抓 / Good Card Replica"));

        builder.AddSection("sampling", section =>
        {
            section.WithTitle(ModSettingsText.Literal("充能与取样"));
            section.AddIntSlider(
                "charge_required",
                ModSettingsText.Literal("充能阈值 a"),
                IntBinding(draft, "charge_required", () => draft.ChargeRequired, (target, value) => target.ChargeRequired = value),
                ChargeRequiredMin,
                ChargeRequiredMax,
                1,
                value => value.ToString(),
                ModSettingsText.Literal("攒满这么多层【制模】充能后，奖励界面的按钮变成【取样】。"));
            section.AddIntSlider(
                "offer_count",
                ModSettingsText.Literal("取样候选数 b"),
                IntBinding(draft, "offer_count", () => draft.OfferCount, (target, value) => target.OfferCount = value),
                1,
                ReplicaConfig.OfferCountMax,
                1,
                value => value.ToString(),
                ModSettingsText.Literal("【取样】一次给出多少张候选。上限 3 与 JSON 配置的钳制一致。"));
            section.AddIntSlider(
                "weight_common",
                ModSettingsText.Literal("白卡权重 c"),
                IntBinding(draft, "weight_common", () => draft.WeightCommon, (target, value) => target.WeightCommon = value),
                0,
                WeightMax,
                1,
                value => value.ToString(),
                ModSettingsText.Literal("白卡（含基础牌）被抽到的相对权重；0 表示该档不进池。"));
            section.AddIntSlider(
                "weight_uncommon",
                ModSettingsText.Literal("蓝卡权重 d"),
                IntBinding(draft, "weight_uncommon", () => draft.WeightUncommon, (target, value) => target.WeightUncommon = value),
                0,
                WeightMax,
                1,
                value => value.ToString(),
                ModSettingsText.Literal("蓝卡被抽到的相对权重。"));
            section.AddIntSlider(
                "weight_rare",
                ModSettingsText.Literal("金卡权重 e"),
                IntBinding(draft, "weight_rare", () => draft.WeightRare, (target, value) => target.WeightRare = value),
                0,
                WeightMax,
                1,
                value => value.ToString(),
                ModSettingsText.Literal("金卡被抽到的相对权重。"));
            section.AddIntSlider(
                "weight_colorless",
                ModSettingsText.Literal("无色牌权重 f"),
                IntBinding(draft, "weight_colorless", () => draft.WeightColorless, (target, value) => target.WeightColorless = value),
                0,
                WeightMax,
                1,
                value => value.ToString(),
                ModSettingsText.Literal("无色牌被抽到的相对权重。"));
            section.AddIntSlider(
                "weight_curse",
                ModSettingsText.Literal("诅咒权重 g"),
                IntBinding(draft, "weight_curse", () => draft.WeightCurse, (target, value) => target.WeightCurse = value),
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
                BoolBinding(draft, "start_with_relic", () => draft.StartWithRelic, (target, value) => target.StartWithRelic = value),
                ModSettingsText.Literal("每个玩家在开局直接获得一个「好牌多抓」；下一次开局生效。"),
                () => true);
        });

        ModSettingsPage page = builder.Build();
        ModSettingsRegistry.Register(page);
    }

    private static IModSettingsValueBinding<int> IntBinding(
        DraftConfig draft,
        string dataKey,
        Func<int> read,
        Action<DraftConfig, int> write)
    {
        return ModSettingsBindings.Callback<int>(
            ReplicaMod.ModId,
            dataKey,
            read,
            value =>
            {
                Log.Info("[sts2-replica] settings write " + dataKey + "=" + value);
                write(draft, value);
                draft.IsDirty = true;
                Persist(draft);
            },
            () => Persist(draft),
            SaveScope.Global);
    }

    private static IModSettingsValueBinding<bool> BoolBinding(
        DraftConfig draft,
        string dataKey,
        Func<bool> read,
        Action<DraftConfig, bool> write)
    {
        return ModSettingsBindings.Callback<bool>(
            ReplicaMod.ModId,
            dataKey,
            read,
            value =>
            {
                Log.Info("[sts2-replica] settings write " + dataKey + "=" + value);
                write(draft, value);
                draft.IsDirty = true;
                Persist(draft);
            },
            () => Persist(draft),
            SaveScope.Global);
    }

    /// <summary>
    /// 落盘。写入回调已经写过一次，RitsuLib 的 Save 回调只是第二道保障，所以按脏标记去重，
    /// 保存失败时保留脏标记，下一次改动或保存会重试。
    /// </summary>
    private static void Persist(DraftConfig draft)
    {
        if (!draft.IsDirty)
        {
            return;
        }
        if (ReplicaConfigStore.Save(draft.ToConfig()))
        {
            draft.IsDirty = false;
        }
    }

    /// <summary>
    /// 设置页的可变草稿：8 个可玩项可变，另外 4 个过滤开关从当前配置原样带过，
    /// 保存时合成完整的 <see cref="ReplicaConfig"/>，不会丢键。
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

        /// <summary>是否有尚未成功写回磁盘的改动。</summary>
        public bool IsDirty { get; set; }

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
