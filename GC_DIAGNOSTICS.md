# Monitoraggio GC in Debug

Le 147 classi istanziabili scritte nella soluzione hanno un finalizzatore diagnostico diretto oppure lo ereditano da una base monitorata. Il messaggio identifica sempre il tipo concreto, così le classi derivate producono una sola riga per oggetto. Le classi statiche non hanno istanze da finalizzare. I file generati non richiedono modifiche: per lo snapshot EF è presente una parte partial separata sotto DEBUG.

Il logger condiviso è Diagnostics/GcDebugLog.cs, collegato ai progetti mediante Directory.Build.props. Non conserva riferimenti agli oggetti e non legge dati applicativi. In Release il logger e i finalizzatori diagnostici non vengono compilati.

## Lettura dell'Output di Visual Studio

Avvia l'app in configurazione Debug, apri Visualizza > Output e seleziona Debug nel menu Mostra output da. Cerca [GC].

Esempio:

    ***** [GC] PID=1234 Soci.ViewModels.PersonSearchViewModel 5678 DISTRUTTO *****

PID distingue l'app dal DatabaseHost. Il nome è il tipo concreto; il numero identifica l'oggetto tramite RuntimeHelpers.GetHashCode. Il messaggio è emesso quando viene eseguito il finalizzatore: indica che l'oggetto è arrivato alla finalizzazione, non che la memoria sia già stata restituita al sistema operativo.

Il componente DatabaseHost invia i propri messaggi GC sul canale stderr; il client inoltra soltanto le righe con il prefisso GC nell'Output dell'app principale. Gli altri messaggi stderr continuano a essere scartati. Non vengono riportati credenziali o dati dei record.

## Interpretazione

- rimosso dallo stack: disattivazione e pulizia del ViewModel.
- [GC] ... DISTRUTTO: esecuzione del finalizzatore diagnostico.
- Nessun messaggio immediato: non dimostra un memory leak. Il GC non esegue necessariamente una raccolta al cambio di vista.
- Search resta referenziato da SociViewModel mentre il modulo Soci è vivo, anche quando il pannello di ricerca è chiuso.
- Settings.Default, il contesto JSON predefinito, le risorse dell'app e il worker condiviso hanno una durata intenzionalmente lunga.
- A fine processo i finalizzatori non sono garantiti; eventuale soppressione tramite Dispose può impedire il messaggio.

I finalizzatori modificano il comportamento della raccolta: gli oggetti attendono il thread di finalizzazione e possono sopravvivere a un ulteriore ciclo di GC. Questa configurazione serve alla diagnosi, non alle misurazioni di prestazioni o all'uso Release. Non sono state aggiunte chiamate a GC.Collect nei flussi dell'app.

Per dimostrare un leak servono anche riferimenti deboli, cicli ripetuti e/o snapshot con un profiler. Nei test temporanei sono stati verificati copertura delle 127 classi, messaggi per PersonSearchViewModel, tipi derivati e DTO, assenza di finalizzatori diagnostici nelle DLL Release e inoltro dei messaggi dal worker.
MainWindow usa ora MainWindowViewModel e un router con NavigateAndReset. Nei test temporanei sono stati eseguiti dieci cicli Menu/Soci e navigazioni Connection/Login/Menu: tutte le 48 vecchie viste e ViewModel osservate tramite WeakReference sono state raccolte. Le raccolte forzate sono esclusivamente nel test, non nella navigazione dell'app. I controlli sono stati isolati in metodi senza inlining per evitare riferimenti temporanei trattenuti dal JIT Debug.

Configurazione: test con WeakReference su 38 vecchie viste e ViewModel, raccolti dopo cinque cicli di salvataggio, associazioni e annullamento. Verificati anche Menu -> Configurazione -> Menu e lo svuotamento dei due router interni alla chiusura.
