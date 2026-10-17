namespace AppServices.Database;
public enum ServiziSection { Abbonamenti, Fidelity }
public enum ServiziAction { Load, Read, Add, Update, Delete }
public sealed class ServiziRecord
{
    public int Id { get; set; }
    public string Nome { get; set; } = "";
    public int NumeroIngressi { get; set; }
    public int TipoPremioId { get; set; } = 1;
    public int DurataGiorni { get; set; }
    public decimal Prezzo { get; set; }
    public bool Attivo { get; set; }
    public string Premio => TipoPremioId==2?"Sconto in denaro":"Ingresso Fidelity"; public string Stato => Attivo ? "Attivo" : "Disattivo";
    public ServiziRecord Copy() => new() { Id=Id, TipoPremioId=TipoPremioId, Nome=Nome, NumeroIngressi=NumeroIngressi, DurataGiorni=DurataGiorni, Prezzo=Prezzo, Attivo=Attivo };
}
public sealed class ServiziRequest
{
    public ServiziAction Action { get; set; }
    public ServiziSection Section { get; set; }
    public int RecordId { get; set; }
    public ServiziRecord? Record { get; set; }
}
public sealed class ServiziResult
{
    public ServiziRecord[] Records { get; set; } = [];
    public int SelectedId { get; set; }
}