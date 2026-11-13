using System;
using System.Text.Json.Serialization;

namespace StreamControlCSharp.Models
{
    public class StreamControlData
    {
        [JsonPropertyName("timestamp")]
        public string Timestamp { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();

        // Main Scoreboard
        [JsonPropertyName("p1Team")]
        public string P1Team { get; set; } = "";

        [JsonPropertyName("pName1")]
        public string PName1 { get; set; } = "";

        [JsonPropertyName("pCountry1")]
        public string PCountry1 { get; set; } = "";

        [JsonPropertyName("pScore1")]
        public string PScore1 { get; set; } = "0";

        [JsonPropertyName("p2Team")]
        public string P2Team { get; set; } = "";

        [JsonPropertyName("pName2")]
        public string PName2 { get; set; } = "";

        [JsonPropertyName("pCountry2")]
        public string PCountry2 { get; set; } = "";

        [JsonPropertyName("pScore2")]
        public string PScore2 { get; set; } = "0";

        [JsonPropertyName("round")]
        public string Round { get; set; } = "";

        [JsonPropertyName("textbottom")]
        public string TextBottom { get; set; } = "";

        // Commentators
        [JsonPropertyName("comm1name")]
        public string Comm1Name { get; set; } = "";

        [JsonPropertyName("comm1twit")]
        public string Comm1Twit { get; set; } = "";

        [JsonPropertyName("comm2name")]
        public string Comm2Name { get; set; } = "";

        [JsonPropertyName("comm2twit")]
        public string Comm2Twit { get; set; } = "";

        [JsonPropertyName("comm3name")]
        public string Comm3Name { get; set; } = "";

        [JsonPropertyName("comm3twit")]
        public string Comm3Twit { get; set; } = "";

        // Tournament Winners / Podio
        [JsonPropertyName("champeon")]
        public string Champeon { get; set; } = "";

        [JsonPropertyName("pCountryW1")]
        public string PCountryW1 { get; set; } = "";

        [JsonPropertyName("pChar1")]
        public string PChar1 { get; set; } = "";

        [JsonPropertyName("segundo")]
        public string Segundo { get; set; } = "";

        [JsonPropertyName("pCountryW2")]
        public string PCountryW2 { get; set; } = "";

        [JsonPropertyName("pChar2")]
        public string PChar2 { get; set; } = "";

        [JsonPropertyName("tercero")]
        public string Tercero { get; set; } = "";

        [JsonPropertyName("pCountryW3")]
        public string PCountryW3 { get; set; } = "";

        [JsonPropertyName("pChar3")]
        public string PChar3 { get; set; } = "";

        // Top 8 Bracket
        [JsonPropertyName("TOP8text")]
        public string TOP8Text { get; set; } = "";

        // Winners Semis 1
        [JsonPropertyName("ws1p1")]
        public string Ws1p1 { get; set; } = "";
        [JsonPropertyName("ws1p1s")]
        public string Ws1p1s { get; set; } = "";
        [JsonPropertyName("ws1p2")]
        public string Ws1p2 { get; set; } = "";
        [JsonPropertyName("ws1p2s")]
        public string Ws1p2s { get; set; } = "";

        // Winners Semis 2
        [JsonPropertyName("ws2p1")]
        public string Ws2p1 { get; set; } = "";
        [JsonPropertyName("ws2p1s")]
        public string Ws2p1s { get; set; } = "";
        [JsonPropertyName("ws2p2")]
        public string Ws2p2 { get; set; } = "";
        [JsonPropertyName("ws2p2s")]
        public string Ws2p2s { get; set; } = "";

        // Winners Finals
        [JsonPropertyName("wfp1")]
        public string Wfp1 { get; set; } = "";
        [JsonPropertyName("wfp1s")]
        public string Wfp1s { get; set; } = "";
        [JsonPropertyName("wfp2")]
        public string Wfp2 { get; set; } = "";
        [JsonPropertyName("wfp2s")]
        public string Wfp2s { get; set; } = "";

        // Losers Top 8 - 1
        [JsonPropertyName("ltop81p1")]
        public string Ltop81p1 { get; set; } = "";
        [JsonPropertyName("ltop81p1s")]
        public string Ltop81p1s { get; set; } = "";
        [JsonPropertyName("ltop81p2")]
        public string Ltop81p2 { get; set; } = "";
        [JsonPropertyName("ltop81p2s")]
        public string Ltop81p2s { get; set; } = "";

        // Losers Top 8 - 2
        [JsonPropertyName("ltop82p1")]
        public string Ltop82p1 { get; set; } = "";
        [JsonPropertyName("ltop82p1s")]
        public string Ltop82p1s { get; set; } = "";
        [JsonPropertyName("ltop82p2")]
        public string Ltop82p2 { get; set; } = "";
        [JsonPropertyName("ltop82p2s")]
        public string Ltop82p2s { get; set; } = "";

        // Losers Quarters 1
        [JsonPropertyName("lq1p1")]
        public string Lq1p1 { get; set; } = "";
        [JsonPropertyName("lq1p1s")]
        public string Lq1p1s { get; set; } = "";
        [JsonPropertyName("lq1p2")]
        public string Lq1p2 { get; set; } = "";
        [JsonPropertyName("lq1p2s")]
        public string Lq1p2s { get; set; } = "";

        // Losers Quarters 2
        [JsonPropertyName("lq2p1")]
        public string Lq2p1 { get; set; } = "";
        [JsonPropertyName("lq2p1s")]
        public string Lq2p1s { get; set; } = "";
        [JsonPropertyName("lq2p2")]
        public string Lq2p2 { get; set; } = "";
        [JsonPropertyName("lq2p2s")]
        public string Lq2p2s { get; set; } = "";

        // Losers Semis
        [JsonPropertyName("lsp1")]
        public string Lsp1 { get; set; } = "";
        [JsonPropertyName("lsp1s")]
        public string Lsp1s { get; set; } = "";
        [JsonPropertyName("lsp2")]
        public string Lsp2 { get; set; } = "";
        [JsonPropertyName("lsp2s")]
        public string Lsp2s { get; set; } = "";

        // Losers Finals
        [JsonPropertyName("lfp1")]
        public string Lfp1 { get; set; } = "";
        [JsonPropertyName("lfp1s")]
        public string Lfp1s { get; set; } = "";
        [JsonPropertyName("lfp2")]
        public string Lfp2 { get; set; } = "";
        [JsonPropertyName("lfp2s")]
        public string Lfp2s { get; set; } = "";

        // Grand Finals
        [JsonPropertyName("gfp1")]
        public string Gfp1 { get; set; } = "";
        [JsonPropertyName("gfp1s")]
        public string Gfp1s { get; set; } = "";
        [JsonPropertyName("gfp2")]
        public string Gfp2 { get; set; } = "";
        [JsonPropertyName("gfp2s")]
        public string Gfp2s { get; set; } = "";

        // Grand Finals Reset
        [JsonPropertyName("gfrp1")]
        public string Gfrp1 { get; set; } = "";
        [JsonPropertyName("gfrp1s")]
        public string Gfrp1s { get; set; } = "";
        [JsonPropertyName("gfrp2")]
        public string Gfrp2 { get; set; } = "";
        [JsonPropertyName("gfrp2s")]
        public string Gfrp2s { get; set; } = "";
    }
}
