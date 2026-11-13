using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using StreamControlCSharp.Models;

namespace StreamControlCSharp.Services
{
    /// <summary>
    /// Cliente de la API GraphQL v1 (alpha) de start.gg.
    /// Todas las consultas viven en <see cref="StartGgQueries"/>.
    /// </summary>
    public class StartGgService
    {
        private const string GraphQlEndpoint = "https://api.start.gg/gql/alpha";

        /// <summary>
        /// start.gg rechaza (límite de complejidad, 1000 objetos) cualquier request que
        /// devuelva más de ~40 sets con estos campos; 30 deja margen suficiente.
        /// </summary>
        private const int SetsPerPage = 30;

        /// <summary>Tope de páginas por fase para no castigar el rate limit del API.</summary>
        private const int MaxSetPages = 30;

        /// <summary>
        /// Si la referencia no identifica un evento, se muestran como máximo estas fechas
        /// (las más recientes) para no disparar decenas de consultas en torneos-temporada.
        /// </summary>
        private const int MaxFallbackEvents = 4;

        private static readonly Regex TournamentRefRegex = new(
            @"tournament/([^/?#\s]+)(?:/event/([^/?#\s]+))?",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex ShortRefRegex = new(
            @"^([^/?#\s]+)/event/([^/?#\s]+)$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(45) };

        /// <summary>Último error devuelto por start.gg (HTTP, GraphQL o excepción).</summary>
        public string? LastError { get; private set; }

        // ----------------------------------------------------------------------
        // Slug / referencia del torneo
        // ----------------------------------------------------------------------

        /// <summary>
        /// Devuelve solo el slug del torneo a partir de un slug, de una ruta
        /// "torneo/evento" o de una URL completa de start.gg.
        /// </summary>
        public static string CleanSlug(string input) => ParseReference(input).TournamentSlug;

        /// <summary>
        /// Separa una referencia de start.gg en (slug del torneo, slug del evento opcional).
        /// Acepta:
        ///   "mi-torneo"
        ///   "tournament/mi-torneo"
        ///   "tournament/mi-torneo/event/mi-evento"
        ///   "https://www.start.gg/tournament/mi-torneo/event/mi-evento/..."
        /// </summary>
        public static (string TournamentSlug, string? EventSlug) ParseReference(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return ("", null);

            string text = input.Trim();

            var match = TournamentRefRegex.Match(text);
            if (match.Success)
            {
                string tournamentSlug = match.Groups[1].Value;
                string eventSlug = match.Groups[2].Success ? match.Groups[2].Value : "";
                return (tournamentSlug, string.IsNullOrWhiteSpace(eventSlug) ? null : eventSlug);
            }

            var shortMatch = ShortRefRegex.Match(text);
            if (shortMatch.Success)
            {
                return (shortMatch.Groups[1].Value, shortMatch.Groups[2].Value);
            }

            return (text.Trim('/'), null);
        }

        // ----------------------------------------------------------------------
        // Transporte
        // ----------------------------------------------------------------------

        /// <summary>
        /// Ejecuta una consulta GraphQL y devuelve el objeto "data" de la respuesta.
        /// Devuelve null y deja el motivo en <see cref="LastError"/> si algo falla.
        /// </summary>
        private async Task<JsonElement?> ExecuteAsync(string query, object variables, string apiToken)
        {
            LastError = null;

            if (string.IsNullOrWhiteSpace(apiToken))
            {
                LastError = "Falta el token personal de start.gg.";
                return null;
            }

            try
            {
                var requestBody = new { query, variables };

                using var request = new HttpRequestMessage(HttpMethod.Post, GraphQlEndpoint);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiToken.Trim());
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                request.Content = new StringContent(
                    JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

                using var response = await _httpClient.SendAsync(request).ConfigureAwait(false);
                string payload = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                if (string.IsNullOrWhiteSpace(payload))
                {
                    LastError = $"start.gg devolvió una respuesta vacía (HTTP {(int)response.StatusCode}).";
                    return null;
                }

                using var document = JsonDocument.Parse(payload);
                var root = document.RootElement;

                if (root.TryGetProperty("errors", out var errors) &&
                    errors.ValueKind == JsonValueKind.Array &&
                    errors.GetArrayLength() > 0)
                {
                    var first = errors[0];
                    LastError = first.TryGetProperty("message", out var message)
                        ? message.GetString()
                        : "Error GraphQL sin mensaje.";
                    Debug.WriteLine($"[start.gg] Error GraphQL: {LastError}");
                    return null;
                }

                if (!response.IsSuccessStatusCode)
                {
                    LastError = $"start.gg respondió HTTP {(int)response.StatusCode}.";
                    return null;
                }

                if (root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object)
                {
                    return data.Clone();
                }

                LastError = "La respuesta de start.gg no contiene datos.";
                return null;
            }
            catch (TaskCanceledException)
            {
                LastError = "start.gg no respondió a tiempo (timeout).";
                return null;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                Debug.WriteLine($"[start.gg] Excepción: {ex}");
                return null;
            }
        }

        // ----------------------------------------------------------------------
        // Torneo / cola de stream
        // ----------------------------------------------------------------------

        /// <summary>Torneo con su stream queue (qué pelea toca en vivo).</summary>
        public async Task<StartGgTournamentInfo?> GetTournamentInfoAsync(string slug, string token)
        {
            var (tournamentSlug, _) = ParseReference(slug);
            if (string.IsNullOrWhiteSpace(tournamentSlug)) return null;

            var data = await ExecuteAsync(StartGgQueries.StreamQueueQuery, new { tourneySlug = tournamentSlug }, token);
            if (data is null) return null;

            if (!TryGetChild(data.Value, "tournament", out var tournament) ||
                tournament.ValueKind == JsonValueKind.Null)
            {
                LastError ??= "El torneo no existe o el slug es incorrecto.";
                return null;
            }

            var info = new StartGgTournamentInfo
            {
                Id = GetLong(tournament, "id"),
                Name = GetString(tournament, "name")
            };

            foreach (var queueEntry in EnumerateArray(tournament, "streamQueue"))
            {
                var entry = new StartGgStreamQueueEntry();

                if (TryGetChild(queueEntry, "stream", out var stream))
                {
                    entry.StreamName = GetString(stream, "streamName");
                    entry.StreamSource = GetString(stream, "streamSource");
                }

                foreach (var set in EnumerateArray(queueEntry, "sets"))
                {
                    long setId = GetLong(set, "id");
                    if (setId != 0) entry.SetIds.Add(setId);
                }

                info.StreamQueue.Add(entry);
            }

            return info;
        }

        /// <summary>Eventos del torneo con sus fases creadas (EVENT_QUERY de queries.py).</summary>
        public async Task<List<StartGgEventInfo>> GetEventsWithPhasesAsync(string slug, string token)
        {
            var events = new List<StartGgEventInfo>();

            var (tournamentSlug, _) = ParseReference(slug);
            if (string.IsNullOrWhiteSpace(tournamentSlug)) return events;

            var data = await ExecuteAsync(StartGgQueries.TournamentEventsQuery, new { tourneySlug = tournamentSlug }, token);
            if (data is null) return events;

            if (!TryGetChild(data.Value, "tournament", out var tournament) ||
                tournament.ValueKind == JsonValueKind.Null)
            {
                LastError ??= "El torneo no existe o el slug es incorrecto.";
                return events;
            }

            foreach (var eventElement in EnumerateArray(tournament, "events"))
            {
                var eventInfo = new StartGgEventInfo
                {
                    Id = GetLong(eventElement, "id"),
                    Name = GetString(eventElement, "name"),
                    Slug = GetString(eventElement, "slug"),
                    NumEntrants = (int)GetLong(eventElement, "numEntrants"),
                    StartAt = GetLong(eventElement, "startAt")
                };

                foreach (var phaseElement in EnumerateArray(eventElement, "phases"))
                {
                    eventInfo.Phases.Add(new StartGgPhaseInfo
                    {
                        Id = GetLong(phaseElement, "id"),
                        EventId = eventInfo.Id,
                        EventName = eventInfo.Name,
                        EventSlug = eventInfo.Slug,
                        Name = GetString(phaseElement, "name"),
                        PhaseOrder = (int)GetLong(phaseElement, "phaseOrder"),
                        BracketType = GetString(phaseElement, "bracketType"),
                        State = NormalizeState(GetString(phaseElement, "state"))
                    });
                }

                events.Add(eventInfo);
            }

            return events;
        }

        // ----------------------------------------------------------------------
        // Sets
        // ----------------------------------------------------------------------

        /// <summary>Detalle de un set puntual (SET_QUERY de queries.py).</summary>
        public async Task<StartGgMatchInfo?> GetSetDetailsAsync(long setId, string token)
        {
            if (setId == 0) return null;

            var data = await ExecuteAsync(StartGgQueries.SetQuery, new { setId }, token);
            if (data is null) return null;

            if (!TryGetChild(data.Value, "set", out var setElement) ||
                setElement.ValueKind == JsonValueKind.Null)
            {
                LastError ??= "El set no existe o no es accesible.";
                return null;
            }

            var match = ParseSet(setElement);
            if (match.SetId == 0) match.SetId = setId;

            return match;
        }

        /// <summary>
        /// Todos los sets de una fase (BRACKET_QUERY de queries.py), recorriendo
        /// todas las páginas que informa start.gg.
        /// </summary>
        public async Task<List<StartGgMatchInfo>> GetPhaseSetsAsync(long phaseId, string token,
            int perPage = SetsPerPage, int maxPages = MaxSetPages)
        {
            var sets = new List<StartGgMatchInfo>();
            if (phaseId == 0) return sets;

            int page = 1;
            int totalPages = 1;

            do
            {
                var data = await ExecuteAsync(StartGgQueries.PhaseSetsQuery, new
                {
                    phaseId = phaseId.ToString(),
                    page,
                    perPage
                }, token);

                if (data is null) break;

                if (!TryGetChild(data.Value, "phase", out var phase) ||
                    phase.ValueKind == JsonValueKind.Null)
                {
                    LastError ??= "La fase no existe o no es accesible.";
                    break;
                }

                if (!TryGetChild(phase, "sets", out var connection)) break;

                foreach (var node in EnumerateArray(connection, "nodes"))
                {
                    sets.Add(ParseSet(node));
                }

                totalPages = 1;
                if (TryGetChild(connection, "pageInfo", out var pageInfo))
                {
                    int reported = (int)GetLong(pageInfo, "totalPages");
                    if (reported > totalPages) totalPages = reported;
                }

                page++;
            }
            while (page <= totalPages && page <= maxPages);

            return sets;
        }

        /// <summary>
        /// Estructura que consume la vista principal: todas las fases del torneo
        /// (o solo las del evento indicado) con todos sus sets adentro.
        /// </summary>
        public async Task<List<StartGgPhaseSets>> GetTournamentSetsByPhaseAsync(string slug, string? eventSlug, string token)
        {
            var result = new List<StartGgPhaseSets>();

            var (tournamentSlug, parsedEventSlug) = ParseReference(slug);
            if (string.IsNullOrWhiteSpace(tournamentSlug)) return result;

            string? wantedEvent = string.IsNullOrWhiteSpace(eventSlug) ? parsedEventSlug : eventSlug;

            var events = await GetEventsWithPhasesAsync(tournamentSlug, token);
            if (events.Count == 0) return result;

            var selectedEvents = new List<StartGgEventInfo>();

            if (!string.IsNullOrWhiteSpace(wantedEvent))
            {
                foreach (var eventInfo in events)
                {
                    if (string.Equals(eventInfo.Slug, wantedEvent, StringComparison.OrdinalIgnoreCase) ||
                        eventInfo.Slug.Contains(wantedEvent!, StringComparison.OrdinalIgnoreCase) ||
                        wantedEvent!.Contains(eventInfo.Slug, StringComparison.OrdinalIgnoreCase))
                    {
                        selectedEvents.Add(eventInfo);
                    }
                }
            }

            // Si el evento pedido no aparece (nombre viejo, URL compartida) o la referencia
            // apunta solo al torneo, mostramos las fechas más recientes para no agotar el
            // rate limit de start.gg cuando un torneo acumula muchas semanas.
            if (selectedEvents.Count == 0)
            {
                selectedEvents = events
                    .OrderByDescending(e => e.StartAt)
                    .ThenByDescending(e => e.Id)
                    .Take(MaxFallbackEvents)
                    .ToList();
            }

            selectedEvents = selectedEvents
                .OrderByDescending(e => e.StartAt)
                .ThenByDescending(e => e.Id)
                .ToList();

            foreach (var eventInfo in selectedEvents)
            {
                var phases = eventInfo.Phases
                    .OrderBy(p => p.PhaseOrder)
                    .ThenBy(p => p.Id);

                foreach (var phase in phases)
                {
                    var group = new StartGgPhaseSets
                    {
                        PhaseId = phase.Id,
                        EventId = phase.EventId,
                        EventName = eventInfo.Name,
                        PhaseName = string.IsNullOrWhiteSpace(phase.Name) ? $"Fase {phase.PhaseOrder}" : phase.Name,
                        PhaseOrder = phase.PhaseOrder,
                        BracketType = phase.BracketType,
                        State = phase.State
                    };

                    var phaseSets = await GetPhaseSetsAsync(phase.Id, token);

                    // Si la fase quedó vacía por un error del API, lo guardamos para mostrarlo
                    // en la tarjeta (antes la fase aparecía vacía sin explicación).
                    if (phaseSets.Count == 0 && !string.IsNullOrWhiteSpace(LastError))
                    {
                        group.Error = LastError!;
                    }

                    foreach (var set in phaseSets)
                    {
                        if (string.IsNullOrWhiteSpace(set.PhaseName)) set.PhaseName = group.PhaseName;
                        if (string.IsNullOrWhiteSpace(set.EventName)) set.EventName = eventInfo.Name;
                        group.Sets.Add(set);
                    }

                    result.Add(group);
                }
            }

            return result;
        }

        /// <summary>
        /// Sets recientes del torneo (selector rápido del marcador).
        /// Si la referencia incluye un evento, solo se traen los sets de ese evento;
        /// si no, los de las fechas más recientes.
        /// </summary>
        public async Task<List<StartGgMatchInfo>> GetEventSetsAsync(string slug, string? eventSlug, string token,
            int perPage = SetsPerPage)
        {
            var sets = new List<StartGgMatchInfo>();

            var (tournamentSlug, parsedEventSlug) = ParseReference(slug);
            if (string.IsNullOrWhiteSpace(tournamentSlug)) return sets;

            string? wantedEvent = string.IsNullOrWhiteSpace(eventSlug) ? parsedEventSlug : eventSlug;

            var events = await GetEventsWithPhasesAsync(tournamentSlug, token);
            if (events.Count == 0) return sets;

            var targets = new List<StartGgEventInfo>();

            if (!string.IsNullOrWhiteSpace(wantedEvent))
            {
                targets = events
                    .Where(e => string.Equals(e.Slug, wantedEvent, StringComparison.OrdinalIgnoreCase) ||
                                e.Slug.Contains(wantedEvent!, StringComparison.OrdinalIgnoreCase) ||
                                wantedEvent!.Contains(e.Slug, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            if (targets.Count == 0) targets = events;

            targets = targets
                .OrderByDescending(e => e.StartAt)
                .ThenByDescending(e => e.Id)
                .Take(MaxFallbackEvents)
                .ToList();

            string? lastError = null;

            foreach (var target in targets)
            {
                var data = await ExecuteAsync(StartGgQueries.EventSetsQuery, new
                {
                    eventId = target.Id.ToString(),
                    page = 1,
                    perPage
                }, token);

                if (data is null)
                {
                    lastError ??= LastError;
                    continue;
                }

                if (!TryGetChild(data.Value, "event", out var eventElement) ||
                    eventElement.ValueKind == JsonValueKind.Null)
                {
                    continue;
                }

                if (!TryGetChild(eventElement, "sets", out var connection)) continue;

                string eventName = GetString(eventElement, "name");

                foreach (var node in EnumerateArray(connection, "nodes"))
                {
                    var match = ParseSet(node);
                    if (string.IsNullOrWhiteSpace(match.EventName)) match.EventName = eventName;
                    sets.Add(match);
                }
            }

            if (sets.Count == 0 && !string.IsNullOrWhiteSpace(lastError)) LastError = lastError;

            return sets;
        }

        /// <summary>Sets recientes del torneo (compatibilidad con la firma anterior).</summary>
        public Task<List<StartGgMatchInfo>> GetRecentSetsAsync(string slug, string token)
            => GetEventSetsAsync(slug, null, token);

        // ----------------------------------------------------------------------
        // Top 8
        // ----------------------------------------------------------------------

        /// <summary>
        /// Sets del Top 8 (fase final del evento) listos para el bracket del overlay.
        /// Las claves son: ws1, ws2, wfp, ltop81, ltop82, lq1, lq2, lsp, lfp, gfp, gfrp.
        /// </summary>
        public async Task<Dictionary<string, (string p1, string p1s, string p2, string p2s)>> GetTop8BracketSetsAsync(string slug, string token)
        {
            var dict = new Dictionary<string, (string, string, string, string)>();

            var (tournamentSlug, eventSlug) = ParseReference(slug);
            if (string.IsNullOrWhiteSpace(tournamentSlug)) return dict;

            var events = await GetEventsWithPhasesAsync(tournamentSlug, token);
            if (events.Count == 0) return dict;

            if (!string.IsNullOrWhiteSpace(eventSlug))
            {
                var matched = events
                    .Where(e => string.Equals(e.Slug, eventSlug, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (matched.Count == 0)
                {
                    matched = events
                        .Where(e => e.Slug.Contains(eventSlug!, StringComparison.OrdinalIgnoreCase))
                        .ToList();
                }

                if (matched.Count > 0) events = matched;
            }

            // La fase final (mayor phaseOrder) es la que contiene el Top 8.
            var sets = new List<StartGgMatchInfo>();

            foreach (var eventInfo in events)
            {
                var phases = eventInfo.Phases
                    .OrderByDescending(p => p.PhaseOrder)
                    .ThenByDescending(p => p.Id);

                foreach (var phase in phases)
                {
                    var phaseSets = await GetPhaseSetsAsync(phase.Id, token);
                    if (phaseSets.Count == 0) continue;

                    sets.AddRange(phaseSets);
                    break;
                }
            }

            if (sets.Count == 0) return dict;

            return MapTop8Sets(sets);
        }

        /// <summary>
        /// Asigna cada set del Top 8 a la casilla que le corresponde en el bracket del overlay.
        /// Claves: ws1, ws2, wfp, ltop81, ltop82, lq1, lq2, lsp, lfp, gfp, gfrp.
        /// </summary>
        internal static Dictionary<string, (string p1, string p1s, string p2, string p2s)> MapTop8Sets(
            IEnumerable<StartGgMatchInfo> sets)
        {
            var dict = new Dictionary<string, (string p1, string p1s, string p2, string p2s)>();

            var ordered = sets
                .OrderBy(s => s.Round)
                .ThenBy(s => s.SetId)
                .ToList();

            // Rondas del losers bracket sin nombre explícito ("Losers Round 1", "Losers Round 2"...):
            // se resuelven después, por orden cronológico.
            var plainLosersRounds = new List<StartGgMatchInfo>();

            int winnersSemis = 1;
            int losersQuarters = 1;
            int losersTop8 = 1;

            foreach (var set in ordered)
            {
                string roundText = NormalizeRoundText(set.RoundText);
                if (roundText.Length == 0) continue;

                var value = (set.P1Name, set.P1Score, set.P2Name, set.P2Score);

                if (roundText.Contains("grand final") && roundText.Contains("reset"))
                {
                    dict["gfrp"] = value;
                }
                else if (roundText.Contains("grand final"))
                {
                    dict["gfp"] = value;
                }
                else if (roundText.Contains("winners final"))
                {
                    dict["wfp"] = value;
                }
                else if (roundText.Contains("losers final"))
                {
                    dict["lfp"] = value;
                }
                else if (roundText.Contains("losers semi"))
                {
                    dict["lsp"] = value;
                }
                else if (roundText.Contains("losers quarter"))
                {
                    dict[$"lq{losersQuarters}"] = value;
                    losersQuarters++;
                }
                else if (roundText.Contains("winners semi"))
                {
                    dict[$"ws{winnersSemis}"] = value;
                    winnersSemis++;
                }
                else if (roundText.Contains("losers"))
                {
                    plainLosersRounds.Add(set);
                }
            }

            // En un Top 8, la última ronda previa a las semis del losers son los cuartos (lq)
            // y las anteriores son el Top 8 del losers (ltop8).
            var rounds = plainLosersRounds
                .GroupBy(s => s.Round)
                .OrderBy(g => g.Key)
                .ToList();

            for (int i = 0; i < rounds.Count; i++)
            {
                bool isQuarterFinalRound = rounds.Count > 1 && i == rounds.Count - 1;

                foreach (var set in rounds[i].OrderBy(s => s.SetId))
                {
                    var value = (set.P1Name, set.P1Score, set.P2Name, set.P2Score);

                    if (isQuarterFinalRound)
                    {
                        dict[$"lq{losersQuarters}"] = value;
                        losersQuarters++;
                    }
                    else
                    {
                        dict[$"ltop8{losersTop8}"] = value;
                        losersTop8++;
                    }
                }
            }

            return dict;
        }

        /// <summary>
        /// start.gg devuelve <c>state</c> como número (1 = no iniciado, 2 = en curso,
        /// 3 = finalizado) aunque el esquema lo declare enum. Lo normalizamos al nombre
        /// del enum para poder compararlo y mostrarlo.
        /// </summary>
        private static string NormalizeState(string raw) => raw switch
        {
            "1" => "CREATED",
            "2" => "ACTIVE",
            "3" => "COMPLETED",
            _ => raw
        };

        private static string NormalizeRoundText(string text)        {
            if (string.IsNullOrWhiteSpace(text)) return "";

            string normalized = text
                .ToLowerInvariant()
                .Replace('-', ' ')
                .Replace('_', ' ');

            while (normalized.Contains("  ")) normalized = normalized.Replace("  ", " ");

            return normalized.Trim();
        }

        // ----------------------------------------------------------------------
        // Parseo de sets / slots
        // ----------------------------------------------------------------------

        private StartGgMatchInfo ParseSet(JsonElement node)
        {
            var match = new StartGgMatchInfo
            {
                SetId = GetLong(node, "id"),
                RoundText = GetString(node, "fullRoundText"),
                DisplayScore = GetString(node, "displayScore"),
                State = NormalizeState(GetString(node, "state")),
                Round = (int)GetLong(node, "round"),
                Identifier = GetString(node, "identifier")
            };

            if (TryGetChild(node, "phaseGroup", out var group))
            {
                match.PoolName = GetString(group, "displayIdentifier");

                if (TryGetChild(group, "phase", out var phase))
                {
                    match.PhaseName = GetString(phase, "name");
                }
            }

            if (TryGetChild(node, "event", out var eventElement))
            {
                match.EventName = GetString(eventElement, "name");
            }

            int slotIndex = 0;

            foreach (var slot in EnumerateArray(node, "slots"))
            {
                ParseSlot(slot, out var tag, out var name, out var country, out var score);

                if (slotIndex == 0)
                {
                    match.P1Tag = tag;
                    match.P1Name = name;
                    match.P1Country = country;
                    match.P1Score = score;
                }
                else if (slotIndex == 1)
                {
                    match.P2Tag = tag;
                    match.P2Name = name;
                    match.P2Country = country;
                    match.P2Score = score;
                }

                slotIndex++;
            }

            return match;
        }

        private static void ParseSlot(JsonElement slot, out string tag, out string name, out string country, out string score)
        {
            tag = "";
            name = "";
            country = "";
            score = "0";

            if (TryGetChild(slot, "entrant", out var entrant) && entrant.ValueKind == JsonValueKind.Object)
            {
                name = GetString(entrant, "name");

                foreach (var participant in EnumerateArray(entrant, "participants"))
                {
                    string gamerTag = GetString(participant, "gamerTag");
                    if (!string.IsNullOrWhiteSpace(gamerTag)) name = gamerTag;

                    string prefix = GetString(participant, "prefix");
                    if (!string.IsNullOrWhiteSpace(prefix)) tag = prefix;

                    if (TryGetChild(participant, "user", out var user) &&
                        TryGetChild(user, "location", out var location))
                    {
                        string countryCode = GetString(location, "country");
                        if (!string.IsNullOrWhiteSpace(countryCode)) country = countryCode;
                    }
                }
            }

            if (TryGetChild(slot, "standing", out var standing) &&
                TryGetChild(standing, "stats", out var stats) &&
                TryGetChild(stats, "score", out var scoreElement))
            {
                score = ReadScore(scoreElement);
            }
        }

        private static string ReadScore(JsonElement scoreElement)
        {
            if (scoreElement.ValueKind != JsonValueKind.Object ||
                !scoreElement.TryGetProperty("value", out var value))
            {
                return "0";
            }

            switch (value.ValueKind)
            {
                case JsonValueKind.Number:
                    if (value.TryGetInt32(out var intValue)) return Math.Max(0, intValue).ToString();
                    return Math.Max(0, value.GetDouble()).ToString("0.##");

                case JsonValueKind.String:
                    string raw = value.GetString() ?? "0";
                    // start.gg marca las descalificaciones con -1; el marcador no debe mostrarlo.
                    return int.TryParse(raw, out var parsed) && parsed < 0 ? "0" : raw;

                default:
                    return "0";
            }
        }

        // ----------------------------------------------------------------------
        // Helpers de JsonElement (nunca lanzan: un campo ausente rompía todo el parseo)
        // ----------------------------------------------------------------------

        private static bool TryGetChild(JsonElement element, string property, out JsonElement value)
        {
            value = default;

            if (element.ValueKind != JsonValueKind.Object ||
                !element.TryGetProperty(property, out var child) ||
                child.ValueKind == JsonValueKind.Null)
            {
                return false;
            }

            value = child;
            return true;
        }

        private static IEnumerable<JsonElement> EnumerateArray(JsonElement element, string property)
        {
            if (element.ValueKind != JsonValueKind.Object ||
                !element.TryGetProperty(property, out var array) ||
                array.ValueKind != JsonValueKind.Array)
            {
                yield break;
            }

            foreach (var item in array.EnumerateArray())
            {
                yield return item;
            }
        }

        private static string GetString(JsonElement element, string property)
        {
            if (element.ValueKind != JsonValueKind.Object ||
                !element.TryGetProperty(property, out var value))
            {
                return "";
            }

            return value.ValueKind switch
            {
                JsonValueKind.String => value.GetString() ?? "",
                JsonValueKind.Number => value.GetRawText(),
                _ => ""
            };
        }

        private static long GetLong(JsonElement element, string property)
        {
            if (element.ValueKind != JsonValueKind.Object ||
                !element.TryGetProperty(property, out var value))
            {
                return 0;
            }

            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var number)) return number;

            if (value.ValueKind == JsonValueKind.String &&
                long.TryParse(value.GetString(), out var parsed))
            {
                return parsed;
            }

            return 0;
        }
    }
}
