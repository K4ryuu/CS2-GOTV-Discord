using System.IO.Compression;
using Microsoft.Extensions.Logging;

namespace K4GOTV;

public static class FileManager
{
	public static async Task<bool> ZipDemoAsync(string demoPath, string zipPath, ILogger logger)
	{
		int retryCount = 5;
		int delayMilliseconds = 2000;
		bool isFileReady = false;

		while (retryCount > 0 && !isFileReady)
		{
			try
			{
				using FileStream fs = new FileStream(demoPath, FileMode.Open, FileAccess.Read, FileShare.None);
				isFileReady = true;
			}
			catch (IOException)
			{
				retryCount--;
				await Task.Delay(delayMilliseconds);
			}
		}

		if (!isFileReady)
		{
			logger.LogError($"Failed to access file: {demoPath}");
			return false;
		}

		try
		{
			using var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create);
			archive.CreateEntryFromFile(demoPath, Path.GetFileName(demoPath), CompressionLevel.Fastest);
			return true;
		}
		catch (Exception ex)
		{
			logger.LogError($"Error occurred during compression: {ex.Message}");
			return false;
		}
	}

	public static async Task DeleteFileAsync(string path, ILogger logger, bool logDeletion = true)
	{
		if (!File.Exists(path))
		{
			logger.LogWarning($"File not found for deletion: {path}");
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
				if (logDeletion)
					logger.LogInformation($"File successfully deleted: {path}");
				return;
			}
			catch (IOException)
			{
				retryCount++;
				if (retryCount < maxRetries)
					await Task.Delay(retryDelayMs);
				else
					logger.LogError($"Failed to delete file after {maxRetries} attempts: {path}");
			}
			catch (Exception ex)
			{
				logger.LogError($"Error occurred while deleting file ({path}): {ex.Message}");
				return;
			}
		}
	}
}