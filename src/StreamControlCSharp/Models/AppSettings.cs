namespace StreamControlCSharp.Models
{
    public class AppSettings
    {
        public string StartGgToken { get; set; } = "";
        public string TournamentSlug { get; set; } = "";
        public string StreamName { get; set; } = "";
        public bool AutoSyncEnabled { get; set; } = false;
        public int AutoSyncIntervalSeconds { get; set; } = 15;
        public string OutputDirectory { get; set; } = "browserfiles";
    }
}
