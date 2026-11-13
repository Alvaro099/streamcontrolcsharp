namespace StreamControlCSharp.Services
{
    /// <summary>
    /// Catálogo de consultas GraphQL de start.gg (API v1 / alpha).
    ///
    /// Está basado en el archivo <c>queries.py</c> del proyecto original, corregido
    /// contra el esquema vigente (https://developer.start.gg/reference/query.doc.html)
    /// y probado contra la API real:
    ///
    ///  - EVENT_QUERY           -> <see cref="TournamentEventsQuery"/>  (torneo -> eventos -> fases)
    ///  - BRACKET_QUERY         -> <see cref="PhaseSetsQuery"/>         (fase -> TODOS sus sets, paginado)
    ///  - SET_QUERY/MATCH_QUERY -> <see cref="SetQuery"/>               (set puntual, ya sincronizado)
    ///  - STREAM_QUERY          -> <see cref="StreamQueueQuery"/>       (cola de stream del torneo)
    ///  - (sets recientes)      -> <see cref="EventSetsQuery"/>         (evento -> sets más recientes)
    ///
    /// Correcciones aplicadas respecto del original:
    ///  - La API devuelve <c>state</c> como número (1 = no iniciado, 2 = en curso,
    ///    3 = finalizado); el servicio lo normaliza a texto antes de usarlo.
    ///  - <c>sortType: STANDARD</c> está deprecado; se usa ROUND (orden por ronda) para el
    ///    listado por fase y RECENT para la lista de peleas recientes.
    ///  - start.gg corta cualquier request que devuelva más de 1000 objetos: por eso los
    ///    sets se piden de a 30 por página (con estos campos, ~25 objetos por set) y no
    ///    se consultan los sets de todos los eventos del torneo en una sola query.
    ///  - BRACKET_GRAPHIC_QUERY del original le pasaba un <c>phaseId</c> a
    ///    <c>phaseGroup(id:)</c>, así que devolvía datos incorrectos: se descartó.
    ///  - POOL_QUERY no hace falta: cada set ya trae <c>phaseGroup.displayIdentifier</c>.
    ///  - Los campos de un set se centralizan en un fragment (<see cref="SetFields"/>) para
    ///    que todas las consultas traigan exactamente la misma información.
    /// </summary>
    internal static class StartGgQueries
    {
        /// <summary>
        /// Campos mínimos y suficientes para cargar una pelea al marcador:
        /// ronda, estado, puntajes y los dos lados (entrant + participantes + país).
        /// </summary>
        internal const string SetFields = """
            fragment SetFields on Set {
              id
              state
              displayScore
              fullRoundText
              round
              identifier
              phaseGroup {
                id
                displayIdentifier
                phase { id name phaseOrder }
              }
              event { id name }
              slots {
                id
                entrant {
                  id
                  name
                  participants {
                    gamerTag
                    prefix
                    user { location { country } }
                  }
                }
                standing { stats { score { value } } }
              }
            }
            """;

        /// <summary>Torneo con todos sus eventos y las fases creadas en cada evento.</summary>
        internal const string TournamentEventsQuery = """
            query TournamentEvents($tourneySlug: String!) {
              tournament(slug: $tourneySlug) {
                id
                name
                events {
                  id
                  name
                  slug
                  numEntrants
                  startAt
                  phases {
                    id
                    name
                    phaseOrder
                    bracketType
                    state
                  }
                }
              }
            }
            """;

        /// <summary>
        /// Todos los sets de una fase (pools y bracket incluidos), paginados.
        /// Es la consulta que alimenta la vista "Sets del torneo por fase".
        /// </summary>
        internal static readonly string PhaseSetsQuery = $$"""
            query PhaseSets($phaseId: ID!, $page: Int!, $perPage: Int!) {
              phase(id: $phaseId) {
                id
                name
                phaseOrder
                bracketType
                state
                sets(page: $page, perPage: $perPage, sortType: ROUND) {
                  pageInfo { total totalPages page perPage }
                  nodes { ...SetFields }
                }
              }
            }

            {{SetFields}}
            """;

        /// <summary>Detalle de un set puntual (sincronización del marcador).</summary>
        internal static readonly string SetQuery = $$"""
            query SetDetails($setId: ID!) {
              set(id: $setId) {
                ...SetFields
              }
            }

            {{SetFields}}
            """;

        /// <summary>Cola de stream del torneo (qué pelea toca en vivo).</summary>
        internal const string StreamQueueQuery = """
            query TournamentStreamQueue($tourneySlug: String!) {
              tournament(slug: $tourneySlug) {
                id
                name
                streamQueue {
                  id
                  stream {
                    id
                    streamName
                    streamSource
                  }
                  sets {
                    id
                    state
                    fullRoundText
                    displayScore
                  }
                }
              }
            }
            """;

        /// <summary>
        /// Sets más recientes de UN evento (selector rápido del marcador).
        /// Se pide por evento y no por torneo: consultar los sets de todos los eventos
        /// de una temporada supera el límite de complejidad de start.gg.
        /// </summary>
        internal static readonly string EventSetsQuery = $$"""
            query EventSets($eventId: ID!, $page: Int!, $perPage: Int!) {
              event(id: $eventId) {
                id
                name
                slug
                sets(page: $page, perPage: $perPage, sortType: RECENT) {
                  pageInfo { total totalPages }
                  nodes { ...SetFields }
                }
              }
            }

            {{SetFields}}
            """;
    }
}
