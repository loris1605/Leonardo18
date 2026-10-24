using System.Text.Json;
using AppServices.Database;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Cassa.Core.Repository;
using Cassa.ViewModels;
using Cassa.Views;
using Leonardo18.DatabaseHost.Cassa;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Models.Context;
using ReactiveUI;
using ReactiveUI.Primitives;
internal static class BloccoSchedaChecks
{
    public static int Count;
    private static void Check(bool value,string text){if(!value)throw new Exception(text);Count++;}
    private static async Task Reject(Func<Task> action,string text)
    {try{await action();}catch(InvalidOperationException e)when(e.Message.Contains(text,StringComparison.OrdinalIgnoreCase)){Count++;return;}throw new Exception("Operazione non rifiutata: "+text);}
    public static async Task Database()
    {
        await using var connection=new SqliteConnection("Data Source=:memory:");await connection.OpenAsync();
        var options=new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        await using(var db=new AppDbContext(options))
        {
            await db.Database.EnsureCreatedAsync();
            db.Postazioni.AddRange(new(){Id=100,Nome="Cassa A",TipoPostazioneId=2,TipoRientroId=-1},new(){Id=200,Nome="Cassa B",TipoPostazioneId=2,TipoRientroId=-1});
            db.Operatori.AddRange(new(){Id=100,Nome="Cassiere",PersonId=-1,Abilitato=true},new(){Id=101,Nome="Senza permessi",PersonId=-1,Abilitato=true});
            db.Permessi.AddRange(new(){OperatoreId=100,PostazioneId=100},new(){OperatoreId=100,PostazioneId=200});
            db.Giornate.Add(new(){Id=100,Aperta=true,DataInizio=DateTime.Today,DataFine=DateTime.MaxValue});
            for(var i=100;i<=101;i++)
            {
                db.People.Add(new(){Id=i,FirstName="Mario",SurName="Rossi",UniqueParam="LOCK"+i,Natoil=19900101,Soci=[new(){NumeroSocio="S"+i,Tessere=[new(){NumeroTessera="T"+i,Scadenza=20991231,Abilitato=true}]}]});
                db.Schede.Add(new(){Id=i,PersonId=i,Posizione="P"+i,NumeroTessera="T"+i,Nome="Mario",Cognome="Rossi",CheckoutTime=DateTime.MaxValue});
            }
            db.SchedeConto.Add(new(){Id=100,SchedaId=100,DescSettore="Bar",DescPostazione="Cassa A",VoiceDesc="Caffè",VoicePrice=5,Pagato=false,DataOra=DateTime.Now});
            db.Settori.Add(new(){Id=110,Nome="Bar",TipoSettoreId=-2});db.Tariffe.Add(new(){Id=110,Nome="Acqua",Label="Acqua",Prezzo=2.50m});
            db.Listini.Add(new(){Id=110,SettoreId=110,TariffaId=110});db.Reparti.AddRange(new(){PostazioneId=100,SettoreId=110},new(){PostazioneId=200,SettoreId=110});
            await db.SaveChangesAsync();
        }
        var a=new CassaDatabaseRepository(()=>new AppDbContext(options));var b=new CassaDatabaseRepository(()=>new AppDbContext(options));
        CassaRequest Request(CassaAction action,int station=100)=>new(){Action=action,PostazioneId=station,Posizione="P100",SchedaId=100,ContoId=100,ListinoId=110,Pagato=true,Grb1=123};
        var stale=await b.Execute(100,Request(CassaAction.OpenSheet,200));
        var prepared=await b.Execute(100,Request(CassaAction.PrepareUscita,200));
        var lockRequest=Request(CassaAction.SetBloccoScheda);lockRequest.BloccoScheda=true;
        Check((await a.Execute(100,lockRequest)).Scheda?.Blocco==true,"Postazione A blocca la scheda");
        Check((await a.Execute(100,lockRequest)).Scheda?.Blocco==true,"Riprova blocco non sblocca accidentalmente");
        Check(!stale.Scheda!.Blocco&&(await b.Execute(100,Request(CassaAction.OpenSheet,200))).Scheda?.Blocco==true,"Blocco persistito visibile da altra postazione con scheda già aperta");
        foreach(var action in new[]{CassaAction.AddCharge,CassaAction.SetPagamento,CassaAction.StornaConto,CassaAction.SaveGuardaroba})
            await Reject(()=>b.Execute(100,Request(action,200)),"bloccata");
        var exit=Request(CassaAction.RegisterUscita,200);exit.VersioneConto=prepared.Uscita!.VersioneConto;exit.UscitaId=Guid.NewGuid();
        await Reject(()=>b.Execute(100,exit),"bloccata");
        await using(var db=new AppDbContext(options))
        {
            Check(await db.SchedeConto.CountAsync()==1&&!(await db.SchedeConto.SingleAsync()).Pagato,"Richieste bloccate non modificano il conto");
            Check((await db.Schede.SingleAsync(x=>x.Id==100)).Grb1==0&&!await db.ContabilitaMovimenti.AnyAsync(),"Guardaroba e contabilità invariati");
            Check((await db.Tessere.SingleAsync(x=>x.NumeroTessera=="T100")).Abilitato,"Blocco scheda indipendente dalla tessera");
        }
        var other=Request(CassaAction.AddCharge,200);other.Posizione="P101";other.SchedaId=101;
        Check((await b.Execute(100,other)).Conto.Length==1,"Il blocco non coinvolge le altre schede");
        var unlock=Request(CassaAction.SetBloccoScheda,200);unlock.BloccoScheda=false;
        await Reject(()=>b.Execute(101,unlock),"permesso");
        var wrong=Request(CassaAction.SetBloccoScheda,200);wrong.SchedaId=101;
        await Reject(()=>b.Execute(100,wrong),"cambiata");
        Check((await b.Execute(100,unlock)).Scheda?.Blocco==false,"Altra cassa autorizzata può sbloccare");
        Check((await b.Execute(100,unlock)).Scheda?.Blocco==false,"Riprova sblocco idempotente");
        Check((await a.Execute(100,Request(CassaAction.AddCharge))).Conto.Length==2,"Dopo sblocco inserimenti nuovamente consentiti");
        var json=JsonSerializer.Serialize(new DatabaseHostRequest{Cassa=lockRequest},DatabaseProtocolJsonContext.Default.DatabaseHostRequest);
        Check(JsonSerializer.Deserialize(json,DatabaseProtocolJsonContext.Default.DatabaseHostRequest)?.Cassa is {Action:CassaAction.SetBloccoScheda,BloccoScheda:true},"Protocollo AOT mantiene operazione e stato esplicito");
        await using(var db=new AppDbContext(options)){(await db.Schede.SingleAsync(x=>x.Id==100)).CheckoutTime=DateTime.Now;await db.SaveChangesAsync();}
        await Reject(()=>a.Execute(100,lockRequest),"chiusa");
    }
    private static void Pump(Func<bool> done){var until=DateTime.UtcNow.AddSeconds(15);while(!done()){Dispatcher.UIThread.RunJobs();Thread.Sleep(1);if(DateTime.UtcNow>until)throw new TimeoutException("UI blocco scheda");}}
    private static void Command(ReactiveCommand<RxVoid,RxVoid> command){var done=false;Exception? error=null;using var sub=command.Execute().SubscribePrimitives(_=>{},e=>{error=e;done=true;},()=>done=true);Pump(()=>done);if(error is not null)throw error;Dispatcher.UIThread.RunJobs();}
    public static void UI()
    {
        var fake=new Fake();var vm=new CassaViewModel(new Screen(),fake,100);var view=new CassaView{ViewModel=vm};var window=new Window{Width=1400,Height=950,Content=view};window.Show();Pump(()=>vm.IsUiReady);
        vm.Posizione="P100";Command(vm.OpenSheetCommand);Check(vm.CanEditSheet&&vm.CanSetBloccoScheda,"Scheda aperta modificabile");
        Command(vm.SetBloccoSchedaCommand);
        Check(vm.Sheet?.Blocco==true&&!vm.CanEditSheet&&vm.BloccoSchedaLabel=="SBLOCCA SCHEDA","Blocco aggiorna stato e pulsante");
        Check(fake.Last is {Action:CassaAction.SetBloccoScheda,SchedaId:100,Posizione:"P100",BloccoScheda:true},"Il client invia identificativo e stato desiderato");
        Check(!view.FindControl<GuardarobaControl>("GuardarobaPanel")!.IsEnabled&&!view.FindControl<Grid>("SheetCharges")!.IsEnabled,"Guardaroba e spese disabilitati");
        Check(view.FindControl<ControlStyles.UiIconButton>("SheetLockButton")!.IsEnabled,"Sblocco resta disponibile");
        using(var frame=window.CaptureRenderedFrame()){var path=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"..","..","..","previews","scheda-bloccata.png"));Directory.CreateDirectory(Path.GetDirectoryName(path)!);frame!.Save(path,Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);}
        var before=fake.Calls;var done=false;using(var sub=vm.AddChargeCommand.Execute(vm.VociSpesa[0]).SubscribePrimitives(_=>{},e=>throw e,()=>done=true))Pump(()=>done);
        Check(fake.Calls==before,"Comando diretto addebito non supera il blocco client");
        var menu=view.FindControl<ControlStyles.UiIconButton>("MenuButton")!;
        Check(menu.IsEnabled&&((System.Windows.Input.ICommand)vm.MenuCommand).CanExecute(null),"Menu disponibile nella scheda principale");
        Command(vm.ApriStoricoFidelityCommand);
        Check(vm.IsStoricoFidelity&&!menu.IsEnabled&&!((System.Windows.Input.ICommand)vm.MenuCommand).CanExecute(null),"Menu disattivato nello storico Fidelity");
        Command(vm.BackCommand);
        Check(vm.IsPostazione&&vm.Sheet?.Blocco==true&&vm.CanSetBloccoScheda&&view.FindControl<ControlStyles.UiIconButton>("SheetLockButton")!.IsEnabled,"Ritorno da Fidelity riabilita Sblocca sulla scheda bloccata");
        Check(menu.IsEnabled,"Ritorno alla scheda riabilita Menu");
        var page=typeof(CassaViewModel).GetMethod("SetPage",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!;
        for(var i=1;i<=6;i++)
        {
            page.Invoke(vm,[i]);Dispatcher.UIThread.RunJobs();
            Check(!menu.IsEnabled&&!vm.CanBackToMenu&&!((System.Windows.Input.ICommand)vm.MenuCommand).CanExecute(null),"Menu disattivato nella sottovista "+i);
            Command(vm.BackCommand);
            Check(vm.IsPostazione&&menu.IsEnabled&&view.FindControl<ControlStyles.UiIconButton>("SheetLockButton")!.IsEnabled,"Ritorno dalla sottovista riabilita Menu e Sblocca "+i);
        }
        Command(vm.SetBloccoSchedaCommand);Check(vm.CanEditSheet&&vm.Sheet?.Blocco==false&&vm.BloccoSchedaLabel=="BLOCCA SCHEDA","Sblocco riabilita modifiche");
        Check(view.FindControl<GuardarobaControl>("GuardarobaPanel")!.IsEnabled&&view.FindControl<Grid>("SheetCharges")!.IsEnabled,"Pannelli riabilitati dopo sblocco");window.Close();Dispatcher.UIThread.RunJobs();
    }
    private sealed class Screen:ReactiveObject,IScreen{public RoutingState Router{get;}=new();}
    private sealed class Fake:ICassaRepository
    {
        public bool Locked;public int Calls;public CassaRequest? Last;
        public Task<CassaResult> Execute(CassaRequest r,CancellationToken ct=default)
        {
            Calls++;Last=r;if(r.Action==CassaAction.SetBloccoScheda)Locked=r.BloccoScheda;
            return Task.FromResult(r.Action is CassaAction.OpenSheet or CassaAction.SetBloccoScheda?new CassaResult{Scheda=new(){Id=100,PersonId=100,Posizione="P100",Nome="Mario",Cognome="Rossi",NumeroTessera="T100",Blocco=Locked},Conto=[new(){Id=100,VoiceDesc="Caffè",VoicePrice=5}],VociSpesa=[new(){ListinoId=110,Settore="Bar",Descrizione="Acqua",Prezzo=2.50m}]}:new CassaResult{PostazioneName="Cassa prova"});
        }
    }
}
