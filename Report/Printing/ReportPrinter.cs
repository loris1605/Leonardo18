using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Text;
using AppServices.Database;
namespace Report.Printing;

public interface IReportPrinter
{
    Task OpenAsync(ReportResult data,CancellationToken cancellationToken=default);
}

public sealed class ReportPrinter:IReportPrinter
{
    public async Task OpenAsync(ReportResult data,CancellationToken cancellationToken=default)
    {
        var html=ReportPrintDocument.Create(data);
        var folder=Path.Combine(Path.GetTempPath(),"Leonardo18","Report");
        Directory.CreateDirectory(folder);
        var path=Path.Combine(folder,$"Report-{data.GiornataId}-{Guid.NewGuid():N}.html");
        await File.WriteAllTextAsync(path,html,new UTF8Encoding(false),cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        Process.Start(new ProcessStartInfo(path){UseShellExecute=true});
    }
}

/// <summary>Printable snapshot of the displayed result; does not read or change database records.</summary>
public static class ReportPrintDocument
{
    public static string Create(ReportResult data)
    {
        if(data.GiornataId==0)throw new InvalidOperationException("Visualizza una giornata prima di stampare.");
        var culture=CultureInfo.GetCultureInfo("it-IT");
        string E(string? text)=>WebUtility.HtmlEncode(text??"");
        string N(decimal value)=>value.ToString("N2",culture);
        string D(DateTime value)=>value.ToString("dd/MM/yyyy HH:mm",culture);
        var day=data.Giornate.FirstOrDefault(x=>x.Id==data.GiornataId);
        var b=new StringBuilder("<!doctype html><html lang=\"it\"><head><meta charset=\"utf-8\"><title>Leonardo18 - Report giornata "+data.GiornataId+"</title>");
        b.Append("""
<style>
@page {size:A4 landscape;margin:14mm;}
*{box-sizing:border-box}body{font:11pt Arial,sans-serif;color:#172b3a;margin:24px;line-height:1.4}
h1{font-size:23pt;margin:0 0 6px}h2{font-size:15pt;margin:22px 0 8px;break-after:avoid}
p{margin:5px 0}.muted{color:#475569;font-size:10pt}.toolbar{padding:14px;background:#edf2f7;margin-bottom:24px}
button{padding:10px 20px;font-size:12pt;cursor:pointer}table{border-collapse:collapse;width:100%;margin:8px 0 16px;table-layout:fixed}
th,td{padding:7px 8px;border-bottom:1px solid #ccd4dc;text-align:right;overflow-wrap:anywhere;vertical-align:top}
th{background:#e8edf2;font-size:9pt}td:first-child,th:first-child{text-align:left;width:28%}
thead{display:table-header-group}tr{break-inside:avoid}.summary{display:grid;grid-template-columns:repeat(4,1fr);gap:8px;margin:16px 0}
.summary div{border:1px solid #ccd4dc;padding:10px}.summary strong{display:block;font-size:16pt}
@media print{body{margin:0;font-size:9pt}.toolbar{display:none}h1{font-size:19pt}h2{font-size:13pt}th,td{padding:5px 6px}.summary strong{font-size:13pt}}
</style></head><body><div class="toolbar"><button onclick="window.print()">Stampa / Salva in PDF</button>
<span> Scegli la stampante o il salvataggio in PDF nella finestra di stampa.</span></div>
""");
        b.Append("<h1>Leonardo18 · Report giornata #").Append(data.GiornataId).Append("</h1>");
        if(day is not null)b.Append("<p>Giornata ").Append(day.Aperta?"aperta":"chiusa").Append(" · Dal ").Append(D(day.Inizio)).Append(" al ").Append(D(data.SituazioneAl)).Append("</p>");
        else b.Append("<p>Situazione al ").Append(D(data.SituazioneAl)).Append("</p>");
        b.Append("<p class=\"muted\">Dati aggiornati al ").Append(D(data.AggiornatoAl)).Append(" · Copia dei dati visualizzati a video.</p><div class=\"summary\">");
        void Metric(string name,string value)=>b.Append("<div>").Append(E(name)).Append("<strong>").Append(E(value)).Append("</strong></div>");
        Metric("Ingressi",data.Ingressi.ToString(culture));Metric("Presenti",data.Presenti.ToString(culture));Metric("Uscite",data.Uscite.ToString(culture));Metric("Totale operativo €",N(data.TotaleOperativo));
        Metric("Pagato operativo €",N(data.PagatoOperativo));Metric("Da pagare operativo €",N(data.DaPagareOperativo));Metric("Incassi contabili €",N(data.Incassi));Metric("Ricavi contabili €",N(data.Ricavi));b.Append("</div>");
        void Header(string title,params string[] columns){b.Append("<h2>").Append(E(title)).Append("</h2><table><thead><tr>");foreach(var col in columns)b.Append("<th scope=\"col\">").Append(E(col)).Append("</th>");b.Append("</tr></thead><tbody>");}
        void Row(params string[] cells){b.Append("<tr>");foreach(var cell in cells)b.Append("<td>").Append(E(cell)).Append("</td>");b.Append("</tr>");}
        void Empty(int count)=>b.Append("<tr><td colspan=\"").Append(count).Append("\">Nessun movimento nel periodo.</td></tr>");
        void End()=>b.Append("</tbody></table>");
        void Summary(string title,ReportRiepilogo[] rows){Header(title,"Nome","Operazioni","Lordo €","Storni €","Netto €","Pagato €","Da pagare €");foreach(var x in rows)Row(x.Nome,x.Operazioni.ToString(culture),N(x.Lordo),N(x.Storni),N(x.Netto),N(x.Pagato),N(x.DaPagare));if(rows.Length==0)Empty(7);End();}
        Header("Cassa Iniziale","Taglio €","Quantità","Subtotale €");
        foreach(var x in data.TagliCassaIniziale)Row(N(x.Taglio),x.Quantita.ToString(culture),N(x.Totale));
        if(data.TagliCassaIniziale.Length==0)Empty(3);End();
        b.Append("<p><strong>Cassa Iniziale: ").Append(N(data.CassaIniziale)).Append(" €</strong> · Variazione Cassa + Banca: ").Append(N(data.SaldoMonetario)).Append(" € · Disponibilità con cassa iniziale: ").Append(N(data.DisponibilitaMonetaria)).Append(" €</p><p class=\"muted\">Il fondo iniziale è patrimoniale e non costituisce un incasso o un ricavo.</p>");
        Summary("Settori",data.Settori);
        Header("Voci di spesa","Settore","Dettaglio","Operazioni","Netto €","Pagato €","Da pagare €");
        foreach(var x in data.Voci)Row(x.Settore,x.Descrizione,x.Operazioni.ToString(culture),N(x.Totale),N(x.Pagato),N(x.DaPagare));if(data.Voci.Length==0)Empty(6);End();
        Summary("Postazioni",data.Postazioni);
        Header("Contabilità","Settore","Ricavi €","Costi €","Differenza €");
        foreach(var x in data.Contabilita)Row(x.Settore,N(x.Ricavi),N(x.Costi),N(x.Margine));if(data.Contabilita.Length==0)Empty(4);End();
        b.Append("<p>Costi: ").Append(N(data.Costi)).Append(" € · Uscite monetarie: ").Append(N(data.UsciteMonetarie)).Append(" € · Saldo Cassa + Banca: ").Append(N(data.SaldoMonetario)).Append(" €</p>");
        Header("Sospesi e abbonamenti","Indicatore","Valore");
        Row("Sospesi aperti a fine periodo",data.NumeroSospesi.ToString(culture));Row("Crediti da incassare",N(data.SospesiCrediti)+" €");Row("Debiti da pagare",N(data.SospesiDebiti)+" €");Row("Sospesi incassati nel periodo",N(data.SospesiIncassati)+" €");Row("Abbonamenti acquistati nel periodo",data.AbbonamentiVenduti.ToString(culture));Row("Valore acquisti abbonamenti",N(data.ValoreAbbonamenti)+" €");End();
        b.Append("<p class=\"muted\">Pagato e da pagare rappresentano lo stato attuale delle voci operative. La contabilità include le scritture confermate della giornata. I sospesi comprendono anche le giornate precedenti. Il valore degli abbonamenti comprende acquisti pagati e non pagati e non equivale all'incasso.</p></body></html>");
        return b.ToString();
    }
}
