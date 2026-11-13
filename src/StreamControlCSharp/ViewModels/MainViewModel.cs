using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using StreamControlCSharp.Models;
using StreamControlCSharp.Services;

namespace StreamControlCSharp.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly JsonStorageService _jsonStorage = new();
        private readonly StartGgService _startGgService = new();
        private readonly CsvService _csvService = new();
        private readonly DispatcherTimer _autoSyncTimer;

        private StreamControlData _data = new();
        private AppSettings _settings = new();
        private string _statusMessage = "Listo.";
        private bool _isBusy = false;
        private StartGgMatchInfo? _selectedRecentSet;

        public event PropertyChangedEventHandler? PropertyChanged;

        public StreamControlData Data
        {
            get => _data;
            set { _data = value; OnPropertyChanged(); }
        }

        public AppSettings Settings
        {
            get => _settings;
            set { _settings = value; OnPropertyChanged(); }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set { _statusMessage = value; OnPropertyChanged(); }
        }

        public bool IsBusy
        {
            get => _isBusy;
            set { _isBusy = value; OnPropertyChanged(); }
        }

        public StartGgMatchInfo? SelectedRecentSet
        {
            get => _selectedRecentSet;
            set
            {
                _selectedRecentSet = value;
                OnPropertyChanged();
                if (value != null)
                {
                    ApplyMatchToScoreboard(value);
                }
            }
        }

        public ObservableCollection<PlayerEntry> Players { get; } = new();
        public ObservableCollection<CommentatorEntry> Commentators { get; } = new();
        public ObservableCollection<string> Rounds { get; } = new();
        public ObservableCollection<string> TextBottomOptions { get; } = new();
        public ObservableCollection<string> Top8TextOptions { get; } = new();
        public ObservableCollection<StartGgMatchInfo> RecentSets { get; } = new();

        /// <summary>Todas las fases del torneo, cada una con todos sus sets.</summary>
        public ObservableCollection<StartGgPhaseSets> TournamentPhases { get; } = new();

        private string _tournamentSetsSummary = "Sin cargar";
        public string TournamentSetsSummary
        {
            get => _tournamentSetsSummary;
            set { _tournamentSetsSummary = value; OnPropertyChanged(); }
        }

        public Visibility PhasesEmptyVisibility =>
            TournamentPhases.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        public Visibility PhasesListVisibility =>
            TournamentPhases.Count == 0 ? Visibility.Collapsed : Visibility.Visible;

        // Commands
        public ICommand SaveCommand { get; }
        public ICommand SwapPlayersCommand { get; }
        public ICommand ResetScoresCommand { get; }
        public ICommand ScoreP1PlusCommand { get; }
        public ICommand ScoreP1MinusCommand { get; }
        public ICommand ScoreP2PlusCommand { get; }
        public ICommand ScoreP2MinusCommand { get; }
        public ICommand SetScoreP1Command { get; }
        public ICommand SetScoreP2Command { get; }
        public ICommand SyncCurrentMatchCommand { get; }
        public ICommand SyncTop8Command { get; }
        public ICommand RefreshRecentSetsCommand { get; }
        public ICommand LoadTournamentSetsCommand { get; }
        public ICommand LoadSetCommand { get; }
        public ICommand ClearBracketCommand { get; }

        private CommentatorEntry? _selectedComm1;
        public CommentatorEntry? SelectedComm1
        {
            get => _selectedComm1;
            set
            {
                _selectedComm1 = value;
                OnPropertyChanged();
                if (value != null)
                {
                    Data.Comm1Name = value.Name;
                    Data.Comm1Twit = value.Twitter;
                    NotifyScoreboard();
                    Save();
                }
            }
        }

        private CommentatorEntry? _selectedComm2;
        public CommentatorEntry? SelectedComm2
        {
            get => _selectedComm2;
            set
            {
                _selectedComm2 = value;
                OnPropertyChanged();
                if (value != null)
                {
                    Data.Comm2Name = value.Name;
                    Data.Comm2Twit = value.Twitter;
                    NotifyScoreboard();
                    Save();
                }
            }
        }

        private CommentatorEntry? _selectedComm3;
        public CommentatorEntry? SelectedComm3
        {
            get => _selectedComm3;
            set
            {
                _selectedComm3 = value;
                OnPropertyChanged();
                if (value != null)
                {
                    Data.Comm3Name = value.Name;
                    Data.Comm3Twit = value.Twitter;
                    NotifyScoreboard();
                    Save();
                }
            }
        }

        public MainViewModel()
        {
            LoadSettings();
            LoadData();
            LoadDatasets();

            SaveCommand = new RelayCommand(Save);
            SwapPlayersCommand = new RelayCommand(SwapPlayers);
            ResetScoresCommand = new RelayCommand(ResetScores);
            ScoreP1PlusCommand = new RelayCommand(() => IncrementScore(1, 1));
            ScoreP1MinusCommand = new RelayCommand(() => IncrementScore(1, -1));
            ScoreP2PlusCommand = new RelayCommand(() => IncrementScore(2, 1));
            ScoreP2MinusCommand = new RelayCommand(() => IncrementScore(2, -1));
            SetScoreP1Command = new RelayCommand(p => SetScore(1, p?.ToString() ?? "0"));
            SetScoreP2Command = new RelayCommand(p => SetScore(2, p?.ToString() ?? "0"));

            SyncCurrentMatchCommand = new RelayCommand(async () => await SyncCurrentMatchAsync());
            SyncTop8Command = new RelayCommand(async () => await SyncTop8Async());
            RefreshRecentSetsCommand = new RelayCommand(async () => await LoadRecentSetsAsync());
            LoadTournamentSetsCommand = new RelayCommand(async () => await LoadTournamentSetsAsync());
            LoadSetCommand = new RelayCommand(p =>
            {
                if (p is StartGgMatchInfo set) LoadSetToScoreboard(set);
            });
            ClearBracketCommand = new RelayCommand(ClearBracket);

            _autoSyncTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(Math.Max(5, Settings.AutoSyncIntervalSeconds))
            };
            _autoSyncTimer.Tick += async (s, e) =>
            {
                if (Settings.AutoSyncEnabled && !IsBusy)
                {
                    await SyncCurrentMatchAsync(silent: true);
                }
            };

            if (Settings.AutoSyncEnabled)
            {
                _autoSyncTimer.Start();
            }
        }

        private void LoadSettings()
        {
            try
            {
                string configPath = Path.Combine(AppContext.BaseDirectory, "config.json");
                if (File.Exists(configPath))
                {
                    string json = File.ReadAllText(configPath);
                    var s = JsonSerializer.Deserialize<AppSettings>(json);
                    if (s != null) Settings = s;
                }
            }
            catch { }
        }

        public void SaveSettings()
        {
            try
            {
                string configPath = Path.Combine(AppContext.BaseDirectory, "config.json");
                string json = JsonSerializer.Serialize(Settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(configPath, json);
            }
            catch { }
        }

        private void LoadData()
        {
            Data = _jsonStorage.Load(Settings.OutputDirectory);
        }

        private void LoadDatasets()
        {
            Players.Clear();
            foreach (var p in _csvService.LoadPlayers()) Players.Add(p);

            Commentators.Clear();
            foreach (var c in _csvService.LoadCommentators()) Commentators.Add(c);

            Rounds.Clear();
            foreach (var r in _csvService.LoadSimpleList("round.csv")) Rounds.Add(r);

            TextBottomOptions.Clear();
            foreach (var t in _csvService.LoadSimpleList("text.csv")) TextBottomOptions.Add(t);

            Top8TextOptions.Clear();
            foreach (var t in _csvService.LoadSimpleList("textos-top8.csv")) Top8TextOptions.Add(t);
        }

        public void Save()
        {
            // Auto add new players to CSV dataset if entered
            _csvService.SaveNewPlayer(Data.PName1, Data.PCountry1, Data.P1Team);
            _csvService.SaveNewPlayer(Data.PName2, Data.PCountry2, Data.P2Team);

            bool success = _jsonStorage.Save(Data, Settings.OutputDirectory);
            if (success)
            {
                StatusMessage = $"Guardado correctamente en OBS ({DateTime.Now:HH:mm:ss})";
            }
            else
            {
                StatusMessage = "Error al guardar streamcontrol.json. Verifique permisos.";
            }

            SaveSettings();
        }

        public void SwapPlayers()
        {
            string tmpTeam = Data.P1Team;
            string tmpName = Data.PName1;
            string tmpCountry = Data.PCountry1;
            string tmpScore = Data.PScore1;

            Data.P1Team = Data.P2Team;
            Data.PName1 = Data.PName2;
            Data.PCountry1 = Data.PCountry2;
            Data.PScore1 = Data.PScore2;

            Data.P2Team = tmpTeam;
            Data.PName2 = tmpName;
            Data.PCountry2 = tmpCountry;
            Data.PScore2 = tmpScore;

            NotifyScoreboard();
            Save();
        }

        public void ResetScores()
        {
            Data.PScore1 = "0";
            Data.PScore2 = "0";
            NotifyScoreboard();
            Save();
        }

        public void IncrementScore(int player, int change)
        {
            if (player == 1)
            {
                int.TryParse(Data.PScore1, out int current);
                current = Math.Max(0, current + change);
                Data.PScore1 = current.ToString();
            }
            else
            {
                int.TryParse(Data.PScore2, out int current);
                current = Math.Max(0, current + change);
                Data.PScore2 = current.ToString();
            }

            NotifyScoreboard();
            Save();
        }

        public void SetScore(int player, string score)
        {
            if (player == 1)
            {
                Data.PScore1 = score;
            }
            else
            {
                Data.PScore2 = score;
            }

            NotifyScoreboard();
            Save();
        }

        private void NotifyScoreboard()
        {
            OnPropertyChanged(nameof(Data));
        }

        public async Task SyncCurrentMatchAsync(bool silent = false)
        {
            if (string.IsNullOrWhiteSpace(Settings.StartGgToken) || string.IsNullOrWhiteSpace(Settings.TournamentSlug))
            {
                if (!silent) StatusMessage = "Debe configurar el Token y el Slug del torneo en la pestaña Start.gg.";
                return;
            }

            IsBusy = true;
            if (!silent) StatusMessage = "Consultando Start.gg...";

            try
            {
                // Step 1: Check stream queue
                var tourney = await _startGgService.GetTournamentInfoAsync(Settings.TournamentSlug, Settings.StartGgToken);
                long targetSetId = 0;

                if (tourney != null && tourney.StreamQueue.Count > 0)
                {
                    // Find stream queue matching StreamName or default to first
                    StartGgStreamQueueEntry? targetQueue = null;
                    if (!string.IsNullOrWhiteSpace(Settings.StreamName))
                    {
                        targetQueue = tourney.StreamQueue.Find(q =>
                            q.StreamName.Contains(Settings.StreamName, StringComparison.OrdinalIgnoreCase));
                    }
                    targetQueue ??= tourney.StreamQueue[0];

                    if (targetQueue.SetIds.Count > 0)
                    {
                        targetSetId = targetQueue.SetIds[0];
                    }
                }

                if (targetSetId == 0)
                {
                    // Fallback to recent sets
                    var recent = await _startGgService.GetRecentSetsAsync(Settings.TournamentSlug, Settings.StartGgToken);
                    if (recent.Count > 0)
                    {
                        // Prefer in-progress set (ACTIVE / CALLED)
                        var active = recent.Find(s => s.IsInProgress) ?? recent[0];
                        targetSetId = active.SetId;
                    }
                }

                if (targetSetId != 0)
                {
                    var match = await _startGgService.GetSetDetailsAsync(targetSetId, Settings.StartGgToken);
                    if (match != null)
                    {
                        ApplyMatchToScoreboard(match);
                        Save();
                        StatusMessage = $"Sincronizado: {match.RoundText} ({match.P1Name} vs {match.P2Name})";
                    }
                    else
                    {
                        if (!silent) StatusMessage = "No se pudieron obtener los detalles del set.";
                    }
                }
                else
                {
                    if (!silent) StatusMessage = "No se encontraron sets activos en la cola de stream ni en el torneo.";
                }
            }
            catch (Exception ex)
            {
                if (!silent) StatusMessage = $"Error al sincronizar: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        public void ApplyMatchToScoreboard(StartGgMatchInfo match)
        {
            Data.PName1 = match.P1Name;
            Data.P1Team = match.P1Tag;
            if (!string.IsNullOrEmpty(match.P1Country)) Data.PCountry1 = match.P1Country;
            Data.PScore1 = match.P1Score;

            Data.PName2 = match.P2Name;
            Data.P2Team = match.P2Tag;
            if (!string.IsNullOrEmpty(match.P2Country)) Data.PCountry2 = match.P2Country;
            Data.PScore2 = match.P2Score;

            if (!string.IsNullOrEmpty(match.RoundText))
            {
                Data.Round = match.RoundText;
                Data.TextBottom = match.RoundText;
            }

            NotifyScoreboard();
        }

        public async Task LoadRecentSetsAsync()
        {
            if (string.IsNullOrWhiteSpace(Settings.StartGgToken) || string.IsNullOrWhiteSpace(Settings.TournamentSlug))
            {
                StatusMessage = "Configure Token y Slug de Start.gg.";
                return;
            }

            IsBusy = true;
            StatusMessage = "Cargando sets del torneo...";

            try
            {
                var sets = await _startGgService.GetRecentSetsAsync(Settings.TournamentSlug, Settings.StartGgToken);
                RecentSets.Clear();
                foreach (var s in sets) RecentSets.Add(s);

                if (sets.Count == 0)
                {
                    StatusMessage = BuildStartGgError("No se encontraron sets en el torneo.",
                        _startGgService.LastError);
                }
                else
                {
                    StatusMessage = $"Se cargaron {sets.Count} sets del torneo.";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error al cargar sets: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>
        /// Carga TODAS las fases del torneo (las creadas en Start.gg) con todos sus sets.
        /// Alimenta la tarjeta "Sets del torneo por fase" de la pestaña Start.gg.
        /// </summary>
        public async Task LoadTournamentSetsAsync(bool silent = false)
        {
            if (string.IsNullOrWhiteSpace(Settings.StartGgToken) || string.IsNullOrWhiteSpace(Settings.TournamentSlug))
            {
                if (!silent) StatusMessage = "Configure Token y Slug de Start.gg.";
                return;
            }

            IsBusy = true;
            if (!silent) StatusMessage = "Cargando fases y sets del torneo...";

            try
            {
                var phases = await _startGgService.GetTournamentSetsByPhaseAsync(
                    Settings.TournamentSlug, null, Settings.StartGgToken);

                TournamentPhases.Clear();

                int totalSets = 0;
                foreach (var phase in phases)
                {
                    TournamentPhases.Add(phase);
                    totalSets += phase.SetCount;
                }

                OnPropertyChanged(nameof(PhasesEmptyVisibility));
                OnPropertyChanged(nameof(PhasesListVisibility));

                if (phases.Count == 0)
                {
                    string? apiError = _startGgService.LastError;
                    TournamentSetsSummary = string.IsNullOrWhiteSpace(apiError) ? "Sin fases" : "No se pudo cargar";

                    if (!silent)
                    {
                        StatusMessage = BuildStartGgError(
                            "No se encontraron fases en el torneo.", apiError);
                    }
                }
                else
                {
                    TournamentSetsSummary = $"{phases.Count} fase(s) · {totalSets} sets";
                    StatusMessage = $"Se cargaron {totalSets} sets en {phases.Count} fase(s).";
                }
            }
            catch (Exception ex)
            {
                if (!silent) StatusMessage = $"Error al cargar las fases: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>Carga un set del listado de fases directamente al marcador.</summary>
        public void LoadSetToScoreboard(StartGgMatchInfo set)
        {
            if (set == null) return;

            ApplyMatchToScoreboard(set);
            Save();

            StatusMessage = $"Set cargado: {set.RoundText} · {set.P1Display} vs {set.P2Display}";
        }

        private static string BuildStartGgError(string fallback, string? apiError)
        {
            return string.IsNullOrWhiteSpace(apiError) ? fallback : $"{fallback} ({apiError})";
        }

        public async Task SyncTop8Async()
        {
            if (string.IsNullOrWhiteSpace(Settings.StartGgToken) || string.IsNullOrWhiteSpace(Settings.TournamentSlug))
            {
                StatusMessage = "Configure Token y Slug de Start.gg.";
                return;
            }

            IsBusy = true;
            StatusMessage = "Consultando Top 8 en Start.gg...";

            try
            {
                var top8 = await _startGgService.GetTop8BracketSetsAsync(Settings.TournamentSlug, Settings.StartGgToken);
                if (top8.Count == 0)
                {
                    StatusMessage = BuildStartGgError("No se encontraron sets de Top 8.",
                        _startGgService.LastError);
                    return;
                }

                // Partimos de un bracket vacío: si el torneo no tiene algún set (p. ej. un Grand
                // Final Reset que no se jugó), no queremos que queden los datos del torneo anterior.
                ClearBracketSlots();

                if (top8.TryGetValue("ws1", out var ws1)) { Data.Ws1p1 = ws1.p1; Data.Ws1p1s = ws1.p1s; Data.Ws1p2 = ws1.p2; Data.Ws1p2s = ws1.p2s; }
                if (top8.TryGetValue("ws2", out var ws2)) { Data.Ws2p1 = ws2.p1; Data.Ws2p1s = ws2.p1s; Data.Ws2p2 = ws2.p2; Data.Ws2p2s = ws2.p2s; }
                if (top8.TryGetValue("wfp", out var wfp)) { Data.Wfp1 = wfp.p1; Data.Wfp1s = wfp.p1s; Data.Wfp2 = wfp.p2; Data.Wfp2s = wfp.p2s; }

                if (top8.TryGetValue("ltop81", out var lt1)) { Data.Ltop81p1 = lt1.p1; Data.Ltop81p1s = lt1.p1s; Data.Ltop81p2 = lt1.p2; Data.Ltop81p2s = lt1.p2s; }
                if (top8.TryGetValue("ltop82", out var lt2)) { Data.Ltop82p1 = lt2.p1; Data.Ltop82p1s = lt2.p1s; Data.Ltop82p2 = lt2.p2; Data.Ltop82p2s = lt2.p2s; }

                if (top8.TryGetValue("lq1", out var lq1)) { Data.Lq1p1 = lq1.p1; Data.Lq1p1s = lq1.p1s; Data.Lq1p2 = lq1.p2; Data.Lq1p2s = lq1.p2s; }
                if (top8.TryGetValue("lq2", out var lq2)) { Data.Lq2p1 = lq2.p1; Data.Lq2p1s = lq2.p1s; Data.Lq2p2 = lq2.p2; Data.Lq2p2s = lq2.p2s; }

                if (top8.TryGetValue("lsp", out var lsp)) { Data.Lsp1 = lsp.p1; Data.Lsp1s = lsp.p1s; Data.Lsp2 = lsp.p2; Data.Lsp2s = lsp.p2s; }
                if (top8.TryGetValue("lfp", out var lfp)) { Data.Lfp1 = lfp.p1; Data.Lfp1s = lfp.p1s; Data.Lfp2 = lfp.p2; Data.Lfp2s = lfp.p2s; }

                if (top8.TryGetValue("gfp", out var gfp)) { Data.Gfp1 = gfp.p1; Data.Gfp1s = gfp.p1s; Data.Gfp2 = gfp.p2; Data.Gfp2s = gfp.p2s; }
                if (top8.TryGetValue("gfrp", out var gfrp)) { Data.Gfrp1 = gfrp.p1; Data.Gfrp1s = gfrp.p1s; Data.Gfrp2 = gfrp.p2; Data.Gfrp2s = gfrp.p2s; }

                // StreamControlData no implementa INotifyPropertyChanged, así que hay que avisarle
                // a la vista que "Data" cambió para que los textbox del bracket se repinten.
                NotifyScoreboard();

                Save();
                StatusMessage = $"Top 8 sincronizado correctamente ({top8.Count} rondas asignadas).";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error sincronizando Top 8: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>Vacía las casillas del bracket Top 8 (sin notificar ni guardar).</summary>
        private void ClearBracketSlots()
        {
            Data.Ws1p1 = Data.Ws1p1s = Data.Ws1p2 = Data.Ws1p2s = "";
            Data.Ws2p1 = Data.Ws2p1s = Data.Ws2p2 = Data.Ws2p2s = "";
            Data.Wfp1 = Data.Wfp1s = Data.Wfp2 = Data.Wfp2s = "";
            Data.Ltop81p1 = Data.Ltop81p1s = Data.Ltop81p2 = Data.Ltop81p2s = "";
            Data.Ltop82p1 = Data.Ltop82p1s = Data.Ltop82p2 = Data.Ltop82p2s = "";
            Data.Lq1p1 = Data.Lq1p1s = Data.Lq1p2 = Data.Lq1p2s = "";
            Data.Lq2p1 = Data.Lq2p1s = Data.Lq2p2 = Data.Lq2p2s = "";
            Data.Lsp1 = Data.Lsp1s = Data.Lsp2 = Data.Lsp2s = "";
            Data.Lfp1 = Data.Lfp1s = Data.Lfp2 = Data.Lfp2s = "";
            Data.Gfp1 = Data.Gfp1s = Data.Gfp2 = Data.Gfp2s = "";
            Data.Gfrp1 = Data.Gfrp1s = Data.Gfrp2 = Data.Gfrp2s = "";
        }

        public void ClearBracket()
        {
            ClearBracketSlots();

            // "CLEAN ALL" también vacía el texto de abajo del bracket.
            Data.TOP8Text = "";

            // Sin esto los textbox siguen mostrando lo anterior (StreamControlData no notifica).
            NotifyScoreboard();

            Save();
            StatusMessage = "Bracket Top 8 limpiado.";
        }

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
