using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using MegaCrit.Sts2.Core.Logging;

namespace Sts2Replica.Config;

/// <summary>
/// Locates and loads sts2-replica.json. Never throws: an unreadable or missing file
/// yields <see cref="ReplicaConfig.Default"/>.
/// </summary>
public static class ReplicaConfigStore
{
	private const string FileName = "sts2-replica.json";
	private static ReplicaConfig _current = ReplicaConfig.Default;

	public static ReplicaConfig Current => _current;

	public static ReplicaConfig Load()
	{
		foreach (string path in CandidatePaths())
		{
			try
			{
				if (!File.Exists(path))
				{
					continue;
				}
				_current = ReplicaConfig.Parse(File.ReadAllText(path));
				Log.Info("[sts2-replica] config loaded from " + path);
				return _current;
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
		string path = ResolveSavePath();
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
	/// 写回目标：优先当前正在用的那个候选路径（文件已存在就用它），否则用候选路径里的第一条
	/// （可执行文件目录下的 mods/config），与 Load() 的查找顺序保持一致。
	/// </summary>
	private static string ResolveSavePath()
	{
		string fallback = string.Empty;
		foreach (string path in CandidatePaths())
		{
			if (fallback.Length == 0)
			{
				fallback = path;
			}
			if (File.Exists(path))
			{
				return path;
			}
		}
		return fallback;
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

	private static IEnumerable<string> CandidatePaths()
	{
		string executableConfig = string.Empty;
		try
		{
			string directory = Path.GetDirectoryName(Godot.OS.GetExecutablePath()) ?? string.Empty;
			executableConfig = Path.Combine(directory, "mods", "config", FileName);
		}
		catch (Exception exception)
		{
			Log.Warn("[sts2-replica] could not resolve the executable directory: " + exception.Message);
		}
		if (executableConfig.Length > 0)
		{
			yield return executableConfig;
		}
		string userConfig = string.Empty;
		try
		{
			userConfig = Path.Combine(Godot.ProjectSettings.GlobalizePath("user://"), "sts2-replica", "config.json");
		}
		catch (Exception exception)
		{
			Log.Warn("[sts2-replica] could not resolve the user directory: " + exception.Message);
		}
		if (userConfig.Length > 0)
		{
			yield return userConfig;
		}
	}
}
