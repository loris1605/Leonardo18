using AppServices.Database;
namespace Cassa.ViewModels;

public sealed class TimbroIngressoFidelityViewModel(int numero,CassaTimbroFidelity? timbro=null,bool corrente=false)
{
    public int Numero => numero;
    public bool Registrato => timbro is not null;
    public bool Corrente => corrente;
    public string Simbolo => Registrato?"✓":Corrente?"+":numero.ToString();
    public string Stato => timbro?.Data ?? (Corrente?"TIMBRA E\nREGISTRA INGRESSO":"Da usare");
    public string Sfondo => Registrato?"#E2E8F0":Corrente?"#BBF7D0":"#FFFFFF";
    public string Bordo => Registrato?"#94A3B8":Corrente?"#15803D":"#B99136";
    public string Aiuto => Registrato?"Timbro registrato: "+timbro!.Data.Replace('\n',' '):Corrente?
        "Conferma la tariffa e la posizione, poi premi per registrare l'ingresso e il suo timbro insieme.":"Spazio per un ingresso futuro.";
}
