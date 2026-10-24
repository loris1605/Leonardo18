# Mirror locale -> Azure SQL

Il frontend Leonardo18 resta Native AOT. Copia, SqlClient, Azure.Identity e cache credenziali sono confinati al DatabaseHost gestito.

Destinazione fissata in MirrorSnapshotEngine: leonardo18-mirror-20261019.database.windows.net / UlisseMirror. Origine: connessione locale configurata, catalogo Ulisse, vincolata al SourceKey registrato nel mirror. Non modificare la connessione principale dell'app per usare la copia cloud.

## Funzionamento
Il collegamento iniziale richiede un amministratore di Leonardo18 e l'accesso Microsoft Entra. Registrazione desktop Leonardo18 Mirror nel tenant del cliente, con solo permesso SQL delegato, senza segreti client. L'AuthenticationRecord è cifrato tramite DPAPI CurrentUser; la cache MSAL è protetta da Azure.Identity/Windows e non ammette archiviazione in chiaro. Tutto è per utente Windows: un altro PC/utente deve effettuare il proprio collegamento.

Stato per utente in %LOCALAPPDATA%/Leonardo18/Mirror. Il DatabaseHost rimane attivo durante la navigazione e verifica i dati locali ogni minuto mentre l'app è aperta. Copia soltanto se cambiati, raggruppando gli invii con un intervallo minimo di cinque minuti. Il pulsante SINCRONIZZA MIRROR forza una copia verificata subito. Non viene installato un servizio di Windows: a programma chiuso non esegue copie; al prossimo avvio rileva le differenze rimaste.

Acquisizione coerente locale serializzabile, timeout attesa lock 250 ms e comando lettura 3 secondi; i lock sono rilasciati prima dell'accesso cloud. Nessun INSERT/UPDATE/DELETE/migrazione locale. Lo snapshot viene mantenuto in memoria. Migrazioni solo sul mirror già inizializzato e associato all'origine. Copia atomica remota, KeepIdentity e KeepNulls, vincoli riverificati, confronto SHA-256 di tutte le righe indipendente dall'ordine. Nessun duplicato di incasso: si copiano i record già registrati, non si rieseguono operazioni di cassa.

Una sola copia per cartella/utente mediante lock di file e un solo trasferimento remoto mediante applock SQL. Lo stato cloud impedisce a uno snapshot precedente di sostituirne uno successivo. L'orario dello snapshot proviene dal SQL Server di origine. La precedente versione cloud non viene cancellata definitivamente se il trasferimento fallisce: la transazione annulla la sostituzione. Ripartenza tramite nuovo snapshot completo, senza dipendere da notifiche volatili. Nessuna scrittura remota verso il locale.

Errori di rete, quota, schema o autenticazione non vengono propagati alle operazioni di cassa. Attesa progressiva 1, 2, 5, 15 minuti; rinnovo interattivo solo premendo COLLEGA MIRROR. Nessuna finestra automatica in background. Menu mostra ultimo invio confermato, stato e codice Microsoft quando serve. ErrorCode è diagnostico senza password/token. File dello stato aggiornato con sostituzione atomica solo dopo commit della copia.

## Quota e limiti
Azure SQL gratuito può sospendersi fino al mese seguente quando esaurisce la quota. Fatturazione in eccedenza disabilitata nel portale. L'aggiornamento frequente può consumare la quota anche con pochi dati; nessuna connessione remota è mantenuta nel pool fra gli invii. Richiede l'IP pubblico autorizzato nel firewall. Stato e date descrivono uno snapshot: una modifica successiva resta da sincronizzare fino al prossimo ciclo. Replica applicativa completa, non SQL Data Sync, non HA, non backup con versioni. Uso operativo remoto e sincronizzazione bidirezionale non implementati.

## Comandi di manutenzione
Leonardo18.DatabaseHost.exe --mirror-connect: accesso Microsoft e prima copia con la stessa configurazione dell'app.
Leonardo18.DatabaseHost.exe --mirror-sync: copia forzata mediante cache protetta, senza login se ancora valido.
Verifiche isolate: Cassa.Tests --mirror. Non utilizzano credenziali o database del cliente.
