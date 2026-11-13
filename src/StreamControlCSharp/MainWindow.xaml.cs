using System;
using System.Windows;
using StreamControlCSharp.ViewModels;

namespace StreamControlCSharp
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            App.Log("MainWindow ctor: before InitializeComponent");
            InitializeComponent();
            App.Log("MainWindow ctor: after InitializeComponent");

            // Precarga las fases y los sets del torneo para la tarjeta "Sets del torneo por fase".
            Loaded += async (s, e) =>
            {
                App.Log("MainWindow Loaded");
                if (DataContext is MainViewModel vm)
                {
                    await vm.LoadTournamentSetsAsync(silent: true);
                }
                App.Log("MainWindow Loaded: after LoadTournamentSetsAsync");
            };

            Closing += (s, e) =>
            {
                App.Log("MainWindow Closing");
                if (DataContext is MainViewModel vm)
                {
                    vm.SaveSettings();
                }
            };
        }
    }
}
