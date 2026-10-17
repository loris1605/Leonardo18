# Modello contabile

Le cinque tabelle con prefisso `Contabilita` costituiscono un archivio separato da SchedaConto, Abbonamenti e dalle altre tabelle operative. Le tabelle rimangono separate. Sono collegati automaticamente gli ingressi pagati, le nuove voci di spesa e gli incassi degli abbonamenti. La tabella Sospesi conserva separatamente gli abbonamenti da pagare.

| Tabella | Contenuto |
| --- | --- |
| ContabilitaPianoConti | Conti gerarchici, codice univoco, natura, abilitazione e possibilità di movimentazione. |
| ContabilitaMovimenti | Testata, date, stato bozza/confermato, valuta, causale, descrizione, origine, riferimenti storici a socio/operatore/postazione/giornata, eventuale movimento stornato. |
| ContabilitaRighe | Conto, Dare o Avere, dettagli storici di socio, settore, tariffa, tessera e abbonamento. |
| ContabilitaPartite | Crediti o debiti, riga di apertura, importo originale, controparte anche senza socio, scadenza. |
| ContabilitaAbbinamentiPartite | Pagamenti e altri regolamenti delle partite, anche parziali, e loro eventuali storni. |

Il piano iniziale contiene Cassa, Banca, Crediti verso soci, Debiti diversi, Patrimonio netto, Ricavi diversi e Costi diversi. I conti possono essere articolati successivamente. La valuta iniziale è EUR; importi in decimal(18,2).

## Separazione e collegamenti

PersonId, OperatoreId, PostazioneId, GiornataId, SettoreId, TariffaId e AbbonamentoId sono riferimenti storici facoltativi, volutamente senza foreign key verso l'operativo. Le descrizioni sono copie del valore al momento della registrazione. La contabilità resta quindi leggibile anche se l'operativo viene modificato o ripulito. I riferimenti in testata sono il contesto generale; quelli sulle righe identificano i dettagli e le eventuali controparti specifiche.

OrigineModulo/OrigineEntita/OrigineId identificano il record operativo, OrigineEvento distingue addebito, incasso, storno ecc. IdOperazione è una chiave univoca da assegnare prima del primo tentativo e riutilizzare nei tentativi successivi: eventi diversi dello stesso record devono avere chiavi diverse. Anche gli abbinamenti hanno una propria chiave univoca.

Le registrazioni parallele usano la stessa transazione: due archivi separati, ma conferma atomica. Questo collegamento è attivo per i nuovi ingressi pagati e per le voci di spesa con i relativi regolamenti. Non vengono usati trigger né copie asincrone prive di controllo.

## Partite e storico

Il residuo si calcola come ImportoOriginale meno la somma degli abbinamenti; non si memorizza un secondo saldo o un flag Pagato che potrebbe diventare incoerente. La riga di regolamento identifica il movimento del pagamento. Uno stesso pagamento può avere più abbinamenti verso partite diverse. Credito significa somma da riscuotere, debito somma da pagare.

Gli storni dei movimenti usano nuove righe con Dare e Avere invertiti. Gli storni degli abbinamenti usano un importo negativo e StornoDiId; il vincolo univoco impedisce due storni dello stesso abbinamento. Le foreign key interne impediscono cancellazioni in cascata.

## Controlli e fase successiva

Il database verifica chiavi univoche, relazioni contabili, importi positivi su un solo lato della riga, numerazione righe e validità dei tipi. VerificaQuadratura controlla almeno due righe, precisione ai centesimi e uguaglianza Dare/Avere sul movimento completo.

La contabilizzazione degli ingressi pagati e delle nuove voci di spesa è attiva con verifica dei conti, quadratura e salvataggio atomico. Per le ulteriori operazioni automatiche o manuali, il servizio dovrà gestire nella stessa transazione: conti attivi/movimentabili; conferma e immutabilità dei movimenti; quadratura di tutte le righe; corrispondenza tra conto, controparte e valuta di apertura/regolamento; limiti degli abbinamenti rispetto al residuo della partita e all'importo del pagamento; correttezza degli storni e concorrenza tra incassi. Queste regole tra più righe non sono garantite dai soli vincoli SQL di questo modello.

Nessuna operazione esistente viene contabilizzata retroattivamente. Gli acquisti degli abbonamenti sono riattivati. La migrazione Start resta la base; AddSospesiAbbonamenti aggiunge Sospesi e i dati di acquisto senza cancellare i dati esistenti. Applicare F5 nella schermata Connessione per aggiornare il database.

## Ingresso pagato dalla postazione cassa

RegisterEntry registra la scheda, la riga SchedaConto pagata e un movimento confermato INGRESSO_PAGATO nella stessa transazione serializzabile. Dare al conto 100 Cassa, Avere al conto 400 Ricavi diversi; entrambi devono essere attivi, movimentabili e di natura corretta. Non si aprono partite per questo incasso già saldato. La competenza è la data di inizio della giornata aperta; DataOra è l'istante di ingresso. Le righe conservano settore, etichetta tariffa, socio e tessera. La selezione operativa è per tariffa: se più settori di ingresso della stessa postazione la offrono, viene scelto il settore con Id minore.

OrigineEntita=SchedaConto e OrigineId identificano la riga operativa; la chiave evento è deterministica rispetto a tale identità. Un errore contabile annulla anche l'inserimento operativo. Le richieste di ingresso ripetute conservano i controlli già presenti su socio e posizione occupati.

Gli ingressi a zero restano registrati e pagati nell'operativo ma non producono movimenti monetari. Prezzi negativi o con frazioni di centesimo vengono rifiutati. Gli ingressi anteriori all'attivazione non vengono importati automaticamente. Le nuove voci di spesa sono collegate con le modalità della sezione seguente. Le operazioni anteriori all'attivazione non vengono contabilizzate retroattivamente.

Il cambio del flag pagamento e lo storno operativo sono bloccati sulle righe di ingresso già contabilizzate, finché non sarà implementata la relativa rettifica contabile. Impostare di nuovo lo stato pagato già presente è innocuo. Le voci prive di scrittura contabile mantengono il comportamento precedente.

Non è richiesta una nuova migrazione: vengono usate le cinque tabelle e i conti iniziali già inclusi nella Start.
## Voci di spesa, incassi e storni

Ogni nuovo click su una voce con prezzo positivo registra una riga SchedaConto non pagata, un movimento VOCE_SPESA (120 Crediti verso soci in Dare, 400 Ricavi diversi in Avere) e una partita di credito collegata alla riga Dare. Si conservano socio, tessera, operatore, postazione, giornata, settore effettivo del listino e etichetta della tariffa. Click distinti sulla stessa voce rappresentano addebiti distinti.

Il flag Pagato genera INCASSO_VOCE_SPESA: 100 Cassa in Dare e lo stesso conto crediti originale in Avere. Un abbinamento chiude la partita; il ricavo non viene duplicato. Ripetere il flag già presente non produce scritture. Il ritorno a Non pagato genera uno storno del precedente incasso e un abbinamento negativo riferito al precedente, riaprendo il credito. I cicli di pagamento conservano tutto lo storico e hanno chiavi evento distinte.

Lo storno di una nuova voce non pagata genera 400 Ricavi in Dare e crediti in Avere; un abbinamento chiude il credito. Lo storno di una voce già pagata genera Ricavi in Dare e il conto cassa dell'incasso in Avere, rappresentando la restituzione dell'importo; la partita già chiusa resta chiusa. Il movimento richiama la scrittura di ricavo originaria e la nuova riga operativa negativa. Un secondo storno della stessa voce viene rifiutato, così come il cambio pagamento dopo lo storno e sulle righe negative già contabilizzate.

Il flag corrisponde attualmente a un regolamento completo: partite con regolamenti parziali o diversi vengono rifiutate da questa funzione per evitare sovrapposizioni con le future funzioni di incasso. Prima di registrare si controllano i conti, la corrispondenza dell'importo originario, il residuo e lo stato della partita. Conti disattivati o non movimentabili impediscono l'operazione. Le rettifiche conservano gli identificativi dei conti e i dettagli storici originali.

Aggiunta, incasso, annullamento e storno condividono la transazione serializzabile dell'operazione di cassa. Se fallisce una parte, viene annullato tutto, anche se un precedente SaveChanges della stessa transazione era già riuscito. Le voci gratuite non producono importi contabili; prezzi negativi o con frazioni di centesimo non sono accettati. Le righe preesistenti prive di scrittura mantengono il comportamento operativo precedente. Rimane il blocco delle rettifiche sugli ingressi pagati, finché sarà sviluppato il loro percorso dedicato.

Il collegamento delle voci di spesa usa lo schema contabile già esistente. La funzione Sospesi descritta di seguito richiede invece una migrazione aggiuntiva.
## Acquisto abbonamenti e Sospesi

La nuova migrazione AddSospesiAbbonamenti conserva Start e i dati già presenti. Crea Sospesi e aggiunge ad Abbonamenti sei dati operativi di acquisto (identificativo, prezzo, stato al momento dell'acquisto, data, operatore e postazione), nullable per gli abbonamenti preesistenti. Prezzo, numero ingressi, durata e nome vengono conservati al momento dell'acquisto. La validità continua a partire dal giorno dell'acquisto, come nel modulo precedente; il sospeso non disattiva l'abbonamento.

- Pagato: abbonamento operativo e movimento confermato ACQUISTO_ABBONAMENTO, 100 Cassa Dare / 400 Ricavi Avere, nella stessa transazione. Nessuna partita o sospeso.
- Non pagato: abbonamento operativo e un Sospeso aperto. Non si registra ancora alcun ricavo, credito contabile o incasso, secondo la scelta richiesta.
- Saldo: il pulsante REGISTRA INCASSO crea ricavo e incasso per il prezzo originale, poi chiude il sospeso collegandolo al movimento. Il sospeso rimane nello storico con data e riferimenti di saldo. Ricavo e incasso competono alla giornata aperta al momento del saldo.
- Gratuito: registrazione operativa senza sospesi o movimenti monetari.

La richiesta di acquisto riutilizza lo stesso AcquistoId nelle riprove; i dati devono corrispondere al primo acquisto, compresi operatore e postazione. Una riprova restituisce il risultato registrato e non duplica abbonamenti, sospesi o incassi. Anche il saldo è idempotente. Tutte le scritture condividono la transazione serializzabile della cassa; un errore annulla l'intera operazione.

Sospesi contiene Tipo (Credito da incassare oppure Debito da pagare), Origine (Abbonamento, Uscita o Manuale), importo, valuta, stato, date, descrizione, note, uno o due soci con tessere e anagrafiche storiche, abbonamento, scheda e riga operativa, eventuale partita contabile già esistente, operatore, postazione e giornata. I riferimenti all'operativo sono storici, senza foreign key; quelli a partita e movimento di saldo sono vincolati e senza cancellazione in cascata.

Per l'abbonamento condiviso esiste un solo credito, intestato al primo socio e visibile anche cercando il secondo; non viene suddiviso o duplicato. La ricerca usa la persona associata alla tessera, quindi una nuova tessera dello stesso socio richiama il medesimo sospeso. La scheda e l'identificazione socio mostrano un richiamo dei crediti aperti. La sezione Sospesi consente ricerca per tessera, elenco generale e incasso degli abbonamenti; mostra importi da incassare e da pagare separatamente.

Esce Socio conserva il credito non saldato in Sospesi con origine Uscita. PartitaContabileId collega le voci già contabilizzate: il saldo genera Cassa a Crediti, con un abbinamento che chiude la partita senza ripetere il ricavo. Per voci operative pregresse prive di scrittura, l'incasso riconosce ricavo e incasso al saldo; se lasciate non pagate, restano solo in Sospesi fino al saldo. Anche i sospesi di uscita sono incassabili dalla Scheda/Sospesi.

Gli abbonamenti anteriori a questo collegamento non vengono trasformati automaticamente in incassi o sospesi, poiché non è noto il loro stato di acquisto.
### Sospesi e ingresso
EntraSocio segnala i sospesi aperti e blocca la scelta della tariffa e la registrazione dell'ingresso, anche nel servizio database. In questa vista non è disponibile la gestione dei sospesi.
L'incasso viene gestito nella sezione Scheda/Sospesi e regola la relativa contabilità senza duplicare ricavi già registrati. Il saldo non aggiunge righe a SchedaConto; dopo il saldo l'operatore può identificare nuovamente il socio e registrare l'ingresso.