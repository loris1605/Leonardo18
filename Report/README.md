# Report della giornata

STAMPA REPORT (Ctrl+P) apre nel browser una copia impaginata A4 orizzontale di tutti i dati visualizzati, incluse tutte le sezioni e le righe fuori schermo. Il pulsante Stampa / Salva in PDF apre la scelta della stampante nel browser. Per stampare una diversa giornata, caricarla prima con Visualizza / Aggiorna. La copia indica giornata, periodo e ora dei dati e non rilegge il database. I file di stampa sono salvati in una sottocartella Report della cartella temporanea di Leonardo18.


Il menu Report apre un modulo in sola lettura, con lo stesso stile degli altri moduli. L'accesso richiede un operatore abilitato con almeno una postazione di tipo Amministrazione, Cassa o Report (1, 2, 6). Anche il servizio database verifica i permessi.

La giornata aperta viene proposta per prima; in assenza di giornate aperte si propone la più recente. Selezionare un'altra giornata e premere Visualizza / Aggiorna (F5) per consultarla. Il report non aggiorna né ricrea scritture contabili.

- Ingressi e uscite: conteggio delle schede con entrata/uscita nel periodo della giornata. Presenti: schede entrate e non ancora uscite al termine del periodo, inclusi ingressi precedenti.
- Settori e postazioni: voci di SchedaConto registrate nel periodo. Lordo = importi positivi; storni = valore assoluto degli importi negativi; netto = lordo meno storni. Gli omaggi contano come operazioni. I settori configurati senza operazioni vengono mostrati con zero; i nomi storici restano visibili anche dopo modifiche di configurazione.
- Pagato e da pagare operativo: stato attuale delle voci del periodo. Non ricostruisce il flag di pagamento alla chiusura storica della giornata.
- Contabilità: solo scritture confermate associate all'ID della giornata. Ricavi e costi sono al netto delle rettifiche. Incassi e uscite monetarie derivano dal flusso netto di ogni movimento sui conti 100 Cassa e 110 Banca: i trasferimenti interni non producono nuovi incassi. Questi dati rimangono distinti dai flag operativi.
- Sospesi: crediti e debiti creati prima della fine del periodo e ancora aperti in quel momento, anche se saldati successivamente. I sospesi relativi a due soci vengono contati una sola volta. Gli incassi dei sospesi sono mostrati per la data di saldo.
- Abbonamenti: numero e valore congelato degli acquisti nel periodo, pagati e non pagati; questo valore non equivale all'incasso.

Il periodo usa gli orari effettivi della giornata e attraversa correttamente la mezzanotte. Le letture vengono eseguite in un'unica transazione per evitare totali incoerenti durante aggiornamenti concorrenti. I dati eliminati dal database non vengono ricostruiti dal report. Non servono nuove tabelle o migrazioni.
Le schede uscite sono conservate in SchedeUscite/SchedeUsciteConto e vengono incluse insieme alle schede attive: la chiusura di una presenza non cancella ingressi o consumi dai Report. I successivi saldi dei sospesi aggiornano il pagamento delle righe storiche.
