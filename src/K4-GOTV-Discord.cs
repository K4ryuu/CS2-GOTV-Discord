using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Modules.Timers;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using FluentFTP;
using System.Text;
using System.IO.Compression;
using CG.Web.MegaApiClient;
using FluentFTP.Exceptions;
using CounterStrikeSharp.API.Modules.Cvars;
using MySqlConnector;
using Dapper;

namespace K4ryuuCS2GOTVDiscord
{
	public sealed class PluginConfig : BasePluginConfig
	{
		[JsonPropertyName("general")]
		public GeneralSettings General { get; set; } = new GeneralSettings();

		[JsonPropertyName("discord")]
		public DiscordSettings Discord { get; set; } = new DiscordSettings();

		[JsonPropertyName("auto-record")]
		public AutoRecordSettings AutoRecord { get; set; } = new AutoRecordSettings();

		[JsonPropertyName("mega")]
		public MegaSettings Mega { get; set; } = new MegaSettings();

		[JsonPropertyName("demo-request")]
		public DemoRequestSettings DemoRequest { get; set; } = new DemoRequestSettings();

		[JsonPropertyName("ftp")]
		public FtpSettings Ftp { get; set; } = new FtpSettings();

		public DatabaseSettings Database { get; set; } = new DatabaseSettings();

		[JsonPropertyName("ConfigVersion")]
		public override int Version { get; set; } = 10;

		public class GeneralSettings
		{
			[JsonPropertyName("minimum-demo-duration")]
			public float MinimumDemoDuration { get; set; } = 5.0f;

			[JsonPropertyName("delete-demo-after-upload")]
			public bool DeleteDemoAfterUpload { get; set; } = true;

			[JsonPropertyName("delete-zipped-demo-after-upload")]
			public bool DeleteZippedDemoAfterUpload { get; set; } = true;

			[JsonPropertyName("delete-every-demo-from-server-after-server-start")]
			public bool DeleteEveryDemoFromServerAfterServerStart { get; set; } = false;

			[JsonPropertyName("log-uploads")]
			public bool LogUploads { get; set; } = true;

			[JsonPropertyName("log-deletions")]
			public bool LogDeletions { get; set; } = true;

			[JsonPropertyName("default-file-name")]
			public string DefaultFileName { get; set; } = "demo";

			[JsonPropertyName("regular-file-naming-pattern")]
			public string RegularFileNamingPattern { get; set; } = "{fileName}_{map}_{date}_{time}";

			[JsonPropertyName("crop-rounds-file-naming-pattern")]
			public string CropRoundsFileNamingPattern { get; set; } = "{fileName}_{map}_round{round}_{date}_{time}";

			[JsonPropertyName("demo-directory")]
			public string DemoDirectory { get; set; } = "discord_demos";
		}

		public class DiscordSettings
		{
			[JsonPropertyName("webhook-url")]
			public string WebhookURL { get; set; } = "";

			[JsonPropertyName("webhook-avatar")]
			public string WebhookAvatar { get; set; } = "";

			[JsonPropertyName("webhook-upload-file")]
			public bool WebhookUploadFile { get; set; } = true;

			[JsonPropertyName("webhook-name")]
			public string WebhookName { get; set; } = "CSGO Demo Bot";

			[JsonPropertyName("embed-title")]
			public string EmbedTitle { get; set; } = "New CSGO Demo Available";

			[JsonPropertyName("message-text")]
			public string MessageText { get; set; } = "@everyone New CSGO Demo Available!";

			[JsonPropertyName("server-boost")]
			public int ServerBoost { get; set; } = 0;
		}

		public class AutoRecordSettings
		{
			[JsonPropertyName("enabled")]
			public bool Enabled { get; set; } = false;

			[JsonPropertyName("crop-rounds")]
			public bool CropRounds { get; set; } = false;

			[JsonPropertyName("stop-on-idle")]
			public bool StopOnIdle { get; set; } = false;

			[JsonPropertyName("record-warmup")]
			public bool RecordWarmup { get; set; } = true;

			[JsonPropertyName("idle-player-count-threshold")]
			public int IdlePlayerCountThreshold { get; set; } = 0;

			[JsonPropertyName("idle-time-seconds")]
			public int IdleTimeSeconds { get; set; } = 300;
		}

		public class MegaSettings
		{
			[JsonPropertyName("enabled")]
			public bool Enabled { get; set; } = false;

			[JsonPropertyName("email")]
			public string Email { get; set; } = "";

			[JsonPropertyName("password")]
			public string Password { get; set; } = "";
		}

		public class DemoRequestSettings
		{
			[JsonPropertyName("enabled")]
			public bool Enabled { get; set; } = false;

			[JsonPropertyName("print-all")]
			public bool PrintAll { get; set; } = true;

			[JsonPropertyName("delete-unused")]
			public bool DeleteUnused { get; set; } = true;
		}

		public class FtpSettings
		{
			[JsonPropertyName("enabled")]
			public bool Enabled { get; set; } = false;

			[JsonPropertyName("host")]
			public string Host { get; set; } = "";

			[JsonPropertyName("port")]
			public int Port { get; set; } = 21;

			[JsonPropertyName("username")]
			public string Username { get; set; } = "";

			[JsonPropertyName("password")]
			public string Password { get; set; } = "";

			[JsonPropertyName("remote-directory")]
			public string RemoteDirectory { get; set; } = "/";

			[JsonPropertyName("use-sftp")]
			public bool UseSftp { get; set; } = false;
		}

		public class DatabaseSettings
		{

			[JsonPropertyName("enabled")]
			public bool enable { get; set; } = false;

			[JsonPropertyName("host")]
			public string host { get; set; } = "localhost";

			[JsonPropertyName("port")]
			public uint port { get; set; } = 3306;

			[JsonPropertyName("name")]
			public string name { get; set; } = "";

			[JsonPropertyName("username")]
			public string username { get; set; } = "";

			[JsonPropertyName("password")]
			public string password { get; set; } = "";

			[JsonPropertyName("table_prefix")]
			public string table_prefix { get; set; } = "";
		}

	}

	[MinimumApiVersion(276)]
	public class CS2GOTVDiscordPlugin : BasePlugin, IPluginConfig<PluginConfig>
	{
		public override string ModuleName => "CS2 GOTV Discord";
		public override string ModuleVersion => "1.3.4";
		public override string ModuleAuthor => "K4ryuu @ KitsuneLab";

		public required PluginConfig Config { get; set; } = new PluginConfig();
		public string? fileName = null;
		public double LastPlayerCheckTime;
		public bool DemoRequestedThisRound = false;
		public List<(string name, ulong steamid)> Requesters = [];
		public CounterStrikeSharp.API.Modules.Timers.Timer? reservedTimer = null;
		public double DemoStartTime = 0.0;
		public bool IsPluginExecution = false;
		public bool PluginRecording = false;
		public int maxFileSizeInMB = 25;
		private string DemoDirectory => Path.Combine(Server.GameDirectory, "csgo", Config.General.DemoDirectory);

		private MySqlConnectionStringBuilder? DatabaseBuilder = null;

		public override void Load(bool hotReload)
		{
			AddCommandListener("tv_record", CommandListener_Record);
			AddCommandListener("tv_stoprecord", CommandListener_StopRecord);
			AddCommandListener("changelevel", CommandListener_Changelevel, HookMode.Pre);
			AddCommandListener("map", CommandListener_Changelevel, HookMode.Pre);
			AddCommandListener("host_workshop_map", CommandListener_Changelevel, HookMode.Pre);
			AddCommandListener("ds_workshop_changelevel", CommandListener_Changelevel, HookMode.Pre);

			RegisterEventHandler((EventCsWinPanelMatch @event, GameEventInfo info) =>
			{
				Server.ExecuteCommand("tv_stoprecord");
				return HookResult.Continue;
			});

			RegisterListener<Listeners.OnMapEnd>(() =>
			{
				if (!string.IsNullOrEmpty(fileName) && DemoStartTime != 0.0)
				{
					Server.ExecuteCommand("tv_stoprecord");
				}
			});

			RegisterEventHandler((EventRoundStart @event, GameEventInfo info) =>
			{
				if (Config.AutoRecord.Enabled)
				{
					if (Config.AutoRecord.CropRounds)
					{
						if (DemoStartTime != 0.0)
							Server.ExecuteCommand("tv_stoprecord");

						Requesters.Clear();
					}

					if (string.IsNullOrEmpty(fileName) || DemoStartTime == 0.0)
						Server.NextWorldUpdate(() => Server.ExecuteCommand("tv_record \"autodemo\""));
				}

				return HookResult.Continue;
			});

			RegisterEventHandler((EventPlayerActivate @event, GameEventInfo info) =>
			{
				CCSPlayerController? player = @event.Userid;
				if (player?.IsValid == true && !player.IsBot && !player.IsHLTV)
					LastPlayerCheckTime = Server.EngineTime;

				if (!PluginRecording && Config.AutoRecord.Enabled)
					Server.ExecuteCommand("tv_record");
				return HookResult.Continue;
			});

			Directory.CreateDirectory(Path.Combine(Server.GameDirectory, "csgo", "discord_demos"));

			if (Config.DemoRequest.Enabled)
				AddCommand($"css_demo", "Request a demo upload at the end of the round", Command_DemoRequest);

			if (Config.AutoRecord.StopOnIdle)
			{
				reservedTimer = AddTimer(1.0f, () =>
				{
					if (DemoStartTime == 0.0)
						return;

					if (GetPlayerCount() < Config.AutoRecord.IdlePlayerCountThreshold)
					{
						double idleTime = Server.EngineTime - LastPlayerCheckTime;
						if (idleTime > Config.AutoRecord.IdleTimeSeconds)
						{
							Server.ExecuteCommand("tv_stoprecord");
							base.Logger.LogInformation($"Recording stopped due to idle time exceeding {Config.AutoRecord.IdleTimeSeconds} seconds with player count < {Config.AutoRecord.IdlePlayerCountThreshold}.");
						}
					}
					else
					{
						LastPlayerCheckTime = Server.EngineTime;
					}
				}, TimerFlags.REPEAT);
			}

			if (Config.AutoRecord.Enabled && hotReload)
				Server.ExecuteCommand("tv_record \"autodemo\"");

			maxFileSizeInMB = (Config.Discord.ServerBoost == 2) ? 50 : (Config.Discord.ServerBoost == 3) ? 100 : 25;
		}

		public override void Unload(bool hotReload)
		{
			Server.ExecuteCommand("tv_stoprecord");
		}

		public override async void OnAllPluginsLoaded(bool isReload)
		{
			if (Config.General.DeleteEveryDemoFromServerAfterServerStart)
			{
				string[] demoFiles = Directory.GetFiles(DemoDirectory, "*.dem");
				string[] zipFiles = Directory.GetFiles(DemoDirectory, "*.zip");

				string[] allFiles = demoFiles.Concat(zipFiles).ToArray();

				foreach (var file in allFiles)
				{
					await DeleteFileAsync(file);
				}
			}
		}

		private HookResult CommandListener_Changelevel(CCSPlayerController? player, CommandInfo info)
		{
			if (!string.IsNullOrEmpty(fileName) && DemoStartTime != 0.0)
			{
				Server.ExecuteCommand("tv_stoprecord");
			}
			return HookResult.Continue;
		}

		private HookResult CommandListener_Record(CCSPlayerController? player, CommandInfo info)
		{
			if (!Config.AutoRecord.Enabled)
				return HookResult.Continue;

			if (PluginRecording)
				return HookResult.Continue;

			if (!Config.AutoRecord.RecordWarmup && GameRules()?.GameRules?.WarmupPeriod == true)
				return HookResult.Continue;

			if (!IsPluginExecution)
			{
				IsPluginExecution = true;

				DemoStartTime = Server.EngineTime;

				string fileNameArgument = info.ArgString.Trim().Replace("\"", "");
				string baseFileName = string.IsNullOrEmpty(fileNameArgument) ? Config.General.DefaultFileName : fileNameArgument;

				string pattern = Config.AutoRecord.Enabled && Config.AutoRecord.CropRounds
					? Config.General.CropRoundsFileNamingPattern
					: Config.General.RegularFileNamingPattern;

				fileName = ReplacePlaceholdersForFileName(pattern, baseFileName);

				// Ensure unique filename
				string fullPath = Path.Combine(DemoDirectory, $"{fileName}.dem");
				int counter = 1;
				while (File.Exists(fullPath))
				{
					fileName = $"{fileName}_{counter}";
					fullPath = Path.Combine(DemoDirectory, $"{fileName}.dem");
					counter++;
				}

				string relativePath = Path.Combine(Config.General.DemoDirectory, $"{fileName}.dem");
				Server.ExecuteCommand($"tv_record \"{relativePath}\"");
				return HookResult.Stop;
			}
			else
			{
				PluginRecording = true;
				IsPluginExecution = false;
				return HookResult.Continue;
			}
		}

		private HookResult CommandListener_StopRecord(CCSPlayerController? player, CommandInfo info)
		{
			if (!PluginRecording)
				return HookResult.Continue;

			PluginRecording = false;

			if (string.IsNullOrEmpty(fileName))
			{
				ResetVariables();
				return HookResult.Continue;
			}

			string demoPath = Path.Combine(DemoDirectory, $"{fileName}.dem");

			if (!File.Exists(demoPath))
			{
				Logger.LogError($"Demo file not found: {demoPath} - Recording stopped without processing.");
				return HookResult.Continue;
			}

			if (Config.DemoRequest.Enabled && !DemoRequestedThisRound)
			{
				if (Config.DemoRequest.DeleteUnused)
					Task.Run(() => DeleteFileAsync(demoPath));

				ResetVariables();
				return HookResult.Continue;
			}

			if (DemoStartTime != 0.0 && (Server.EngineTime - DemoStartTime) > Config.General.MinimumDemoDuration && !string.IsNullOrEmpty(fileName))
			{
				ProcessUpload(fileName, demoPath);
			}

			ResetVariables();
			return HookResult.Continue;
		}

		public void ProcessUpload(string fileName, string demoPath)
		{
			string zipPath = Path.Combine(DemoDirectory, $"{fileName}.zip");

			var demoLength = TimeSpan.FromSeconds(Server.EngineTime - DemoStartTime);
			var placeholderValues = new Dictionary<string, string>
			{
				{ "webhook_name", Config.Discord.WebhookName },
				{ "webhook_avatar", Config.Discord.WebhookAvatar },
				{ "message_text", Config.Discord.MessageText },
				{ "embed_title", Config.Discord.EmbedTitle },
				{ "map", Server.MapName },
				{ "date", DateTime.Now.ToString("yyyy-MM-dd") },
				{ "time", DateTime.Now.ToString("HH:mm:ss") },
				{ "timedate", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") },
				{ "length", $"{demoLength.Minutes:00}:{demoLength.Seconds:00}" },
				{ "round", (GameRules()?.GameRules?.TotalRoundsPlayed + 1)?.ToString() ?? "Unknown" },
				{ "mega_link", "Not uploaded to Mega." },
				{ "ftp_link", "Not uploaded to FTP." },
				{ "requester_name", string.Join(", ", Requesters.Select(x => x.name)) },
				{ "requester_steamid", string.Join(", ", Requesters.Select(x => x.steamid)) },
				{ "requester_both", string.Join("\n", Requesters.Select(x => $"{x.name} ({x.steamid})")) },
				{ "requester_count", Requesters.Count.ToString() },
				{ "player_count", GetPlayerCount().ToString() },
				{ "server_name", ConVar.Find("hostname")?.StringValue ?? "Unknown Server" },
				{ "fileName", Path.GetFileNameWithoutExtension(fileName) },
				{ "iso_timestamp", DateTime.UtcNow.ToString("o") },
				{ "file_size_warning", "" }
			};

			try
			{
				string remoteFilePath = ReplacePlaceholdersForFileName(Path.Combine(Config.Ftp.RemoteDirectory, Path.GetFileName(zipPath)).Replace("\\", "/"), Path.GetFileName(zipPath));

				Task.Run(async () =>
				{
					int retryCount = 5; // Maximum number of retries
					int delayMilliseconds = 2000; // Wait 2 seconds between retries

					bool isFileReady = false;
					while (retryCount > 0 && !isFileReady)
					{
						try
						{
							// Check if the file can be accessed (open and immediately close it)
							using FileStream fs = new FileStream(demoPath, FileMode.Open, FileAccess.Read, FileShare.None);
							isFileReady = true;
						}
						catch (IOException)
						{
							retryCount--;

							// Wait before retrying
							await Task.Delay(delayMilliseconds);
						}
					}

					if (isFileReady)
					{
						try
						{
							// Now that the file is ready, proceed with zipping it
							using var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create);
							archive.CreateEntryFromFile(demoPath, Path.GetFileName(demoPath), CompressionLevel.Fastest);
						}
						catch (Exception ex)
						{
							Logger.LogError($"An error occurred while zipping the file: {ex.Message}");
						}
					}
					else
					{
						Logger.LogError($"Failed to access the file '{demoPath}' after multiple attempts. File is still in use.");
					}

					// Upload to FTP if enabled
					if (Config.Ftp.Enabled && !string.IsNullOrEmpty(Config.Ftp.Host) && !string.IsNullOrEmpty(Config.Ftp.Username) && !string.IsNullOrEmpty(Config.Ftp.Password))
					{
						string ftpLink = await UploadToFtp(zipPath, remoteFilePath);
						placeholderValues["ftp_link"] = ftpLink;
					}

					// Upload to Mega if enabled
					if (Config.Mega.Enabled && !string.IsNullOrEmpty(Config.Mega.Email) && !string.IsNullOrEmpty(Config.Mega.Password))
					{
						var client = new MegaApiClient();
						await client.LoginAsync(Config.Mega.Email, Config.Mega.Password);

						var rootNode = (await client.GetNodesAsync()).Single(x => x.Type == NodeType.Root);
						var uploadedNode = await client.UploadFileAsync(zipPath, rootNode);
						var downloadLink = await client.GetDownloadLinkAsync(uploadedNode);

						placeholderValues["mega_link"] = downloadLink.ToString();
					}

					// Load and process the payload template
					string payloadTemplatePath = Path.Combine(ModuleDirectory, "payload.json");
					if (!File.Exists(payloadTemplatePath))
					{
						Logger.LogError($"Payload template not found at: {payloadTemplatePath}");
						return;
					}

					//move this here, so we can store the file in db
					long fileSizeInBytes = new FileInfo(zipPath).Length;
					long fileSizeInMB = fileSizeInBytes / (1024 * 1024);
					long fileSizeInKB = fileSizeInBytes / 1024;
					//Store them only if mega link or ftp link is not Not uploa uploaded to X.
					//As for web integration there no need to have record that cannot be downloaded ?
					if (DatabaseBuilder != null && (placeholderValues["mega_link"] != "Not uploaded to Mega." || placeholderValues["ftp_link"] != "Not uploaded to FTP."))
						try
						{
							string tableName = $"{Config.Database.table_prefix}k4_gotv";
							string insertQuery = $@"
							INSERT INTO `{tableName}` (
								map, date, time, timedate, length, round, mega_link, ftp_link,
								requester_name, requester_steamid, requester_count,
								player_count, server_name, file_name,file_size
							) VALUES (
								@map, @date, @time, @timedate, @length, @round, @mega_link, @ftp_link,
								@requester_name, @requester_steamid, @requester_count,
								@player_count, @server_name, @fileName, @fileSizeInKB
							);";
							await using var connection = new MySqlConnection(DatabaseBuilder.ConnectionString);
							await connection.OpenAsync();

							await connection.ExecuteAsync(insertQuery, new
							{
								map = placeholderValues["map"],
								date = placeholderValues["date"],
								time = placeholderValues["time"],
								timedate = placeholderValues["timedate"],
								length = placeholderValues["length"],
								round = placeholderValues["round"],
								mega_link = placeholderValues["mega_link"],
								ftp_link = placeholderValues["ftp_link"],
								requester_name = placeholderValues["requester_name"],
								requester_steamid = placeholderValues["requester_steamid"],
								requester_count = int.Parse(placeholderValues["requester_count"]),
								player_count = int.Parse(placeholderValues["player_count"]),
								server_name = placeholderValues["server_name"],
								fileName = placeholderValues["fileName"],
								fileSizeInKB = fileSizeInKB,
							});

							Logger.LogInformation($"Data successfully inserted into table {tableName}");
						}
						catch (Exception ex)
						{
							Logger.LogError($"Error inserting data into database: {ex.Message}");
						}


					string payloadTemplate = await File.ReadAllTextAsync(payloadTemplatePath);
					string payloadJson = ReplacePlaceholders(payloadTemplate, placeholderValues);

					using var httpClient = new HttpClient();
					MultipartFormDataContent content = new MultipartFormDataContent();

					// Add the JSON payload
					content.Add(new StringContent(payloadJson, Encoding.UTF8, "application/json"), "payload_json");

					// Check file size and handle upload
					if (File.Exists(zipPath))
					{

						if (fileSizeInMB > maxFileSizeInMB)
						{
							Logger.LogWarning($"Zip file size ({fileSizeInMB}MB) exceeds Discord's {maxFileSizeInMB}MB limit. File will not be uploaded to Discord.");
							placeholderValues["file_size_warning"] = $"⚠️ File size ({fileSizeInMB}MB) exceeds Discord's ({maxFileSizeInMB}MB) limit. Use Mega or FTP links to download.";

							// Suggest enabling Mega or FTP if not already enabled
							if (!Config.Mega.Enabled && !Config.Ftp.Enabled)
							{
								Logger.LogWarning("Consider enabling Mega or FTP upload for large files in the configuration.");
							}

							// Regenerate payload JSON with updated placeholders
							payloadJson = ReplacePlaceholders(payloadTemplate, placeholderValues);
							content = new MultipartFormDataContent
							{
								{ new StringContent(payloadJson, Encoding.UTF8, "application/json"), "payload_json" }
							};
						}
						else if (Config.Discord.WebhookUploadFile)
						{
							content.Add(new ByteArrayContent(await File.ReadAllBytesAsync(zipPath)), "file", $"{fileName}.zip");
						}
					}
					else
					{
						Logger.LogWarning($"Zip file not found for upload: {zipPath}");
					}

					// Send the webhook
					var response = await httpClient.PostAsync(Config.Discord.WebhookURL, content);
					response.EnsureSuccessStatusCode();

					if (Config.General.LogUploads)
						Logger.LogInformation($"Demo information uploaded successfully: {fileName}");

					// Clean up files if configured
					if (Config.General.DeleteDemoAfterUpload)
						await DeleteFileAsync(demoPath);

					if (Config.General.DeleteZippedDemoAfterUpload)
						await DeleteFileAsync(zipPath);
				});
			}
			catch (HttpRequestException ex)
			{
				Logger.LogError($"Error uploading to Discord: {ex.Message}");
			}
			catch (Exception ex)
			{
				Logger.LogError($"Unexpected error in ProcessUpload: {ex.Message}");
			}
		}

		private async Task<string> UploadToFtp(string filePath, string remoteFilePath)
		{
			using (var client = new AsyncFtpClient(Config.Ftp.Host, Config.Ftp.Username, Config.Ftp.Password, Config.Ftp.Port))
			{
				try
				{
					client.Config.EncryptionMode = Config.Ftp.UseSftp ? FtpEncryptionMode.Implicit : FtpEncryptionMode.None;
					client.Config.ValidateAnyCertificate = true;

					await client.AutoConnect();

					await client.UploadFile(filePath, remoteFilePath);

					// Generate a download link
					string protocol = Config.Ftp.UseSftp ? "sftp" : "ftp";
					string ftpLink = $"{protocol}://{Config.Ftp.Host}/{remoteFilePath}";
					return ftpLink;
				}
				catch (FtpException ex)
				{
					Logger.LogError($"FTP upload error: {ex.Message}");
					throw;
				}
				finally
				{
					await client.Disconnect();
				}
			}
		}

		public static string ReplacePlaceholders(string input, Dictionary<string, string> placeholders)
		{
			foreach (var placeholder in placeholders)
			{
				input = input.Replace($"{{{placeholder.Key}}}", placeholder.Value);
			}

			return input;
		}

		private async Task DeleteFileAsync(string path)
		{
			if (!File.Exists(path))
			{
				Logger.LogWarning($"File not found for deletion: {path}");
				return;
			}

			int retryCount = 0;
			const int maxRetries = 3;
			const int retryDelayMs = 1000;

			while (retryCount < maxRetries)
			{
				try
				{
					using FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);
					File.Delete(path);

					if (Config.General.LogDeletions)
						Logger.LogInformation($"File deleted successfully: {path}");
					return;
				}
				catch (IOException)
				{
					retryCount++;

					if (retryCount < maxRetries)
					{
						await Task.Delay(retryDelayMs);
					}
					else
					{
						Logger.LogError($"Failed to delete file after {maxRetries} attempts due to file lock: {path}");
					}
				}
				catch (Exception ex)
				{
					Logger.LogError($"Error deleting file {path}: {ex.Message}");
					return;
				}
			}
		}

		public void ResetVariables()
		{
			DemoRequestedThisRound = false;
			DemoStartTime = 0.0;
			fileName = null;
		}

		private string ReplacePlaceholdersForFileName(string pattern, string baseFileName)
		{
			var placeholders = new Dictionary<string, string>
			{
				{ "fileName", baseFileName },
				{ "map", Server.MapName },
				{ "date", DateTime.Now.ToString("yyyy-MM-dd") },
				{ "time", DateTime.Now.ToString("HH-mm-ss") },
				{ "timestamp", DateTime.Now.ToString("yyyyMMdd_HHmmss") },
				{ "round", (GameRules()?.GameRules?.TotalRoundsPlayed + 1)?.ToString() ?? "Unknown" },
				{ "playerCount", GetPlayerCount().ToString() },
			};

			return ReplacePlaceholders(pattern, placeholders);
		}

		public void Command_DemoRequest(CCSPlayerController? player, CommandInfo info)
		{
			if (Config.DemoRequest.PrintAll)
			{
				if (!DemoRequestedThisRound)
					Server.PrintToChatAll($" {Localizer["k4.general.prefix"]} {Localizer["k4.chat.demo.request.all", player?.PlayerName ?? "Server"]}");
			}
			else
				info.ReplyToCommand($" {Localizer["k4.general.prefix"]} {Localizer["k4.chat.demo.request.self"]}");

			if (player?.IsValid == true && !Requesters.Contains((player.PlayerName, player.SteamID)))
				Requesters.Add((player.PlayerName, player.SteamID));

			DemoRequestedThisRound = true;
		}

		public void OnConfigParsed(PluginConfig config)
		{
			if (config.Version < Config.Version)
				base.Logger.LogWarning("Configuration version mismatch (Expected: {0} | Current: {1})", this.Config.Version, config.Version);

			if (string.IsNullOrWhiteSpace(config.General.DemoDirectory))
			{
				base.Logger.LogWarning("DemoDirectory is empty, using default 'discord_demos'");
				config.General.DemoDirectory = "discord_demos";
			}

			string fullDemoPath = Path.Combine(Server.GameDirectory, "csgo", config.General.DemoDirectory);
			try
			{
				Directory.CreateDirectory(fullDemoPath);
			}
			catch (Exception ex)
			{
				base.Logger.LogError($"Failed to create demo directory: {ex.Message}");
				config.General.DemoDirectory = "discord_demos"; // Fallback to default
			}

			if (config.DemoRequest.Enabled)
			{
				config.AutoRecord.Enabled = true;
				config.AutoRecord.CropRounds = true;
			}

			if (config.AutoRecord.CropRounds && !config.AutoRecord.Enabled)
				base.Logger.LogWarning("AutoRecord.CropRounds is enabled but AutoRecord.Enabled is disabled. AutoRecord.CropRounds will not work without AutoRecord.Enabled enabled.");

			if (string.IsNullOrEmpty(config.Discord.WebhookURL))
				base.Logger.LogWarning("Discord.WebhookURL is not set. Plugin will not function without a valid webhook URL.");

			if (config.AutoRecord.StopOnIdle && config.AutoRecord.IdleTimeSeconds <= 0)
				base.Logger.LogWarning("AutoRecord.IdleTimeSeconds must be greater than 0 when AutoRecord.StopOnIdle is enabled.");

			if (config.Ftp.Enabled)
			{
				if (string.IsNullOrEmpty(config.Ftp.Host))
					base.Logger.LogWarning("FTP.Host is not set. FTP uploads will not function without a valid host.");
				if (string.IsNullOrEmpty(config.Ftp.Username) || string.IsNullOrEmpty(config.Ftp.Password))
					base.Logger.LogWarning("FTP credentials are not set. FTP uploads may fail without valid credentials.");
			}


			if (config.Database.enable)
			{
				try
				{
					DatabaseBuilder = new MySqlConnectionStringBuilder
					{
						Server = config.Database.host,
						Database = config.Database.name,
						UserID = config.Database.username,
						Password = config.Database.password,
						Port = config.Database.port,
						Pooling = true,
						MinimumPoolSize = 0,
						MaximumPoolSize = 640,
						ConnectionReset = false,
						CharacterSet = "utf8mb4"
					};
				
					Task.Run(async () =>
					{
						try
						{
							string tableName = $"{config.Database.table_prefix}k4_gotv";
							string sql = $@"
							CREATE TABLE IF NOT EXISTS `{tableName}` (
								id BIGINT AUTO_INCREMENT PRIMARY KEY,           -- Unique identifier for each row
								map VARCHAR(255) NOT NULL,                      
								date DATE NOT NULL COMMENT 'Date in yyyy-MM-dd format',       -- Date column
								time TIME NOT NULL COMMENT 'Time in HH:mm:ss format',         -- Time column
								timedate DATETIME NOT NULL COMMENT 'Datetime in yyyy-MM-dd HH:mm:ss format',  -- Date:time column       
								length VARCHAR(8) NOT NULL,                     
								round VARCHAR(50) NOT NULL,                     
								mega_link TEXT NOT NULL,                        
								ftp_link TEXT NOT NULL,                         
								requester_name TEXT NOT NULL,                   
								requester_steamid TEXT NOT NULL,           
								requester_count INT NOT NULL,                   
								player_count INT NOT NULL,                      
								server_name VARCHAR(255) NOT NULL,    
								file_name VARCHAR(255) NOT NULL,         
								file_size BIGINT NOT NULL,                       -- Changed to BIGINT for larger files

								-- Indexes
								KEY idx_requester_steamid (requester_steamid(50)),  -- Limited index size
								KEY idx_length (length),
								KEY idx_date_time (date, time),
								KEY idx_map (map),
								KEY idx_map_date_time (map, date, time),
								KEY idx_server_name (server_name)
							) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_520_ci;";

							// Use a local connection
							await using var connection = new MySqlConnection(DatabaseBuilder.ConnectionString);
							await connection.OpenAsync();
							await connection.ExecuteAsync(sql);
						}
						catch (Exception ex)
						{
							Logger.LogWarning($"Database error: {ex.Message}");
						}
					});
				}
				catch (Exception ex)
				{
					Logger.LogWarning("Could not connect to the database.");
				}
			}

			this.Config = config;
		}

		public static bool IsFileLocked(string filePath)
		{
			try
			{
				using FileStream stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.None);
				stream.Close();
			}
			catch (IOException)
			{
				return true;
			}

			return false;
		}

		public static int GetPlayerCount()
		{
			return Utilities.GetPlayers().Count(p => p?.IsValid == true && !p.IsBot && !p.IsHLTV);
		}

		public static CCSGameRulesProxy? GameRules()
		{
			return Utilities.FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules").FirstOrDefault();
		}
	}
}