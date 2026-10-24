using AppServices.Database;
using ReactiveUI;
using ReactiveUI.Primitives;
namespace Cassa.ViewModels;
public sealed partial class CassaViewModel
{
    private string _elencoSort=nameof(CassaScheda.Posizione);
    private bool _elencoDescending;
    private static readonly IComparer<string> PositionComparer=Comparer<string>.Create((a,b)=>{
        var an=a is {Length:>0}&&a.All(char.IsAsciiDigit);
        var bn=b is {Length:>0}&&b.All(char.IsAsciiDigit);
        if(an!=bn)return an?-1:1;
        if(an)
        {
            var av=a.TrimStart('0');var bv=b.TrimStart('0');
            var length=av.Length.CompareTo(bv.Length);if(length!=0)return length;
            var value=StringComparer.Ordinal.Compare(av,bv);if(value!=0)return value;
        }
        return StringComparer.CurrentCultureIgnoreCase.Compare(a,b);
    });
    private string ElencoHeader(string key,string title)=>title+(_elencoSort==key?(_elencoDescending?" ▼":" ▲"):"");
    public string ElencoPosizioneHeader=>ElencoHeader(nameof(CassaScheda.Posizione),"POSIZIONE");
    public string ElencoCognomeHeader=>ElencoHeader(nameof(CassaScheda.Cognome),"COGNOME");
    public string ElencoNomeHeader=>ElencoHeader(nameof(CassaScheda.Nome),"NOME");
    public string ElencoNumeroTesseraHeader=>ElencoHeader(nameof(CassaScheda.NumeroTessera),"TESSERA");
    public string ElencoNatoilHeader=>ElencoHeader(nameof(CassaScheda.Natoil),"NASCITA");
    public string ElencoCheckinTimeHeader=>ElencoHeader(nameof(CassaScheda.CheckinTime),"INGRESSO");
    public string ElencoGrb1Header=>ElencoHeader(nameof(CassaScheda.Grb1),"GRB 1");
    public string ElencoGrb2Header=>ElencoHeader(nameof(CassaScheda.Grb2),"GRB 2");
    public string ElencoGrb3Header=>ElencoHeader(nameof(CassaScheda.Grb3),"GRB 3");
    public string ElencoGrb4Header=>ElencoHeader(nameof(CassaScheda.Grb4),"GRB 4");
    public string ElencoConsumazioneHeader=>ElencoHeader(nameof(CassaScheda.Consumazione),"CONSUMAZIONE");
    public string ElencoBloccoHeader=>ElencoHeader(nameof(CassaScheda.Blocco),"BLOCCO");
    public string ElencoNoteHeader=>ElencoHeader(nameof(CassaScheda.Note),"NOTE");
    public ReactiveCommand<string,RxVoid> OrdinaSociCommand {get;private set;}=null!;
    private void InitializeElencoSoci()
    {
        OrdinaSociCommand=ReactiveCommand.Create<string>(key=>{
            if(!IsList||!IsUiReady||key is not (nameof(CassaScheda.Posizione) or nameof(CassaScheda.Cognome) or nameof(CassaScheda.Nome) or nameof(CassaScheda.NumeroTessera) or nameof(CassaScheda.Natoil) or nameof(CassaScheda.CheckinTime) or nameof(CassaScheda.Grb1) or nameof(CassaScheda.Grb2) or nameof(CassaScheda.Grb3) or nameof(CassaScheda.Grb4) or nameof(CassaScheda.Consumazione) or nameof(CassaScheda.Blocco) or nameof(CassaScheda.Note)))return;
            _elencoDescending=key==_elencoSort&&!_elencoDescending;_elencoSort=key;
            ApplyFilter();
            foreach(var name in new[]{nameof(ElencoPosizioneHeader),nameof(ElencoCognomeHeader),nameof(ElencoNomeHeader),nameof(ElencoNumeroTesseraHeader),nameof(ElencoNatoilHeader),nameof(ElencoCheckinTimeHeader),nameof(ElencoGrb1Header),nameof(ElencoGrb2Header),nameof(ElencoGrb3Header),nameof(ElencoGrb4Header),nameof(ElencoConsumazioneHeader),nameof(ElencoBloccoHeader),nameof(ElencoNoteHeader)})this.RaisePropertyChanged(name);
        },_ready);
        _subscriptions.Add(OrdinaSociCommand.ThrownExceptions.SubscribePrimitives(_=>{if(!_isClosing)InfoLabel="Impossibile ordinare l'elenco soci.";}));
    }
    private IOrderedEnumerable<CassaScheda> OrderElenco(IEnumerable<CassaScheda> rows)
    {
        IOrderedEnumerable<CassaScheda> Order<T>(Func<CassaScheda,T> key,IComparer<T>? comparer=null)=>
            _elencoDescending?rows.OrderByDescending(key,comparer):rows.OrderBy(key,comparer);
        var text=StringComparer.CurrentCultureIgnoreCase;
        var ordered=_elencoSort switch {
            nameof(CassaScheda.Cognome)=>Order(x=>x.Cognome, text),
            nameof(CassaScheda.Nome)=>Order(x=>x.Nome, text),
            nameof(CassaScheda.NumeroTessera)=>Order(x=>x.NumeroTessera, PositionComparer),
            nameof(CassaScheda.Natoil)=>Order(x=>x.Natoil),
            nameof(CassaScheda.CheckinTime)=>Order(x=>x.CheckinTime),
            nameof(CassaScheda.Grb1)=>Order(x=>x.Grb1),
            nameof(CassaScheda.Grb2)=>Order(x=>x.Grb2),
            nameof(CassaScheda.Grb3)=>Order(x=>x.Grb3),
            nameof(CassaScheda.Grb4)=>Order(x=>x.Grb4),
            nameof(CassaScheda.Consumazione)=>Order(x=>x.Consumazione),
            nameof(CassaScheda.Blocco)=>Order(x=>x.Blocco),
            nameof(CassaScheda.Note)=>Order(x=>x.Note, text),
            _=>Order(x=>x.Posizione,PositionComparer)
        };
        return ordered.ThenBy(x=>x.Id);
    }
}
