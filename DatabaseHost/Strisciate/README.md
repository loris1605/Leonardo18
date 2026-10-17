# Ricezione Strisciate

Il servizio riproduce l'endpoint del file `C:\Leonardo18\app.js` all'interno del componente database di Leonardo18. Si avvia quando viene caricata una postazione Cassa autorizzata con una giornata aperta; resta attivo passando da postazione a elenco o ingresso; si arresta tornando al Menu, chiudendo la vista o terminando l'applicazione.

Indirizzo: `http://127.0.0.1:5000/update`. Il programma che legge le tessere deve chiamare questo endpoint sullo stesso computer di Leonardo18. Il servizio usa il database configurato nell'applicazione e non le credenziali contenute nello script JS. Non occorre eseguire `app.js` né installare Node/ODBC. Se un altro programma occupa la porta 5000, l'avvio della Cassa segnala l'errore senza interrompere quel programma.

Esempio con dati fittizi:

```text
http://127.0.0.1:5000/update?codice_socio=000054&numero_tessera=000077&nome=ANNA&cognome=ROSSI&born_date=19950302&data_scadenza=20991231
```

| Parametro HTTP | Campo Strisciate |
| --- | --- |
| codice_socio | CodiceSocio (testo, massimo 20 caratteri) |
| numero_tessera | NumeroTessera (testo, massimo 20 caratteri) |
| cognome | Cognome (massimo 50 caratteri) |
| nome | Nome (massimo 50 caratteri) |
| born_date | Natoil (data valida yyyyMMdd, salvata come intero) |
| data_scadenza | Scadenza (data valida yyyyMMdd, salvata come intero) |
| — | DataOra = 0, CodMotivo = 0, come nello script fornito |

I sei parametri sono obbligatori. Zeri iniziali, spazi URL (`+`) e apostrofi vengono preservati. Come in `app.js`, gli altri parametri, ad esempio stato, tipo_tessera_id, circolo_id e data_rilascio, non sono mappati nella tabella.

La risposta è `200 OK` con corpo `OK` dopo lo smistamento riuscito; una tessera già presente restituisce OK dopo aver aggiornato scadenza e data di nascita della Person collegata. Dati mancanti, ambigui, troppo lunghi o date impossibili restituiscono 400; percorsi diversi 404; metodi diversi da GET 405; sessione non più autorizzata 403; conflitti anagrafici 409; problemi di salvataggio 500. Le risposte non includono credenziali o dati SQL riservati.

Ogni richiesta viene smistata subito nella stessa transazione. UniqueParam è composto dalle prime tre lettere di cognome e nome (con spazi di riempimento per i nomi corti) e dalla data di nascita yyyyMMdd, tutto in maiuscolo.

- Se NumeroTessera esiste già, vengono aggiornate la scadenza e, se diversa, la data di nascita della Person collegata con risposta OK: UniqueParam, nomi, Socio e abilitazione restano invariati e non viene lasciata una riga in coda. 
- Per una nuova tessera, Person viene cercata soltanto tramite UniqueParam. Se esiste, viene aggiornata la data di nascita; altrimenti viene creata con nome e cognome ricevuti.
- Il NumeroSocio della persona viene riutilizzato se presente; se diverso viene aggiunto, senza eliminare i precedenti.
- La nuova tessera viene associata al NumeroSocio ricevuto con la relativa scadenza.
- Se NumeroSocio appartiene a una persona con un altro UniqueParam, la richiesta restituisce 409 e la transazione viene annullata.

La riga Strisciate è consumata dopo lo smistamento riuscito. Le eventuali righe già in coda vengono elaborate aprendo Entra socio con le stesse regole; in caso di errore del batch restano in coda e le modifiche parziali vengono annullate. Gli inserimenti usano EF e parametri, con DbContext separati e cancellazione delle richieste pendenti alla chiusura.

Il server HTTP gira nel DatabaseHost gestito; il desktop rimane NativeAOT. Il protocollo tra desktop e componente database include un identificativo per ogni sessione: una chiusura tardiva della vecchia Cassa non spegne la nuova sessione. I log del server sono disattivati per mantenere pulito il protocollo JSON su stdout.

Verifiche automatiche: `dotnet run --project Cassa.Tests/Cassa.Tests.csproj`. Le prove HTTP usano porte temporanee e un database SQLite in memoria con dati fittizi, senza modificare il database configurato nell'app.
