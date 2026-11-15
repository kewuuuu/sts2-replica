using System;
using System.Collections.Generic;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Logging;

namespace Sts2Replica.Localization;

/// <summary>
/// Injects our localization entries into the game's existing tables at runtime.
/// A pck-bundled loc table is not available to a DLL-only mod
/// (ModManager.GetModdedLocTables reads res://&lt;id&gt;/localization/..., ModManager.cs:1118-1123),
/// so the entries are merged into the live LocTable instead.
/// </summary>
public static class ReplicaLoc
{
	public const string RelicEntry = "GOOD_CARD_REPLICA";

	/// <summary>Called after LocManager.Initialize and again after every locale change.</summary>
	public static void InjectAll()
	{
		LocManager? manager = LocManager.Instance;
		if (manager == null)
		{
			Log.Warn("[sts2-replica] locale injection skipped: LocManager is not initialized yet");
			return;
		}
		Inject(manager, "relics", RelicEntries());
		Inject(manager, "card_reward_ui", new List<KeyValuePair<string, string>>
		{
			new KeyValuePair<string, string>("OPTION_MOLD.name", "制模"),
			new KeyValuePair<string, string>("OPTION_SAMPLE.name", "取样")
		});
	}

	private static void Inject(LocManager manager, string table, IEnumerable<KeyValuePair<string, string>> entries)
	{
		try
		{
			manager.GetTable(table).MergeWith(new Dictionary<string, string>(entries, StringComparer.Ordinal));
		}
		catch (Exception exception)
		{
			Log.Warn("[sts2-replica] localization table '" + table + "' not injected: " + exception.Message);
		}
	}

	/// <summary>
	/// Model ids are upper-cased by ModelDb.GetEntry (StringHelper.Slugify), but the key case of the
	/// shipped tables could not be verified from the pck, so every case variant is provided.
	/// </summary>
	private static List<KeyValuePair<string, string>> RelicEntries()
	{
		List<KeyValuePair<string, string>> entries = new List<KeyValuePair<string, string>>();
		Add(entries, "title", "好牌多抓");
		Add(entries, "description", "卡牌奖励中可以放弃本次奖励，为此遗物充能 1 点。充能满后，改为从牌组中按稀有度抽选候选牌，自选一张加入牌组。");
		Add(entries, "flavor", "牌桌上的好牌，总是值得多抓一张。");
		Add(entries, "selectionScreenPrompt", "选择一张牌加入牌组");
		return entries;
	}

	private static void Add(List<KeyValuePair<string, string>> entries, string suffix, string text)
	{
		foreach (string entry in new[] { RelicEntry, RelicEntry.ToUpperInvariant(), RelicEntry.ToLowerInvariant() })
		{
			string key = entry + "." + suffix;
			if (entries.Exists(pair => pair.Key == key))
			{
				continue;
			}
			entries.Add(new KeyValuePair<string, string>(key, text));
		}
	}
}
