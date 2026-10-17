# Configurazione da Leonardo17

Sono trasferite le funzioni di operatori (nome, password, badge, abilitazione e permessi), postazioni (tipo, rientro e reparti), settori (etichetta, tipo e listini), tariffe (etichetta, prezzo e free drink) e tipi di rientro (durata).

Le parti ripetute degli elenchi e dei form originali sono riunite in mini ViewModel e viste condivise, con sezione e azione esplicite. Il contenitore conserva GroupRouter e InputRouter: ogni apertura crea una nuova istanza, l'uscita dal form svuota InputRouter e dopo un salvataggio ricrea l'elenco selezionando l'elemento salvato. Il layout affianca i pannelli nelle finestre ampie e mostra il form nelle finestre compatte.

Program registra repository e factory tramite interfacce; MainWindowViewModel riceve IConfigurazioneViewModel. Menu apre il modulo per gli amministratori. I router host risolvono le viste esplicitamente per NativeAOT.

Le operazioni EF vengono eseguite nel DatabaseHost condiviso, con un nuovo AppDbContext e una transazione serializzabile per richiesta. Le autorizzazioni sono verificate nel worker. Le associazioni sono validate e salvate atomicamente. Le cancellazioni di elementi collegati sono rifiutate; restano protetti gli elementi di sistema e deve rimanere almeno un amministratore abilitato.

Le password non vengono mostrate né restituite al desktop. Una password vuota durante la modifica conserva quella esistente; una nuova password viene salvata come hash PBKDF2. Il protocollo JSON usa la generazione dei metadati in compilazione.

Le viste usano PopupBorder, UiButton, UiTextBox e derivati, UiComboBox e UiWaitIndicator delle librerie di Leonardo18. Gli indicatori mostrano il messaggio dell'operazione corrente. I ViewModel ereditano ViewModelBase e la diagnostica GC DEBUG.

Verifiche: CRUD SQLite per tutte e cinque le sezioni; permessi/reparti/listini; duplicati; collegamenti invalidi; cancellazioni protette; password e protocollo; layout ampio/compatto; focus Nome; svuotamento dei router; 38 vecchie viste e ViewModel raccolti nel test GC; navigazione Menu-Configurazione-Menu.
