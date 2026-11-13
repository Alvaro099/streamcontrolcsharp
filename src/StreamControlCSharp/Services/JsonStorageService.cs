using System;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using StreamControlCSharp.Models;

namespace StreamControlCSharp.Services
{
    public class JsonStorageService
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping // Preserves accents, tildes, UTF-8 cleanly
        };

        public string ResolveFilePath(string outputDir)
        {
            // If relative, check relative to AppContext.BaseDirectory or CurrentDirectory
            if (!Path.IsPathRooted(outputDir))
            {
                string baseDir = AppContext.BaseDirectory;
                string candidate = Path.Combine(baseDir, outputDir, "streamcontrol.json");
                if (File.Exists(candidate) || Directory.Exists(Path.Combine(baseDir, outputDir)))
                {
                    return candidate;
                }

                // Check working directory
                string workDir = Directory.GetCurrentDirectory();
                return Path.Combine(workDir, outputDir, "streamcontrol.json");
            }

            return Path.Combine(outputDir, "streamcontrol.json");
        }

        public StreamControlData Load(string outputDir)
        {
            try
            {
                string filePath = ResolveFilePath(outputDir);
                if (File.Exists(filePath))
                {
                    string json = File.ReadAllText(filePath);
                    var data = JsonSerializer.Deserialize<StreamControlData>(json, JsonOptions);
                    if (data != null) return data;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error reading streamcontrol.json: {ex.Message}");
            }

            return new StreamControlData();
        }

        public bool Save(StreamControlData data, string outputDir)
        {
            try
            {
                string filePath = ResolveFilePath(outputDir);
                string? dir = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                data.Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();

                string json = JsonSerializer.Serialize(data, JsonOptions);

                // Write with UTF-8 without BOM for 100% compatibility with XMLHttpRequest/Fetch in OBS
                File.WriteAllText(filePath, json, new System.Text.UTF8Encoding(false));
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error writing streamcontrol.json: {ex.Message}");
                return false;
            }
        }
    }
}
