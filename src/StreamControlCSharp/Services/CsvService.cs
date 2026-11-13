using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using StreamControlCSharp.Models;

namespace StreamControlCSharp.Services
{
    public class CsvService
    {
        private string ResolvePath(string fileName)
        {
            string baseDir = AppContext.BaseDirectory;
            string p1 = Path.Combine(baseDir, fileName);
            if (File.Exists(p1)) return p1;

            string p2 = Path.Combine(Directory.GetCurrentDirectory(), fileName);
            if (File.Exists(p2)) return p2;

            return p1;
        }

        public List<PlayerEntry> LoadPlayers()
        {
            var list = new List<PlayerEntry>();
            string path = ResolvePath("players.csv");
            if (!File.Exists(path)) return list;

            try
            {
                var lines = File.ReadAllLines(path);
                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    var parts = line.Split(',');
                    var p = new PlayerEntry
                    {
                        Name = parts.Length > 0 ? parts[0].Trim() : "",
                        Country = parts.Length > 1 ? parts[1].Trim() : "",
                        Tag = parts.Length > 2 ? parts[2].Trim() : ""
                    };
                    if (!string.IsNullOrEmpty(p.Name))
                    {
                        list.Add(p);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading players.csv: {ex.Message}");
            }

            return list;
        }

        public List<CommentatorEntry> LoadCommentators()
        {
            var list = new List<CommentatorEntry>();
            string path = ResolvePath("commentators.csv");
            if (!File.Exists(path)) return list;

            try
            {
                var lines = File.ReadAllLines(path);
                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    var parts = line.Split(',');
                    var c = new CommentatorEntry
                    {
                        Name = parts.Length > 0 ? parts[0].Trim() : "",
                        Twitter = parts.Length > 1 ? parts[1].Trim() : ""
                    };
                    if (!string.IsNullOrEmpty(c.Name))
                    {
                        list.Add(c);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading commentators.csv: {ex.Message}");
            }

            return list;
        }

        public List<string> LoadSimpleList(string fileName)
        {
            var list = new List<string>();
            string path = ResolvePath(fileName);
            if (!File.Exists(path)) return list;

            try
            {
                var lines = File.ReadAllLines(path);
                foreach (var line in lines)
                {
                    string trimmed = line.Trim();
                    if (!string.IsNullOrEmpty(trimmed) && !list.Contains(trimmed))
                    {
                        list.Add(trimmed);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading {fileName}: {ex.Message}");
            }

            return list;
        }

        public void SaveNewPlayer(string name, string country, string tag)
        {
            if (string.IsNullOrWhiteSpace(name)) return;
            string path = ResolvePath("players.csv");

            try
            {
                var players = LoadPlayers();
                if (players.Any(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                    return; // already exists

                // Safe append with clean comma format
                string cleanLine = $"{name.Replace(",", "")},{country.Replace(",", "")},{tag.Replace(",", "")}\n";
                File.AppendAllText(path, cleanLine, System.Text.Encoding.UTF8);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving new player: {ex.Message}");
            }
        }
    }
}
