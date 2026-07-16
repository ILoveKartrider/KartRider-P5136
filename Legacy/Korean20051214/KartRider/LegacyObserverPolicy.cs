using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace KartRider;

internal sealed class LegacyObserverPolicyDocument
{
	public int Version { get; set; } = 1;

	public List<string> Usernames { get; set; } = new List<string> { "옵저버" };
}

internal static class LegacyObserverPolicy
{
	private static readonly object SyncRoot = new object();
	private static readonly JsonSerializerOptions SerializerOptions = new JsonSerializerOptions
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		PropertyNameCaseInsensitive = true,
		WriteIndented = true
	};
	private static readonly string PolicyPath = Path.Combine(
		AppContext.BaseDirectory,
		"data",
		"korean2005",
		"observers.json");
	private static HashSet<string> _usernames = CreateDefaultUsernames();

	public static void Reload()
	{
		HashSet<string> usernames = CreateDefaultUsernames();
		try
		{
			Directory.CreateDirectory(Path.GetDirectoryName(PolicyPath)!);
			LegacyObserverPolicyDocument document;
			if (File.Exists(PolicyPath))
			{
				using FileStream stream = File.OpenRead(PolicyPath);
				document = JsonSerializer.Deserialize<LegacyObserverPolicyDocument>(
					stream,
					SerializerOptions) ?? new LegacyObserverPolicyDocument();
			}
			else
			{
				document = new LegacyObserverPolicyDocument();
				using FileStream stream = new FileStream(
					PolicyPath,
					FileMode.CreateNew,
					FileAccess.Write,
					FileShare.Read);
				JsonSerializer.Serialize(stream, document, SerializerOptions);
			}

			usernames.Clear();
			foreach (string username in document.Usernames ?? new List<string>())
			{
				if (!string.IsNullOrWhiteSpace(username))
				{
					usernames.Add(username.Trim());
				}
			}
		}
		catch (Exception exception)
		{
			LegacyPacketTrace.LogEvent(
				$"[2005 OBSERVER] Unable to load '{PolicyPath}': {exception.Message}; " +
				"using the default username '옵저버'.");
		}

		lock (SyncRoot)
		{
			_usernames = usernames;
		}
		LegacyPacketTrace.LogEvent(
			$"[2005 OBSERVER] Loaded {usernames.Count} observer username(s) " +
			$"from '{PolicyPath}'.");
	}

	public static bool IsObserver(LegacySessionProfile profile)
	{
		if (profile == null)
		{
			return false;
		}

		lock (SyncRoot)
		{
			string username = string.IsNullOrWhiteSpace(profile.SourceUsername)
				? profile.UserId
				: profile.SourceUsername;
			return Matches(username);
		}
	}

	private static bool Matches(string identity)
	{
		return !string.IsNullOrWhiteSpace(identity) && _usernames.Contains(identity.Trim());
	}

	private static HashSet<string> CreateDefaultUsernames()
	{
		return new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "옵저버" };
	}
}
