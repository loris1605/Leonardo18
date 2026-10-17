# NativeAOT in Leonardo18

Leonardo18 mantiene PublishAot=true. La vista Connection usa l'attivazione esplicita del ViewModel e i binding Avalonia compilati. BaseUserControl non cerca piu il ViewModel tramite reflection.

## Database

Le operazioni SQL, la ricerca delle istanze e le migrazioni EF vengono eseguite nel progetto locale DatabaseHost. L'interfaccia nativa comunica con il componente mediante JSON generato a compilazione, su input/output standard. Le credenziali non vengono inserite negli argomenti del processo. Un unico processo DatabaseHost viene condiviso fra tutte le viste per l'intera sessione. Le richieste JSON viaggiano una per riga e vengono serializzate dal client; il processo risponde senza terminare. L'annullamento di una richiesta attiva termina il processo e la richiesta successiva ne crea uno nuovo. L'annullamento in coda non interrompe le altre richieste. La chiusura dell'app termina i processi tramite DatabaseHostClient.Shutdown; la fine del flusso stdin fa terminare anche il ciclo del worker.

DatabaseHost e un componente .NET gestito. In pubblicazione viene distribuito come self-contained nella sottocartella DatabaseHost, cosi il PC di destinazione non deve installare separatamente il runtime .NET. Distribuire tutta la cartella di pubblicazione.

Le future operazioni che usano RepositoryBase e AppDbContext devono essere esposte attraverso questo componente, senza eseguire direttamente EF nel processo NativeAOT. Le operazioni esposte comprendono ricerca istanze, verifica connessione, migrazioni, Login, Menu, Soci, Configurazione, Cassa e Servizi.

## Impostazioni

L'API Settings.Default.ConnectionString / Settings.Default.Save() resta disponibile. Il nuovo Settings.cs legge e scrive XML esplicitamente, senza ApplicationSettingsBase. Il generatore precedente Settings.Designer.cs e escluso dalla compilazione; aggiungere eventuali nuove proprieta in Settings.cs.

Il percorso e %LOCALAPPDATA%\Leonardo18\user.config. La stringa viene cifrata con StringProtector prima del salvataggio. I vecchi file user.config presenti nelle sottocartelle delle impostazioni di Leonardo18 vengono letti se manca il nuovo file; i vecchi valori in chiaro o DPAPI vengono migrati dall'accesso Connection.

## Stili DataGrid

GridStyles conserva gli stili DataGrid tramite un collegamento al sorgente originale ControlStyles/DataGrid/DataGridStyles.axaml. Il progetto e nella soluzione ma non e referenziato dall'attuale interfaccia, che usa binding compilati per Connection, Login, Menu, Soci, Configurazione, Cassa e Servizi. La libreria DataGrid 12.1.2 usa percorsi di reflection e costruzione dinamica dei tipi.

Quando viene aggiunta una schermata con griglie, la sua compatibilita AOT deve essere verificata usando colonne e binding espliciti. Il tema opzionale e avares://GridStyles/Themes.axaml.

## Compilazione e pubblicazione Windows x64

    dotnet build Leonardo18.slnx
    dotnet publish Leonardo18/Leonardo18.csproj -c Release -r win-x64 --self-contained -o artifacts/native

La build prepara automaticamente DatabaseHost. La pubblicazione prepara e copia anche il componente self-contained.

Per il collegamento nativo Windows serve Visual Studio con il carico di lavoro Desktop development with C++ e i componenti predefiniti, compreso il Windows SDK. Non disabilitare PublishAot per aggirare un linker mancante.

Riferimenti: https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/ e https://learn.microsoft.com/en-us/ef/core/performance/nativeaot-and-precompiled-queries.
## Verifica del 13 ottobre 2026

Pubblicazione Release win-x64 completata con gli strumenti C++ installati. Leonardo18.exe e un PE nativo, senza directory CLR, e la prova di avvio ha creato una finestra responsiva. DatabaseHost e presente nella distribuzione con il runtime .NET incluso. Nessuna creazione o migrazione di database reali e stata eseguita durante la verifica.

## Login portato da Leonardo17

Il progetto Login usa .NET 10, ReactiveUI.Primitives e binding Avalonia compilati. Connection apre la vista Login dopo la verifica del database. DatabaseHost espone login-operators e login-authenticate; la verifica della password e il caricamento di postazioni, settori, tariffe e giornata vengono eseguiti nel processo gestito. Le risposte non includono password o hash. Dopo la verifica vengono aggiornate le password precedenti con PasswordHasher e viene costruito GlobalValuesC.MySetting nell'interfaccia. La notifica LoginSuccesso apre il modulo Menu.

## Menu portato da Leonardo17

Menu usa .NET 10, ReactiveUI.Primitives, BaseUserControl e binding compilati. Il layout mantiene PopupBorder, UiTitolo, UiButton, UiComboBox e WaitIndicator con messaggi distinti durante caricamento e apertura/chiusura giornata. Login apre Menu; cambia operatore e connessione azzerano i dati della sessione.

DatabaseHost espone menu-load, menu-open e menu-close. Rilegge i permessi e controlla che l'operatore sia abilitato. Le scritture sulla giornata richiedono una postazione amministratore o cassa e avvengono in una transazione serializzabile. Aperture ripetute non duplicano la giornata; più giornate aperte generano un errore. GlobalValuesC.MySetting.GIORNATA viene aggiornato con il risultato.

Configurazione, Soci, Cassa e Servizi sono migrati con binding compilati, factory esplicite e accesso ai dati tramite DatabaseHost. Servizi gestisce i tipi di abbonamento (vedi Servizi/MIGRATION.md). Report è migrato con binding compilati e accesso ai dati tramite DatabaseHost. DatabaseHost è un componente locale, non un servizio centrale di autenticazione: il controllo dell'OperatorId non costituisce un confine di sicurezza contro un client modificato.
Connection aperta dal Menu usa autoConnect=false: precompila i parametri salvati e ricerca le istanze, senza verificare o salvare automaticamente la connessione e senza aprire Login. La verifica esplicita conserva il normale flusso di salvataggio e accesso. All'avvio resta autoConnect=true.

## Soci portato da Leonardo17

Menu apre Soci per l'operatore con permesso Amministratore. Sono disponibili inserimento, modifica ed eliminazione dell'anagrafica, dei codici socio e delle tessere, ricerca per anagrafica/codice socio/tessera e ordinamento dell'elenco. I moduli usano le regole e la struttura di Leonardo17; le viste impiegano PopupBorder, UiTitolo, UiButton e UiComboBox di ControlStyles. La finestra compatta mostra il modulo al posto dell'elenco durante l'inserimento; da 1100 pixel li affianca.

Il desktop usa .NET 10, ReactiveUI.Primitives, attivazione esplicita e binding compilati. L'elenco utilizza un TreeView a colonne con TreeDataTemplate compilato e nodi tipizzati SociTreeNode. I tre livelli sono anagrafica, codice socio e tessera con numero e scadenza. Non utilizza DataGridCollectionView o raggruppamenti per nome di proprietà. Le anagrafiche prive di codici e i codici privi di tessere rimangono selezionabili.

DatabaseHost riceve l'operazione soci tramite il protocollo JSON generato. SociDatabaseRepository controlla operatore abilitato e permesso Amministratore, rilegge le relazioni e gestisce le scritture in una transazione serializzabile. Restano i controlli di maggiore età, dati anagrafici univoci e numeri socio/tessera univoci. Non si può eliminare un'anagrafica con codici né un codice con tessere; le conferme di eliminazione avvengono nei moduli. Gli identificativi riservati non compaiono nell'elenco e non sono modificabili dal modulo.

Le date restano interi YYYYMMDD. La scadenza viene ora salvata anche nelle aggiunte e nelle modifiche delle tessere. La ricerca applica insieme i filtri prima del limite originale di 100 righe; un messaggio segnala se esistono ulteriori risultati. Non sono state modificate le tabelle o le migrazioni.

I test del porting usano AppDbContext con SQLite in una cartella temporanea e servizi simulati per la vista. Nessuna scrittura su database utente è necessaria per questi test.
Verifica finale del porting Soci: compilazione della soluzione senza warning, controlli CRUD e interfaccia superati, pubblicazione NativeAOT win-x64 completata senza warning. L'eseguibile è nativo e la prova di avvio ha confermato una finestra responsiva; DatabaseHost è incluso nella distribuzione.

## Collegamento persistente al database

DatabaseHostClient condivide un WorkerSession per percorso eseguibile. Non conserva ViewModel o repository delle viste. Ogni operazione continua a creare e smaltire AppDbContext: vengono riutilizzati il processo, il modello EF e il pool SQL, non un contesto condiviso.

Una risposta applicativa fallita non chiude il worker. Se il processo termina o la risposta non è valida, il collegamento viene invalidato e la chiamata successiva lo riavvia. Le richieste già inviate non vengono ripetute automaticamente, perché una scrittura potrebbe essere stata completata prima dell'interruzione. stderr viene consumato senza accumularlo in memoria.

I test del collegamento verificano con un worker simulato riuso tra client diversi, richieste concorrenti, annullamento in coda e durante l'esecuzione, errori, riavvio e chiusura senza processi residui. La misurazione in sola lettura sul database salvato ha mostrato, a processo inizializzato: Connection 86 ms, Login 11 ms, Menu 20-51 ms, Soci 18-19 ms. Il primo accesso mantiene il costo di inizializzazione; operazioni diverse, rete e quantità di dati possono richiedere tempi diversi.
Verifica del collegamento persistente: soluzione e pubblicazione NativeAOT senza warning; test del trasporto e regressione Connection superati. La prova dell'eseguibile nativo ha confermato un solo DatabaseHost attivo e la sua terminazione dopo la chiusura normale della finestra.

Sottogruppi Soci: la gerarchia usa gli identificativi, evita duplicazioni e mantiene espansione e selezione durante l'ordinamento. La selezione di un nodo aggiorna GroupBindingT al livello corrispondente; i comandi di cancellazione verificano tutte le righe associate, non soltanto i campi del nodo selezionato. Espandere e comprimere non esegue letture SQL.

## MainWindow come contenitore router

MainWindow è associata a MainWindowViewModel, derivato da ViewModelBase e IScreen. RoutingState contiene il modulo corrente; ogni transizione usa NavigateAndReset e smaltisce le sottoscrizioni del modulo precedente. I moduli vengono creati tramite factory e ricevono MainWindowViewModel come HostScreen. Rientrare da Soci crea un nuovo Menu; Connection aperta dal Menu conserva autoConnect=false.

MainRouterHost associa esplicitamente ConnectionViewModel, LoginViewModel, MenuViewModel e SociViewModel alle rispettive viste. Svuota il contenuto precedente e costruisce una nuova vista anche nelle navigazioni verso lo stesso tipo. Non usa ricerca delle viste tramite reflection. Alla chiusura vengono liberati attivazione della finestra, stack, contenuto e DataContext.

Le notifiche dei moduli vengono elaborate sul dispatcher e ignorate se provengono da un modulo non più corrente. ViewModelBase smaltisce anche i propri comandi e lo stato di caricamento; Menu smaltisce tutti i comandi locali. DatabaseHost rimane condiviso fra le navigazioni e viene chiuso all'uscita dell'app.

Questa modifica riguarda il contenitore principale. I contenitori interni di Soci restano da riallineare separatamente al modello di moduli figli autonomi.
### Dependency injection
Program configura Microsoft.Extensions.DependencyInjection tramite factory esplicite, senza attivazione dei costruttori tramite reflection. App risolve MainWindow dal contenitore; MainWindowViewModel riceve le factory di Connection, Login, Menu, Soci, Configurazione, Cassa e Servizi. Ogni chiamata crea un nuovo ViewModel con il proprio IScreen e i parametri di navigazione. Client del worker, servizi e repository sono singleton privi di riferimenti alle pagine. Il contenitore viene smaltito alla fine del ciclo di vita desktop.

MainRouterHost usa TransitioningContentControl con CrossFade da 500 ms e DefaultContent vuoto. Le richieste durante una dissolvenza vengono accodate fino alla sua conclusione. La sostituzione di una pagina con una nuova istanza dello stesso modulo avviene immediatamente per rilasciare la precedente attivazione. La risoluzione delle viste resta esplicita.

MainWindowViewModel richiede obbligatoriamente factory di IConnectionViewModel, ILoginViewModel, IMenuViewModel e ISociViewModel. Non contiene creazioni dirette né factory predefinite dei moduli; Program definisce le implementazioni concrete. Il costruttore predefinito di MainWindow risolve anch'esso il ViewModel tramite DI, anche nel designer, senza avviare la navigazione in modalità design.

## Vincolo di pubblicazione Native AOT

Il progetto desktop Leonardo18 blocca PrepareForPublish se PublishAot non è true. IlcTreatWarningsAsErrors e ILLinkTreatWarningsAsErrors sono true: gli avvisi della compilazione nativa e del trimming devono essere risolti prima della distribuzione. Il vincolo appartiene al solo progetto desktop; DatabaseHost continua a essere gestito e self-contained.

Le normali compilazioni Debug/Release usate da Visual Studio per sviluppo non producono un eseguibile Native AOT. Per ottenere la versione nativa distribuibile usare dotnet publish come indicato sopra e distribuire l'intera cartella artifacts/native, inclusa DatabaseHost.

Cassa (sospesi, abbonamenti, rientri, Fidelity e relativo storico), Servizi e Report mantengono i binding compilati. Il protocollo JSON utilizza DatabaseProtocolJsonContext generato a compilazione. La stampa Report genera HTML con API tipizzate, quindi apre il file tramite Process.Start/UseShellExecute; l'utilizzo del browser esterno non modifica la modalità di compilazione dell'app desktop.
Verifica del 17 ottobre 2026: pubblicazione Release win-x64 completata della versione con sospesi, abbonamenti, rientri, Fidelity, storico Fidelity e stampa Report. Il compilatore ha generato il codice nativo senza avvisi AOT o trimming. Leonardo18.exe è un PE x64 senza directory CLR e la cartella non contiene Leonardo18.dll. Verificato il rifiuto di PublishAot=false e l'accettazione di PublishAot=true da parte di EnsureNativeAotPublication. Log: artifacts/native-aot-verifica.log. Non sono stati eseguiti accessi o migrazioni su database reali per questa verifica.