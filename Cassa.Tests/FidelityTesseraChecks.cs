using Avalonia;
using AppServices.Database;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Cassa.Core.Repository;
using Cassa.ViewModels;
using Cassa.Views;
using ReactiveUI;
using ReactiveUI.Primitives;

internal static class FidelityTesseraChecks
{
    public static int Count;
    private static void Check(bool ok,string text){if(!ok)throw new Exception(text);Count++;}
    private static void Pump(Func<bool> ready){var end=DateTime.UtcNow.AddSeconds(15);while(!ready()){Dispatcher.UIThread.RunJobs();Thread.Sleep(1);if(DateTime.UtcNow>end)throw new TimeoutException("Tessera Fidelity");}Dispatcher.UIThread.RunJobs();}
    private static void Command(ReactiveCommand<RxVoid,RxVoid> command){var done=false;using var sub=command.Execute().SubscribePrimitives(_=>{},e=>throw e,()=>done=true);Pump(()=>done);}
    private static CassaFidelity Card(int points=2,int target=5,int kind=1,bool active=true)=>new(){Id=1,Nome="Fidelity ingressi",Attiva=active,AttivataIl=DateTime.Today.AddDays(-10),Traguardo=target,Punti=points,TipoPremioId=kind,
        Timbri=Enumerable.Range(0,Math.Min(points,5)).Select(i=>new CassaTimbroFidelity{DataOra=DateTime.Today.AddDays(-1).AddHours(9).AddMinutes(i*15)}).ToArray()};
    public static void UI()
    {
        var fake=new Fake{Fidelity=Card()};var vm=new CassaViewModel(new Screen(),fake,100);var view=new CassaView{ViewModel=vm};var window=new Window{Width=1400,Height=1000,Content=view};window.Show();Pump(()=>vm.IsUiReady);
        void Identify(){Command(vm.EntraSocioCommand);vm.NumeroTessera="T100";Command(vm.FindCardCommand);}
        Button Current()=>view.GetVisualDescendants().OfType<Button>().Single(x=>x.DataContext is TimbroIngressoFidelityViewModel{Corrente:true});
        void Stamp(){var count=fake.Entries;var button=Current();Check(button.IsEffectivelyEnabled,"Timbro corrente utilizzabile con ingresso completo");button.Command!.Execute(button.CommandParameter);Pump(()=>fake.Entries>count&&vm.IsUiReady);Check(fake.Last?.TimbrareFidelity==true,"Solo il comando del timbro richiede la timbratura");}
        Identify();
        Check(vm.TimbriTesseraFidelity.Count==5,"Tessera unica con timbri registrati e futuri");
        Check(vm.TimbriRegistratiFidelity.Count==2&&vm.TimbriRegistratiFidelity.All(x=>x.Registrato&&!x.Corrente),"Timbri precedenti datati e non riutilizzabili");
        Check(vm.TimbriRegistratiFidelity[0].Stato.Contains("09:15")&&vm.TimbriRegistratiFidelity[1].Stato.Contains("09:00"),"Data e ora conservate, più recente per primo");
        Check(vm.TimbriIngressoFidelity.Count==3&&vm.TimbriIngressoFidelity.Count(x=>x.Corrente)==1,"Un timbro corrente e spazi fino al prossimo traguardo");
        Check(vm.TimbriIngressoFidelity.First().Simbolo=="+"&&vm.TimbriIngressoFidelity.Skip(1).All(x=>!x.Registrato&&!x.Corrente),"Timbro dell'ingresso evidenziato, futuri non utilizzabili");
        Check(ReferenceEquals(Current().Command,vm.TimbraERegistraIngressoCommand),"Timbro collegato alla conferma con timbratura esplicita");
        Check(!Current().IsEffectivelyEnabled&&fake.Entries==0&&fake.Fidelity!.Timbri.Length==2,"Posizione mancante: nessun timbro anticipato");
        vm.EntryPosition="F100";Dispatcher.UIThread.RunJobs();
        Check(Current().IsEffectivelyEnabled,"Posizione completa abilita la conferma dal timbro");
        Check(view.GetVisualDescendants().OfType<Button>().Where(x=>x.DataContext is TimbroIngressoFidelityViewModel{Registrato:true}).All(x=>!x.IsEffectivelyEnabled),"Timbri registrati disabilitati nella vista");
        foreach(var size in new[]{(1400,1000),(1000,760)})
        {
            window.Width=size.Item1;window.Height=size.Item2;Dispatcher.UIThread.RunJobs();using var frame=window.CaptureRenderedFrame();
            var folder=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"..","..","..","previews"));Directory.CreateDirectory(folder);
            frame!.Save(Path.Combine(folder,$"tessera-fidelity-{size.Item1}.png"),Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
        }
        fake.Fail=true;Stamp();Check(vm.IsEntry&&vm.InfoLabel.Contains("Errore ingresso")&&vm.TimbriRegistratiFidelity.Count==2,"Errore ingresso non crea un timbro visuale inesistente");
        fake.Fail=false;Command(vm.RegisterEntryCommand);
        Check(fake.Last?.TimbrareFidelity==false&&vm.IsPostazione,"Registra ingresso non timbra, anche dopo un tentativo dal timbro fallito");
        Identify();vm.EntryPosition="F100";
        fake.Fail=false;Stamp();Check(vm.IsPostazione&&fake.Last is {TariffaId:100,IngressoFidelityId:0,AbbonamentoIngressoId:0},"Il timbro conferma l'ingresso ordinario selezionato");
        Identify();vm.EntryPosition="F100";
        var bollino=vm.AbbonamentiIngresso.Single().Bollini.Single();var chosen=false;using(var sub=vm.SelezionaBollinoCommand.Execute(bollino).SubscribePrimitives(_=>{},e=>throw e,()=>chosen=true))Pump(()=>chosen);
        Stamp();Check(fake.Last is {TariffaId:0,AbbonamentoIngressoId:9,NumeroIngressoAbbonamento:1,IngressoFidelityId:0},"Il timbro mantiene la scelta dell'ingresso abbonamento");
        fake.Fidelity=Card(5);Identify();vm.EntryPosition="F100";Command(vm.IngressoFidelityCommand);
        Check(vm.TimbriIngressoFidelity.Count==5&&vm.ShowIngressoFidelity,"Traguardo raggiunto: premio disponibile e nuova raccolta rappresentata");
        Stamp();Check(fake.Last is {IngressoFidelityId:1,TariffaId:0,AbbonamentoIngressoId:0},"Il timbro conferma anche l'ingresso premio selezionato");
        fake.Return=true;Identify();Check(vm.HasRientro&&vm.TimbriIngressoFidelity.All(x=>!x.Corrente)&&vm.TimbroIngressoFidelityInfo.Contains("RIENTRO"),"Rientro senza timbro dell'ingresso corrente");
        fake.Return=false;fake.Pending=true;Identify();Check(vm.TimbriIngressoFidelity.All(x=>!x.Corrente)&&vm.TimbroIngressoFidelityInfo.Contains("sospesi"),"Sospesi da saldare impediscono il timbro");
        fake.Pending=false;fake.Blocked=true;Identify();Check(vm.TimbriIngressoFidelity.All(x=>!x.Corrente),"Socio bloccato non può timbrare");
        fake.Blocked=false;fake.Inside=true;Identify();Check(vm.TimbriIngressoFidelity.All(x=>!x.Corrente),"Socio già dentro senza nuovo timbro");
        fake.Inside=false;fake.Fidelity=Card(active:false);Identify();Check(vm.TimbriIngressoFidelity.Count==0&&vm.TimbriRegistratiFidelity.Count==2,"Fidelity non attiva mostra solo timbri storici");
        fake.Fidelity=Card(0);Identify();Check(vm.TimbriIngressoFidelity.Count==5&&vm.TimbriRegistratiFidelity.Count==0,"Raccolta nuova mostra tutti gli spazi senza date inventate");
        fake.Fidelity=Card(2,5,2);Identify();Check(vm.TimbriIngressoFidelity.Count==3&&vm.TimbriIngressoFidelity.Any(x=>x.Corrente),"Raccolta premio denaro utilizza la stessa tessera");
        fake.Fidelity=Card();fake.Fidelity.Timbri=Enumerable.Range(0,100).Select(i=>new CassaTimbroFidelity{DataOra=DateTime.Today.AddDays(-100+i)}).ToArray();Identify();
        var current=Current();var scroller=current.GetVisualAncestors().OfType<ScrollViewer>().First();var position=current.TranslatePoint(new Avalonia.Point(0,0),scroller)!.Value;
        Check(vm.TimbriRegistratiFidelity.Count==100&&vm.TimbriTesseraFidelity.Count==103,"Storico lungo conserva tutti i timbri e le loro date");
        Check(position.Y>=-1&&position.Y+current.Bounds.Height<=scroller.Bounds.Height+1,"Storico lungo scorre fino al timbro corrente");
        fake.Fidelity=Card(0,int.MaxValue);Identify();Check(vm.TimbriIngressoFidelity.Count==40&&!string.IsNullOrEmpty(vm.AltriSpaziFidelity),"Traguardo molto grande non genera milioni di controlli");
        vm.NumeroTessera="ALTRA";Check(!vm.HasFidelity&&vm.TimbriIngressoFidelity.Count==0&&vm.TimbriRegistratiFidelity.Count==0,"Cambio tessera cancella anche bollini e date del socio precedente");
        window.Close();Dispatcher.UIThread.RunJobs();
    }
    private sealed class Screen:ReactiveObject,IScreen{public RoutingState Router{get;}=new();}
    private sealed class Fake:ICassaRepository
    {
        public CassaFidelity? Fidelity;public bool Return,Pending,Blocked,Inside,Fail;public int Entries;public CassaRequest? Last;
        public Task<CassaResult> Execute(CassaRequest r,CancellationToken ct=default)
        {
            if(r.Action==CassaAction.PrepareEntry)return Task.FromResult(new CassaResult{Ingressi=[new(){Id=100,Etichetta="Intero",Prezzo=10}]});
            if(r.Action==CassaAction.FindCard)return Task.FromResult(new CassaResult{Socio=new(){PersonId=100,Cognome="ROSSI",Nome="MARIO",NumeroTessera=r.NumeroTessera,ScadenzaTessera=20991231,
                SospesiAperti=Pending?1:0,TotaleSospesi=Pending?10:0,SocioBloccato=Blocked,PosizioneOccupata=Inside?"F100":""},Fidelity=Fidelity,
                Rientro=Return?new(){Tipo="Giornata",UltimoIngresso=DateTime.Now.AddHours(-1)}:null,
                AbbonamentiIngresso=[new(){Id=9,Nome="Abbonamento 5 ingressi",DataFine=DateTime.Today.AddDays(30),Bollini=[new(){AbbonamentoId=9,Numero=1}]}]});
            if(r.Action==CassaAction.RegisterEntry){Entries++;Last=r;if(Fail)throw new InvalidOperationException("Errore ingresso simulato");return Task.FromResult(new CassaResult{Scheda=new(){Id=100,Posizione=r.Posizione}});}
            if(r.Action==CassaAction.OpenSheet)return Task.FromResult(new CassaResult{Scheda=new(){Id=100,Posizione=r.Posizione},Fidelity=Fidelity});
            return Task.FromResult(new CassaResult{PostazioneName="Cassa"});
        }
    }
}
