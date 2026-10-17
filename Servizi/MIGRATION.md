# Servizi in Leonardo18

Migrazione della gestione tipi di abbonamento di Leonardo17: nome, numero ingressi, durata in giorni, prezzo e stato attivo. Inserimento, modifica, eliminazione, ricerca e ordinamento con lettura aggiornata prima di modificare.

Stessi controlli condivisi e versioni di Configurazione/Soci: .NET 10, Avalonia 12.1.4, ReactiveUI.Avalonia 12.1.4, ReactiveUI.Primitives, FluentIcons tramite ControlStyles. Pulsanti UiIconButton, PopupBorder e indicatore di attesa. Binding compilati, factory e router espliciti compatibili con NativeAOT; nessuna dipendenza da AutoMapper, Splat, System.Reactive o DataGrid riflessive.

Accesso EF esclusivamente nel DatabaseHost gestito tramite protocollo JSON generato. Operatore amministratore abilitato richiesto dal backend. Validazione nome, duplicati, valori interi non negativi e prezzi con due decimali. Non è consentita l'eliminazione di tipi già utilizzati. Nessuna modifica allo schema database.