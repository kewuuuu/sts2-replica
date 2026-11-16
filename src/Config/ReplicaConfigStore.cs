using System;
using System.IO;
using System.Text;
using System.Text.Json;
using MegaCrit.Sts2.Core.Logging;

namespace Sts2Replica.Config;

/// <summary>
/// Locates and loads sts2-replica.cfg (JSON inside; only the extension changed). Never throws: an
/// unreadable or missing file yields <see cref="ReplicaConfig.Default"/>.
/// </summary>
public static class ReplicaConfigStore
{
	private const string FileName = "sts2-replica.cfg";
	private static ReplicaConfig _current = ReplicaConfig.Default;

	public static ReplicaConfig Current => _current;

	public static ReplicaConfig Load()
	{
		string path = ResolveConfigPath();
		WarnAboutLegacyFile(path);
		if (path.Length > 0)
		{
			try
			{
				if (File.Exists(path))
				{
					_current = ReplicaConfig.Parse(File.ReadAllText(path));
					Log.Info("[sts2-replica] config loaded from " + path);
					if (Serialize(_current) != Serialize(ReplicaConfig.Default))
					{
						// The reward screen syncs an index into a list every peer rebuilds, so the numbers have to
						// agree: a multiplayer client takes them from the host (ReplicaConfigSync) and ignores this
						// file until the run ends, while a singleplayer run uses them as they are.
						Log.Warn("[sts2-replica] non-default config is active; a multiplayer run uses the host's values, so a client's own values apply to singleplayer only");
					}
					return _current;
				}
			}
			catch (Exception exception)
			{
				Log.Warn("[sts2-replica] could not read config at " + path + ": " + exception.Message);
			}
		}
		_current = ReplicaConfig.Default;
		Log.Info("[sts2-replica] no config file found, using defaults");
		return _current;
	}

	/// <summary>
	/// 把整份配置写回配置文件，并把 <see cref="Current"/> 换成「刚写出的这份 JSON」的解析结果，
	/// 保证内存里的值与下次启动 Load() 读到的值完全一致。永远不抛：写失败只记警告并返回 false。
	/// </summary>
	public static bool Save(ReplicaConfig config)
	{
		string json = Serialize(config);
		_current = ReplicaConfig.Parse(json);
		string path = ResolveConfigPath();
		if (path.Length == 0)
		{
			Log.Warn("[sts2-replica] no config path available, settings are kept in memory only");
			return false;
		}
		try
		{
			string directory = Path.GetDirectoryName(path) ?? string.Empty;
			if (directory.Length > 0)
			{
				Directory.CreateDirectory(directory);
			}
			File.WriteAllText(path, json);
			Log.Info("[sts2-replica] config saved to " + path);
			return true;
		}
		catch (Exception exception)
		{
			Log.Warn("[sts2-replica] could not write config at " + path + ": " + exception.Message);
			return false;
		}
	}

	/// <summary>
	/// 读写的唯一路径：<游戏可执行文件目录>/mods/config/sts2-replica.cfg。0.3.0 起不再有候选路径，
	/// 也不再回落到 user:// —— 旧名 sts2-replica.json 既不读也不写。
	/// </summary>
	private static string ResolveConfigPath()
	{
		try
		{
			string directory = Path.GetDirectoryName(Godot.OS.GetExecutablePath()) ?? string.Empty;
			if (directory.Length > 0)
			{
				return Path.Combine(directory, "mods", "config", FileName);
			}
			Log.Warn("[sts2-replica] the executable directory is unknown; no config file can be read or written");
		}
		catch (Exception exception)
		{
			Log.Warn("[sts2-replica] could not resolve the executable directory: " + exception.Message);
		}
		return string.Empty;
	}

	/// <summary>
	/// 旧名文件只提示、不迁移：它留着会让玩家以为自己改的设置还在生效，删不删由玩家决定。
	/// </summary>
	private static void WarnAboutLegacyFile(string path)
	{
		if (path.Length == 0)
		{
			return;
		}
		string legacy = Path.ChangeExtension(path, ".json");
		try
		{
			if (File.Exists(legacy))
			{
				Log.Warn("[sts2-replica] legacy " + legacy + " is ignored (this build reads " + FileName + " only); delete it by hand");
			}
		}
		catch (Exception exception)
		{
			Log.Warn("[sts2-replica] could not check for a legacy config: " + exception.Message);
		}
	}

	/// <summary>
	/// 序列化全部 12 个键（8 个可玩项 + 4 个过滤开关），键名与 <see cref="ReplicaConfig.Parse"/> 一一对应。
	/// </summary>
	private static string Serialize(ReplicaConfig config)
	{
		using (MemoryStream stream = new MemoryStream())
		{
			using (Utf8JsonWriter writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
			{
				writer.WriteStartObject();
				writer.WriteNumber("charge_required", config.ChargeRequired);
				writer.WriteNumber("offer_count", config.OfferCount);
				writer.WriteNumber("weight_common", config.WeightCommon);
				writer.WriteNumber("weight_uncommon", config.WeightUncommon);
				writer.WriteNumber("weight_rare", config.WeightRare);
				writer.WriteNumber("weight_colorless", config.WeightColorless);
				writer.WriteNumber("weight_curse", config.WeightCurse);
				writer.WriteBoolean("start_with_relic", config.StartWithRelic);
				writer.WriteBoolean("include_basic", config.IncludeBasic);
				writer.WriteBoolean("include_status", config.IncludeStatus);
				writer.WriteBoolean("include_quest", config.IncludeQuest);
				writer.WriteBoolean("dedupe", config.Dedupe);
				writer.WriteEndObject();
			}
			return Encoding.UTF8.GetString(stream.ToArray());
		}
	}
}
