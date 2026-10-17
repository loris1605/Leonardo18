namespace AppServices.Database;

public sealed class CassaTipoFidelity
{
    public int Id { get; set; }
    public string Nome { get; set; } = "";
    public int Traguardo { get; set; }
    public int TipoPremioId { get; set; }
    public decimal ValorePremio { get; set; }
    public int DurataGiorni { get; set; }
    public string Descrizione => $"{Nome} Â· {Traguardo} timbri Â· "+(TipoPremioId==1?"ingresso Fidelity":$"sconto {ValorePremio:N2} â‚¬");
}
public sealed class CassaTimbroFidelity
{
    public DateTime? DataOra { get; set; }
    public int DataStorica { get; set; }
    public string Data => DataOra is {} at?at.ToString("dd/MM/yyyy\nHH:mm"):CassaEntryRules.FormatDate(DataStorica)+"\norario non disponibile";
}
public sealed class CassaFidelity
{
    public int Id { get; set; }
    public string Nome { get; set; } = "";
    public bool Attiva { get; set; }
    public DateTime? AttivataIl { get; set; }
    public DateTime? ScadeIl { get; set; }
    public int Traguardo { get; set; }
    public int Punti { get; set; }
    public int TipoPremioId { get; set; }
    public decimal CreditoSconto { get; set; }
    public CassaTimbroFidelity[] Timbri { get; set; } = [];
    public bool CanIngresso => Attiva&&TipoPremioId==1&&Traguardo>0&&Punti>=Traguardo;
    public bool HasCredito => CreditoSconto>0;
    public string Progresso => Attiva?$"{Punti} timbri Â· traguardo {Traguardo}" : "Fidelity da attivare o rinnovare";
    public string Scadenza => ScadeIl is {} end?$"Scadenza raccolta: {end:dd/MM/yyyy}":"Raccolta senza scadenza";
    public string Premio => CanIngresso?$"{Punti/Traguardo} "+(Punti/Traguardo==1?"ingresso Fidelity disponibile":"ingressi Fidelity disponibili"):TipoPremioId==2?$"Credito sconto maturato: {CreditoSconto:N2} â‚¬":"";
}

public sealed class CassaMovimentoFidelity
{
    public int Id {get;set;}
    public int Ciclo {get;set;}
    public int TipoMovimento {get;set;}
    public DateTime? DataOra {get;set;}
    public int DataStorica {get;set;}
    public int Punti {get;set;}
    public decimal Importo {get;set;}
    public string Data => DataOra is {} at ? at.ToString("dd/MM/yyyy HH:mm") : CassaEntryRules.FormatDate(DataStorica)+" · orario non disponibile";
    public string Descrizione => TipoMovimento switch {1=>"Timbro ingresso",2=>"Ingresso Fidelity utilizzato",3=>"Premio sconto maturato",4=>"Sconto su acquisto",5=>"Storno sconto: credito ripristinato",6=>"Sconto su ingresso",7=>"Timbro annullato · NON ENTRA",8=>"Ingresso Fidelity restituito",9=>"Premio sconto annullato",_=>"Movimento Fidelity"};
    public string Dettaglio => $"Ciclo {Ciclo} · Timbri {Punti:+0;-0;0} · Credito {Importo:+0.00;-0.00;0.00} €";
}
