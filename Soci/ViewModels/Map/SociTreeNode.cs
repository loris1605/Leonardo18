using System.Collections.ObjectModel;
using AppServices.Database;
namespace Soci.ViewModels.Map;
public enum SociNodeKind { Person, Socio, Tessera }
/// <summary>Typed hierarchy: no property-name grouping or reflected accessors.</summary>
public sealed class SociTreeNode(SociNodeKind kind, SociPersonMap row)
{
#if DEBUG
    ~SociTreeNode() => global::Leonardo18.Diagnostics.GcDebugLog.Collected(this);
#endif

    public SociNodeKind Kind { get; } = kind;
    public SociPersonMap Row { get; } = row;
    public ObservableCollection<SociTreeNode> Children { get; } = [];
    public bool IsExpanded { get; set; } = false;
    public bool IsGroup => Kind != SociNodeKind.Tessera;
    public string Label => Kind switch {
        SociNodeKind.Person => $"{Row.Cognome} {Row.Nome} — {Row.NatoilText}",
        SociNodeKind.Socio => $"Codice socio: {Row.NumeroSocio}",
        _ => "Tessera" };
    public string NumeroTessera => Kind == SociNodeKind.Tessera ? Row.NumeroTessera : "";
    public string Scadenza => Kind == SociNodeKind.Tessera ? Row.ScadenzaText : "";

    public static SociTreeNode[] Build(IEnumerable<SociPersonMap> rows)
    {
        return rows.GroupBy(x => x.Id).Select(person => {
            var data = person.First().ToDto();
            data.CodiceSocio=0; data.NumeroSocio=""; data.CodiceTessera=0; data.NumeroTessera=""; data.Scadenza=0;
            var parent = new SociTreeNode(SociNodeKind.Person,new SociPersonMap(data));
            foreach(var member in person.Where(x=>x.CodiceSocio>0).GroupBy(x=>x.CodiceSocio))
            {
                var memberData=member.First().ToDto();
                memberData.CodiceTessera=0; memberData.NumeroTessera=""; memberData.Scadenza=0;
                var child=new SociTreeNode(SociNodeKind.Socio,new SociPersonMap(memberData));
                foreach(var card in member.Where(x=>x.CodiceTessera>0).DistinctBy(x=>x.CodiceTessera))
                    child.Children.Add(new(SociNodeKind.Tessera,card));
                parent.Children.Add(child);
            }
            return parent;
        }).ToArray();
    }
    public IEnumerable<SociTreeNode> DescendantsAndSelf()
    {
        yield return this;
        foreach(var child in Children)
            foreach(var node in child.DescendantsAndSelf())yield return node;
    }
}