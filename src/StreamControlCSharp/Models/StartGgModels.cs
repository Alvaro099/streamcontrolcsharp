using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;

namespace StreamControlCSharp.Models
{
    /// <summary>
    /// Información de una pelea (set) de start.gg ya normalizada para el marcador.
    /// </summary>
    public class StartGgMatchInfo
    {
        public long SetId { get; set; }
        public string RoundText { get; set; } = "";

        /// <summary>Número de ronda dentro de la fase (puede ser negativo en el losers bracket).</summary>
        public int Round { get; set; }

        /// <summary>Identificador del set dentro de la ronda (A, B, C...).</summary>
        public string Identifier { get; set; } = "";

        /// <summary>Nombre de la fase a la que pertenece el set.</summary>
        public string PhaseName { get; set; } = "";

        /// <summary>Nombre del evento (ej: "Tekken 8 Singles").</summary>
        public string EventName { get; set; } = "";

        /// <summary>Identificador del pool/grupo (ej: "Pool A1"), cuando aplica.</summary>
        public string PoolName { get; set; } = "";

        public string P1Name { get; set; } = "";
        public string P1Tag { get; set; } = "";
        public string P1Country { get; set; } = "";
        public string P1Score { get; set; } = "0";

        public string P2Name { get; set; } = "";
        public string P2Tag { get; set; } = "";
        public string P2Country { get; set; } = "";
        public string P2Score { get; set; } = "0";

        /// <summary>
        /// Estado devuelto por start.gg como texto:
        /// CREATED, READY, QUEUED, CALLED, ACTIVE, COMPLETED o INVALID.
        /// </summary>
        public string State { get; set; } = "";

        /// <summary>Marcador tal cual lo muestra start.gg (ej: "2 - 1").</summary>
        public string DisplayScore { get; set; } = "";

        // ---- Propiedades calculadas para la vista -------------------------------

        public bool IsCompleted => State == "COMPLETED";

        public bool IsInProgress => State == "ACTIVE" || State == "CALLED";

        public string StateLabel => State switch
        {
            "ACTIVE" => "En curso",
            "CALLED" => "Llamado",
            "COMPLETED" => "Finalizado",
            "READY" => "Listo",
            "CREATED" => "Creado",
            "QUEUED" => "En cola",
            "INVALID" => "Inválido",
            _ => State
        };

        /// <summary>Jugador 1 con prefijo/equipo incluido (solo el que exista).</summary>
        public string P1Display => FormatPlayer(P1Tag, P1Name);

        /// <summary>Jugador 2 con prefijo/equipo incluido (solo el que exista).</summary>
        public string P2Display => FormatPlayer(P2Tag, P2Name);

        public string ScoreText => string.IsNullOrWhiteSpace(DisplayScore)
            ? $"{P1Score} - {P2Score}"
            : DisplayScore;

        public string TooltipText =>
            $"{(string.IsNullOrWhiteSpace(RoundText) ? "Set" : RoundText)}: {P1Display} {ScoreText} {P2Display} ({StateLabel})";

        private static string FormatPlayer(string tag, string name)
        {
            bool hasTag = !string.IsNullOrWhiteSpace(tag);
            bool hasName = !string.IsNullOrWhiteSpace(name);

            if (hasTag && hasName)
            {
                // Si el nombre ya incluye el prefijo no lo duplicamos.
                return name.StartsWith(tag, System.StringComparison.OrdinalIgnoreCase) ? name : $"{tag} {name}";
            }

            if (hasName) return name;
            if (hasTag) return tag;
            return "Por definir";
        }

        public override string ToString() => $"{RoundText}: {P1Display} vs {P2Display} ({ScoreText})";
    }

    /// <summary>Torneo con su cola de stream.</summary>
    public class StartGgTournamentInfo
    {
        public long Id { get; set; }
        public string Name { get; set; } = "";
        public List<StartGgStreamQueueEntry> StreamQueue { get; set; } = new();
    }

    public class StartGgStreamQueueEntry
    {
        public string StreamName { get; set; } = "";
        public string StreamSource { get; set; } = "";
        public List<long> SetIds { get; set; } = new();
    }

    /// <summary>Evento del torneo junto a sus fases creadas.</summary>
    public class StartGgEventInfo
    {
        public long Id { get; set; }
        public string Name { get; set; } = "";
        public string Slug { get; set; } = "";
        public int NumEntrants { get; set; }

        /// <summary>Fecha de inicio del evento (timestamp Unix); 0 si no está definida.</summary>
        public long StartAt { get; set; }

        public List<StartGgPhaseInfo> Phases { get; } = new();
    }

    /// <summary>Fase creada dentro de un evento.</summary>
    public class StartGgPhaseInfo
    {
        public long Id { get; set; }
        public long EventId { get; set; }
        public string EventName { get; set; } = "";
        public string EventSlug { get; set; } = "";
        public string Name { get; set; } = "";
        public int PhaseOrder { get; set; }
        public string BracketType { get; set; } = "";
        public string State { get; set; } = "";
    }

    /// <summary>
    /// Una fase del torneo con TODOS sus sets: es la unidad que muestra la vista
    /// "Sets del torneo por fase".
    /// </summary>
    public class StartGgPhaseSets
    {
        public long PhaseId { get; set; }
        public long EventId { get; set; }
        public string EventName { get; set; } = "";
        public string PhaseName { get; set; } = "";
        public int PhaseOrder { get; set; }
        public string BracketType { get; set; } = "";
        public string State { get; set; } = "";

        /// <summary>Motivo por el que la fase no pudo cargarse (vacío si cargó bien).</summary>
        public string Error { get; set; } = "";

        public ObservableCollection<StartGgMatchInfo> Sets { get; } = new();

        public Visibility ErrorVisibility =>
            string.IsNullOrWhiteSpace(Error) ? Visibility.Collapsed : Visibility.Visible;

        public int SetCount => Sets.Count;

        public string OrderLabel => PhaseOrder > 0 ? $"FASE {PhaseOrder}" : "FASE";

        public string BracketLabel => BracketType switch
        {
            "SINGLE_ELIMINATION" => "Eliminación directa",
            "DOUBLE_ELIMINATION" => "Doble eliminación",
            "ROUND_ROBIN" => "Round Robin",
            "SWISS" => "Suizo",
            "EXHIBITION" => "Exhibición",
            "CUSTOM_SCHEDULE" => "Personalizado",
            _ => ""
        };

        public string Title => string.IsNullOrWhiteSpace(EventName) ? PhaseName : $"{EventName} · {PhaseName}";

        /// <summary>Resumen legible: "24 sets · 10 finalizados · 2 en curso".</summary>
        public string Summary
        {
            get
            {
                int completed = 0;
                int live = 0;

                foreach (var set in Sets)
                {
                    if (set.IsCompleted) completed++;
                    else if (set.IsInProgress) live++;
                }

                var parts = new List<string> { Sets.Count == 1 ? "1 set" : $"{Sets.Count} sets" };
                if (completed > 0) parts.Add(completed == 1 ? "1 finalizado" : $"{completed} finalizados");
                if (live > 0) parts.Add($"{live} en curso");

                return string.Join(" · ", parts);
            }
        }
    }
}
