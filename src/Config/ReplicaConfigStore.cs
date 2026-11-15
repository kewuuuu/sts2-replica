using System;
using System.Collections.Generic;
using System.IO;
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
