# Cassa — Leonardo18

Porting delle operazioni implementate in `Leonardo17/Cassa`:

| Leonardo17 | Leonardo18 |
| --- | --- |
| CassaViewModel e navigazione con Splat | CassaViewModel costruito con factory esplicita, collegato a MainRouterHost |
| CassaPostazioneViewModel e CassaPostazioneRepository | Apertura scheda e postazione in CassaViewModel / CassaDatabaseRepository |
| SchedaContoViewModel e CassaSchedaContoRepository | Dettaglio conto, totale e importo da pagare, con pulsanti di addebito dei listini Standard della postazione |
| CassaListaSociViewModel e CassaListaSociRepository | Elenco soci presenti con filtro, apertura tramite pulsante o doppio clic |
| EntraSocioViewModel / EntraSocioAnagraficaViewModel | Identificazione tessera, posizione, tariffa e registrazione ingresso |
| StrisciataRepository | Importazione delle strisciate nel componente database separato |

Il modulo usa .NET 10, Avalonia 12.1.4, ReactiveUI.Primitives, binding compilati e UiIconButton con icone Fluent colorate. Le query EF sono nel DatabaseHost gestito; il desktop comunica attraverso contratti JSON generati e resta compatibile con NativeAOT.

L'ingresso viene registrato in una transazione con verifica dell'operatore, dei permessi di cassa, della giornata aperta, della tessera e della tariffa assegnata alla postazione. La persona e gli importi provengono dal database, non dai dati visualizzati nel client. Posizione e persona restano univoche come nel modello esistente; nessuna nuova migrazione database è necessaria.

Il cambio della tessera invalida l'identificazione precedente. Risposte asincrone e notifiche dei binding dopo la chiusura della vista non aggiornano segnali eliminati. ESC torna alla postazione dai pannelli; nella postazione azzera prima la scheda, poi torna al menu. INVIO cerca la scheda o la tessera, oppure registra l'ingresso quando i dati sono validi.

## Funzioni non implementate nel sorgente originale

`F5Async` (socio virtuale) era vuoto in Leonardo17 e non è esposto come operazione funzionante. Esce Socio è ora implementato nella sezione descritta sotto. La precedente bozza di acquisto abbonamenti è sostituita dalla sezione descritta sotto. La casella della colonna Stato salva Pagato/Da pagare della singola riga di SchedaConto nel database e aggiorna il residuo da pagare. Il totale del conto resta invariato. Un errore ripristina la casella allo stato salvato; ordine delle righe, settore selezionato e modifiche guardaroba ancora da salvare vengono conservati.

## Verifiche

Da `C:\Leonardo18`:

```powershell
dotnet build Leonardo18.slnx
dotnet run --project Cassa.Tests/Cassa.Tests.csproj
dotnet publish Leonardo18/Leonardo18.csproj -c Release -r win-x64 --self-contained true -o artifacts/cassa-validation
```

Il runner usa un database SQLite in memoria e la UI Avalonia senza finestre visibili. Verifica il protocollo JSON, le query/registrazioni, gli errori di validazione, l'importazione di strisciate ripetute, la navigazione dal Menu e i completamenti tardivi dopo la chiusura. Le anteprime in `Cassa.Tests/previews` contengono soltanto dati fittizi. SQLite non sostituisce la verifica finale sul database SQL Server dell'applicazione.

## Smistamento immediato delle strisciate

Il servizio HTTP ora elabora subito Person/Socio/Tessera in una transazione. Person viene riconosciuta soltanto tramite UniqueParam; se NumeroTessera esiste già vengono aggiornate la scadenza e, se diversa, la nascita della Person collegata, conservando UniqueParam, nomi, Socio e abilitazione. Per tessere nuove viene aggiornata la nascita della persona trovata, aggiunto NumeroSocio se manca e creata la tessera con la scadenza ricevuta. Le righe consumate vengono rimosse da Strisciate. Anche la coda precedente usa lo stesso elaboratore.

CassaPostazione: la scheda aperta mostra anagrafica, conto e pulsanti delle voci Standard assegnate alla postazione. Ogni clic aggiunge una riga non pagata in SchedaConto e ricarica righe e totali. Il backend verifica permessi, giornata aperta, identità della scheda, blocco/chiusura e listino della postazione, usando descrizioni e prezzo correnti dal database. La PopupBorder si estende alla finestra: anagrafica, totali e voci restano nella vista fissa, con scorrimento limitato al conto. I settori sono selezionabili tramite TabControl e i pulsanti sono disposti su quattro colonne. Il settore selezionato viene mantenuto dopo gli addebiti.

Guardaroba: il controllo a sinistra del conto modifica i campi esistenti Grb1, Grb2, Grb3 e Grb4 della Scheda. Il pulsante Salva persiste soltanto questi valori senza generare addebiti; vuoto o zero libera il campo. Numeri negativi, non interi o fuori intervallo sono rifiutati. Permessi, giornata, identità della scheda e stato bloccato/chiuso sono verificati nel backend. Gli addebiti non eliminano le modifiche guardaroba ancora da salvare. SchedaConto e TabControl delle voci di spesa hanno la stessa larghezza, con quattro pulsanti per riga e descrizione e prezzo disposti su più righe.

Pagamento per riga: il backend imposta lo stato richiesto in modo idempotente e verifica appartenenza della riga alla scheda aperta, permessi della postazione, giornata aperta e scheda non bloccata/chiusa. Non sono necessarie modifiche al modello database.

Il cambio Pagato/Da pagare usa un salvataggio locale alla griglia: aggiorna la riga esistente tramite notifiche e ricalcola il residuo, senza attivare il caricamento generale o ricreare anagrafica, tab e guardaroba. Durante la richiesta resta disabilitata soltanto la griglia del conto. Errori ripristinano la casella esistente; ordinamenti usano spostamenti delle righe per conservare i controlli e lo scorrimento. La risposta del pagamento non ricarica i listini.

Storni: il tasto destro sulla riga apre il menu contestuale con Storna voce. Il comando e disabilitato per importi zero o negativi e la colonna del pulsante Storna e stata rimossa. Il backend aggiunge una nuova SchedaConto con descrizione prefissata da STORNO - , prezzo opposto e stesso stato Pagato, settore, postazione e note della riga originale. La riga originale resta invariata. Per importi zero o negativi il client non invia richieste e il backend non scrive righe. Lo storno aggiorna soltanto la griglia e i totali, conserva ordinamento e pannelli e riporta il focus a Posizione. Sono verificati permessi, giornata, scheda e appartenenza della riga.

La migrazione iniziale Start crea VoiceDesc direttamente con lunghezza 50 per conservare anche le descrizioni con il prefisso STORNO - . Il database nuovo viene creato tramite F5 nella schermata Connessione.

Le nuove righe di SchedaConto usano la Label della tariffa come VoiceDesc, sia per gli addebiti sia per gli ingressi. Lo storno aggiunge il prefisso alla descrizione salvata. Le descrizioni delle righe preesistenti non vengono riscritte.

Dettaglio delle righe preesistenti: al caricamento del conto i vecchi nomi delle tariffe vengono presentati con la Label corrispondente, mantenendo il prefisso STORNO - . La conversione avviene solo nel risultato per la vista, senza riscrivere lo storico. Le etichette gia salvate sono conservate; nomi con piu etichette diverse non vengono associati arbitrariamente. Il pagamento locale non ricarica questa mappa.

Distribuzione del servizio: dopo modifiche al DatabaseHost ricostruire anche Leonardo18/Leonardo18.csproj. Il target BuildDatabaseHost aggiorna la copia sotto bin/Debug/net10.0/DatabaseHost, che e il percorso effettivamente avviato dal desktop. La sola esecuzione di Cassa.Tests aggiorna il servizio di test ma non questa copia. La regressione INGRESSO OMAGGIO -> OMAGGIO e coperta dalle prove; il servizio distribuito e stato verificato in sola lettura sulla posizione 27.

Salva guardaroba usa un salvataggio locale a GuardarobaControl: solo il controllo viene disabilitato durante la richiesta; messaggi e validazione sono locali. La risposta contiene la scheda aggiornata senza ricaricare conto o listini. Nel client vengono aggiornati soltanto Grb1-Grb4 e i campi di inserimento: anagrafica, righe, ordinamento e tab restano gli stessi. Dopo il successo il focus torna a Posizione. Il pulsante Salva usa la variante Filled della icona Save, disponibile nel set Fluent.

## Acquisto abbonamenti e Sospesi

Gli acquisti sono riattivati. Pagato registra abbonamento e incasso contabile; Non pagato registra abbonamento e credito nella nuova tabella Sospesi, senza anticipare ricavo o incasso. Il saldo dal pannello Sospesi registra entrambi e conserva lo storico. Per due soci esiste un solo sospeso, ricercabile tramite entrambi. Il richiamo compare sulla scheda e all'identificazione della tessera.

Start resta invariata. Applicare F5 in Connessione per la nuova migrazione AddSospesiAbbonamenti: non occorre cancellare il database. La migrazione aggiunge Sospesi e i dati operativi di acquisto ad Abbonamenti senza rimuovere registrazioni esistenti. Le verifiche usano solo database isolati.

La tabella gestisce anche i crediti lasciati all'uscita, tramite il comando Esce Socio.

## Ingressi e contabilità
La registrazione di un nuovo ingresso pagato salva anche il movimento INGRESSO_PAGATO (100 Cassa Dare / 400 Ricavi Avere) nella stessa transazione. Gli omaggi a zero non generano importi contabili. Lo storno degli ingressi ordinari pagati genera STORNO_INGRESSO con Dare/Avere invertiti rispetto alla scrittura originale, collegata tramite StornoDiId, nella giornata aperta. La riga negativa e la rettifica sono atomiche; un secondo storno viene rifiutato. Lo storno annulla anche ingresso, timbro Fidelity e uso abbonamento, come descritto nella sezione NON ENTRA. Il cambio del flag Pagato resta bloccato sugli ingressi contabilizzati e sulle loro righe di storno. Gli sconti Fidelity di ingresso sono restituiti nella stessa transazione. I saldi di sospesi mantengono le proprie protezioni. Per i nuovi identificativi serve la migrazione AnnullaIngresso. Vedere Models/Contabilita/README.md.

## Voci di spesa e contabilità
Le nuove voci di spesa generano ricavo e credito verso il socio, con una partita aperta. Il flag Pagato registra soltanto l'incasso e chiude la partita; il ritorno a Non pagato storna l'incasso. Lo storno annulla il ricavo e il credito oppure restituisce cassa se la voce era pagata. Ogni operazione è atomica con SchedaConto e mantiene lo storico. Non sono modificati il layout, il focus o il caricamento locale della griglia. Nessuna nuova migrazione; riavviare l'app dopo la compilazione. Dettagli in Models/Contabilita/README.md.

## Esce Socio
La sezione cerca la presenza tramite posizione o tessera, propone la scheda aperta, il guardaroba e il conto. Incassa e registra uscita regola tutte le voci residue; Uscita con sospeso registra il credito residuo e consente il saldo successivo dalla sezione Sospesi. Non è previsto in questo passaggio l'incasso parziale; un residuo negativo richiede di regolarizzare il rimborso nella Scheda.
La conferma controlla una versione del conto letta dal server. Se cambiano prezzi, voci o pagamenti, occorre aggiornare il riepilogo. IdOperazione rende le riprove idempotenti; i dati confermati devono coincidere. Un errore annulla insieme incassi, sospesi, storico e rimozione della presenza attiva.
SchedeUscite e SchedeUsciteConto conservano tutti i dati della scheda e del conto, con gli ID originali e il contesto di uscita. Solo dopo la creazione dello storico vengono rimosse le righe attive nella stessa transazione. Questo libera la persona e la posizione senza modificare i vincoli esistenti. I Report comprendono sia i dati attivi sia quelli archiviati.
Applicare la nuova migrazione AddSchedeUscite con F5 in Connessione; non eliminare il database e non modificare le migrazioni già applicate.
## Annullamento ingresso: NON ENTRA

Lo storno di una riga di ingresso, anche a importo zero, aggiunge STORNO - NON ENTRA e annulla la presenza ai fini di elenco presenti, Report e rientri. Gli storni delle altre spese mantengono STORNO senza NON ENTRA. La scheda rimane consultabile alla sua posizione; chiuderla con Esce socio per liberare la posizione, dopo aver regolarizzato eventuali altre spese.

Lo stesso annullamento restituisce il bollino abbonamento utilizzato, compensa il timbro Fidelity e restituisce un eventuale ingresso premio Fidelity. I premi denaro maturati senza più il relativo traguardo sono revocati se ancora disponibili; se sono già stati spesi l'operazione viene bloccata senza scritture parziali. La restituzione dello sconto ingresso avviene insieme all'annullamento. Lo storico Fidelity conserva i movimenti originali e quelli di annullamento; la tessera mostra solo i timbri validi.

La migrazione 20261017150000_AnnullaIngresso aggiunge identificazione delle righe ingresso, stato NON ENTRA e collegamento del bollino alla scheda. Riconosce le righe ingresso già presenti tramite settore e data/ora di check-in e recupera i collegamenti degli abbonamenti attivi dalle note generate da Leonardo18. Eseguire l'aggiornamento database dalla schermata Connessione (F5). Nessun aggiornamento del database utente viene eseguito dalle verifiche automatiche.Gli storni ingresso monetari della versione precedente possono essere completati scegliendo nuovamente lo storno sulla riga originale dopo l'aggiornamento: viene aggiornata la riga negativa esistente e annullato il beneficio collegato, senza creare un secondo rimborso contabile. L'annullamento già completo viene invece rifiutato.
## Blocco e sblocco globale della scheda

Nell'identità della scheda di Cassa è presente BLOCCA SCHEDA / SBLOCCA SCHEDA. Lo stato usa Scheda.Blocco, già esistente e indipendente dall'abilitazione della tessera. Non è necessaria una migrazione.

SetBloccoScheda specifica posizione, identificativo della scheda e stato desiderato: le ripetizioni sono idempotenti, gli identificativi superati e le schede chiuse vengono rifiutati. Servono gli stessi permessi di accesso alla Cassa. Qualunque postazione Cassa autorizzata può sbloccare una scheda.

Il servizio rilegge il blocco nella transazione serializzabile di ogni modifica: addebiti, pagamenti, storni, guardaroba e uscita sono impediti da tutte le postazioni, anche con una schermata aperta prima del blocco. Le letture rimangono consentite. Per aggiornare lo stato visualizzato su un'altra postazione, riaprire la scheda. Il blocco non modifica i dati delle altre schede, della tessera, del conto o della contabilità.
Esce Socio è disponibile soltanto con una scheda effettivamente aperta nella vista Cassa, non bloccata e non già chiusa. La sola digitazione della posizione non abilita il pulsante. Togliere la scheda dalla vista o confermare l'uscita lo disabilita; aprire una scheda valida lo riabilita. Il comando utilizza la posizione della scheda aperta. Non servono migrazioni aggiuntive.
## Blocchi disciplinari dei soci
In Soci, selezionare un'anagrafica, un codice socio o una tessera e usare GESTISCI BLOCCO SOCIO. Il blocco appartiene alla persona e vale per tutti i suoi codici e tessere. SOCIO BLOCCATO abilita il divieto; A TEMPO INDETERMINATO evita la scadenza, altrimenti impostare la data finale, compresa nel periodo di blocco. La motivazione è obbligatoria per un blocco attivo e ammette fino a 1000 caratteri. Per sbloccare togliere SOCIO BLOCCATO e salvare. Dopo la scadenza il divieto cessa automaticamente; motivazione e impostazione precedente restano consultabili nell'editor.
Cassa mostra il motivo e rifiuta sul database ingressi di ogni tipo (anche rientri, abbonamento e Fidelity), nuovi acquisti di abbonamenti (anche se il bloccato è il secondo intestatario) e attivazioni Fidelity. I controlli vengono ripetuti alla conferma, anche con identificazioni già aperte in altre postazioni. Modifiche anagrafiche e importazioni Strisciate non cancellano il blocco. Il blocco della scheda in Cassa e l'abilitazione della tessera restano funzioni distinte.
Migrazione aggiuntiva: 20261017193000_BlocchiSoci, tre colonne su People. Applicare gli aggiornamenti del database dalla Connessione (F5) prima di usare questa versione. Nessuna modifica ai blocchi esistenti delle schede e nessun blocco automatico dei soci già registrati.

## Anagrafica in maiuscolo
Nome e cognome della Person vengono normalizzati in maiuscolo durante l'inserimento, nella validazione e nell'entità condivisa, comprese le importazioni Strisciate. La migrazione 20261017210000_PersonMaiuscolo converte anche i record preesistenti; non modifica codici socio, tessere, blocchi o registrazioni contabili. Su Ulisse l'aggiornamento delle anagrafiche è stato eseguito direttamente e verificato; la migrazione resta idempotente.

## Pagamento totale del conto
In CassaPostazione, sotto la griglia del conto, PAGA TUTTE LE VOCI salda le righe non pagate con un'unica transazione. Le voci già pagate restano invariate. Gli incassi usano le scritture e gli abbinamenti del pagamento singolo; per le voci pregresse prive di contabilità viene creata prima la relativa partita. Gli storni già riconciliati vengono chiusi senza ulteriori movimenti di cassa; gli storni pregressi vengono abbinati alle voci originarie quando descrizione e importi coincidono. Conti a rimborso o storni non riconciliabili richiedono regolarizzazione prima del pagamento totale.
Il servizio controlla scheda, blocco, righe visualizzate e importo ancora dovuto. Le richieste ripetute non duplicano incassi; un errore annulla sia i flag sia tutte le scritture della transazione. La scheda resta aperta e il focus ritorna alla posizione. Non richiede nuove migrazioni.

## Cassa iniziale all'apertura della giornata

Dal menu, APRI GIORNATA apre il conteggio dei tagli da 500 euro a 10 centesimi (esclusi 1, 2 e 5 centesimi). Le quantità devono essere intere e non negative. ANNULLA non scrive dati; CONFERMA E APRI GIORNATA salva conteggio e totale insieme alla giornata. Un importo zero è consentito e non produce righe contabili a zero.

Per un fondo positivo viene registrata una scrittura confermata: Dare conto 100 Cassa, Avere conto patrimoniale 310 Cassa Iniziale. Il conto 310 viene creato al primo utilizzo; un conto esistente incompatibile impedisce l'apertura. Giornata e scrittura sono nella stessa transazione. Un identificativo dell'apertura evita duplicazioni in caso di ripetizione della conferma e impedisce di applicare il conteggio a una giornata già aperta da un'altra postazione.

La sezione CASSA INIZIALE dei Report e la stampa riportano il conteggio salvato, il fondo e la disponibilità comprensiva del fondo. La cassa iniziale non aumenta incassi, ricavi o totale operativo. Le giornate preesistenti mantengono fondo zero e nessun dettaglio.

Migrazione aggiuntiva: 20261017230000_CassaInizialeGiornata. Aggiornare il database dalla schermata Connessione (F5) prima di usare la nuova versione su tutte le postazioni.

## Tessera Fidelity in EntraSocio

Con una Fidelity attiva, la scheda mostra timbri registrati con data/ora, spazi ancora da usare e il timbro verde dell'ingresso corrente. Il timbro verde conferma l'ingresso scelto (tariffa, abbonamento o premio Fidelity) tramite la normale registrazione: occorre completare la posizione e la scelta dell'ingresso. REGISTRA INGRESSO registra solo l'ingresso, senza timbrare. Il timbro viene richiesto esclusivamente tramite il comando del timbro verde. I timbri usati e gli spazi futuri non sono selezionabili; rientri, soci già presenti, blocchi e sospesi non permettono un nuovo timbro.

La scrittura del timbro resta nella transazione dell'ingresso e non viene anticipata dalla vista. Lo storico conserva le date precedenti, anche dopo premi o rinnovi; la griglia scorre sul timbro corrente quando necessario. Nessuna modifica allo schema del database.


La richiesta CassaRequest.TimbrareFidelity è falsa per impostazione predefinita. Il server aggiunge il timbro solo quando è esplicitamente vera; i rientri restano esclusi. I due comandi UI non condividono uno stato di selezione: un errore o un annullamento non abilita il timbro su un successivo ingresso normale.

## Limite annullamento ingresso
L'operatore può annullare l'ingresso dal menu della riga in CassaPostazione soltanto prima di dieci minuti dal CheckinTime della scheda. Il database verifica nuovamente il limite nella transazione dello storno, su qualsiasi postazione; da dieci minuti esatti richiede un operatore Amministratore. Non esiste ancora una procedura di autorizzazione o un bypass, nemmeno per l'amministratore corrente. Gli storni delle altre spese non hanno questo limite. Restano atomici rettifica contabile, NON ENTRA, annullamento timbri Fidelity e restituzione ingresso abbonamento; gli importi zero mantengono lo storico operativo senza creare movimenti monetari fittizi. Nessuna migrazione richiesta.
