using Servizi.Contracts;
using Servizi.Core.Repository;
using Servizi.ViewModels;
using Cassa.Contracts;
using Cassa.Core.Repository;
using Cassa.ViewModels;
using Models.Entity.Global;
using Configurazione.Contracts;
using Configurazione.Core.Repository;
using Configurazione.ViewModels;
using Connection.Contracts;
using Login.Contracts;
using Menu.Contracts;
using Soci.Contracts;
using Avalonia;
using ReactiveUI.Avalonia;
using System;
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using AppServices.Database;
using Connection.Core.Services;
using Login.Core.Repository;
using Login.ViewModels;
using Menu.Core.Repository;
using Menu.ViewModels;
using Soci.Core.Repository;
using Soci.ViewModels;
using ReactiveUI;
using ViewModels;

namespace Leonardo18;

internal static class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    private static readonly Lazy<ServiceProvider> Container = new(CreateServices);
    internal static IServiceProvider Services => Container.Value;

    // Explicit factories keep construction compatible with NativeAOT.
    private static ServiceProvider CreateServices()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IDatabaseHostClient>(_ => new DatabaseHostClient());

        services.AddTransient<IConnectionService>(sp =>
            new ConnectionService(client: sp.GetRequiredService<IDatabaseHostClient>()));

        services.AddTransient<ILoginRepository>(sp =>
            new LoginRepository(sp.GetRequiredService<IDatabaseHostClient>()));

        services.AddTransient<IMenuRepository>(sp =>
            new MenuRepository(sp.GetRequiredService<IDatabaseHostClient>()));
        services.AddTransient<IMirrorClient>(sp =>
            new MirrorClient(sp.GetRequiredService<IDatabaseHostClient>()));

        services.AddTransient<ISociPersonRepository>(sp =>
            new SociPersonRepository(sp.GetRequiredService<IDatabaseHostClient>()));

        // The host and autoConnect are navigation parameters, not global services.
        // Each invocation creates a fresh ViewModel; the container does not retain it.
        services.AddTransient<Func<IScreen, bool, IConnectionViewModel>>(sp =>
            (host, autoConnect) => new ConnectionViewModel(host: host,
                service: sp.GetRequiredService<IConnectionService>(), autoConnect: autoConnect));

        services.AddTransient<Func<IScreen, ILoginViewModel>>(sp =>
            host => new LoginViewModel(sp.GetRequiredService<ILoginRepository>(), host));

        services.AddTransient<Func<IScreen, IMenuViewModel>>(sp =>
            host => new MenuViewModel(sp.GetRequiredService<IMenuRepository>(), host, sp.GetRequiredService<IMirrorClient>()));

        services.AddTransient<Func<IScreen, ISociViewModel>>(sp =>
            host => new SociViewModel(sp.GetRequiredService<ISociPersonRepository>(), host));

        services.AddTransient<IConfigurazioneRepository>(sp =>
            new ConfigurazioneRepository(sp.GetRequiredService<IDatabaseHostClient>()));
        services.AddTransient<Func<IScreen, ConfigurazioneSection, int, IConfigurazioneGroupViewModel>>(sp =>
            (host, section, selection) => new ConfigurazioneGroupViewModel(host,
                sp.GetRequiredService<IConfigurazioneRepository>(), section, selection));
        services.AddTransient<Func<IScreen, ConfigurazioneSection, ConfigurazioneAction, int, string, IConfigurazioneInputViewModel>>(sp =>
            (host, section, action, id, nome) => action == ConfigurazioneAction.LinksSave
                ? new ConfigurazioneLinksViewModel(host, sp.GetRequiredService<IConfigurazioneRepository>(), section, id, nome)
                : new ConfigurazioneInputViewModel(host, sp.GetRequiredService<IConfigurazioneRepository>(), section, action, id));
        services.AddTransient<Func<IScreen, IConfigurazioneViewModel>>(sp =>
            host => new ConfigurazioneViewModel(host,
                sp.GetRequiredService<Func<IScreen, ConfigurazioneSection, int, IConfigurazioneGroupViewModel>>(),
                sp.GetRequiredService<Func<IScreen, ConfigurazioneSection, ConfigurazioneAction, int, string, IConfigurazioneInputViewModel>>()));

        services.AddTransient<Func<IScreen, int, ICassaViewModel>>(sp =>
            (host, stationId) => new CassaViewModel(host,
                new CassaRepository(sp.GetRequiredService<IDatabaseHostClient>(),
                    GlobalValuesC.MySetting?.IDOPERATORE ?? throw new InvalidOperationException("Accedi nuovamente.")), stationId));

        services.AddTransient<Func<IScreen, IServiziViewModel>>(sp =>
            host => new ServiziViewModel(host, new ServiziRepository(sp.GetRequiredService<IDatabaseHostClient>(),
                GlobalValuesC.MySetting?.IDOPERATORE ?? throw new InvalidOperationException("Accedi nuovamente."))));
        services.AddTransient<Func<IScreen, global::Report.Contracts.IReportViewModel>>(sp=>host=>new global::Report.ViewModels.ReportViewModel(host,
            new global::Report.Core.Repository.ReportRepository(sp.GetRequiredService<IDatabaseHostClient>(),GlobalValuesC.MySetting?.IDOPERATORE??throw new InvalidOperationException("Accedi nuovamente."))));
        services.AddTransient<MainWindowViewModel>(sp => new MainWindowViewModel(
            sp.GetRequiredService<Func<IScreen, bool, IConnectionViewModel>>(),
            sp.GetRequiredService<Func<IScreen, ILoginViewModel>>(),
            sp.GetRequiredService<Func<IScreen, IMenuViewModel>>(),
            sp.GetRequiredService<Func<IScreen, ISociViewModel>>(),
            sp.GetRequiredService<Func<IScreen, IConfigurazioneViewModel>>(),
            sp.GetRequiredService<Func<IScreen, int, ICassaViewModel>>(),
            sp.GetRequiredService<Func<IScreen, IServiziViewModel>>(), sp.GetRequiredService<Func<IScreen, global::Report.Contracts.IReportViewModel>>()));
        services.AddTransient<MainWindow>(sp => new MainWindow(
            sp.GetRequiredService<MainWindowViewModel>()));
        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        });
    }

    [STAThread]
    public static void Main(string[] args)
    {
        // AppDomain è un'API .NET: il gestore viene installato prima dell'avvio di Avalonia.
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

        try
        {
            BuildAvaloniaApp()
                .StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            DatabaseHostClient.Shutdown();
            if (Container.IsValueCreated) Container.Value.Dispose();
        }
    }

    private static void OnUnhandledException(object? sender, UnhandledExceptionEventArgs e)
    {
        Debug.WriteLine("====================================================");
        Debug.WriteLine("Eccezione non gestita nell'applicazione.");
        Debug.WriteLine($"Terminazione del processo: {e.IsTerminating}");

        if (e.ExceptionObject is Exception exception)
        {
            Debug.WriteLine(exception.ToString());
        }
        else
        {
            Debug.WriteLine($"Oggetto eccezione: {e.ExceptionObject}");
        }

        Debug.WriteLine("====================================================");
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    // Mantenuto pubblico per gli strumenti di progettazione Avalonia.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace()
            .UseReactiveUI(_ => { });
}



