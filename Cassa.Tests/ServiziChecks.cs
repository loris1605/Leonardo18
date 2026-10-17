using System.Text.Json;
using AppServices.Database;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Models.Context;
using Models.Tables;
using ReactiveUI;
using ReactiveUI.Primitives;
using Servizi.Core.Repository;
using Servizi.ViewModels;
using Servizi.Views;
using Leonardo18.DatabaseHost.Servizi;

internal static class ServiziChecks
{
    public static int Count;
    private static void Check(bool b,string message){if(!b)throw new Exception("Servizi: "+message);Count++;}
    private static async Task Reject(Func<Task> work,string message)
    {try{await work();throw new Exception("Operazione accettata: "+message);}catch(InvalidOperationException ex){Check(ex.Message.Contains(message,StringComparison.OrdinalIgnoreCase),ex.Message);}}
    public static async Task Database()
    {
        await using var connection=new SqliteConnection("Data Source=:memory:");await connection.OpenAsync();
        var options=new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        await using(var db=new AppDbContext(options)){
            await db.Database.EnsureCreatedAsync();
            db.Postazioni.Add(new(){Id=200,Nome="Amministrazione",TipoPostazioneId=1,TipoRientroId=-1});
            db.Operatori.AddRange(new Operatore{Id=200,Nome="Admin",PersonId=-1,Abilitato=true},new Operatore{Id=201,Nome="Senza permesso",PersonId=-1,Abilitato=true},new Operatore{Id=202,Nome="Disabilitato",PersonId=-1,Abilitato=false});
            db.Permessi.AddRange(new Permesso{OperatoreId=200,PostazioneId=200},new Permesso{OperatoreId=202,PostazioneId=200});
            await db.SaveChangesAsync();
        }
        var repo=new ServiziDatabaseRepository(()=>new AppDbContext(options));
        await Reject(()=>repo.Execute(201,new()),"amministratore");await Reject(()=>repo.Execute(202,new()),"amministratore");
        await Reject(()=>repo.Execute(200,new(){Action=(ServiziAction)99}),"non supportata");
        var r=new ServiziRecord{Nome=" Mensile ",NumeroIngressi=10,DurataGiorni=30,Prezzo=99.50m,Attivo=true};
        var result=await repo.Execute(200,new(){Action=ServiziAction.Add,Record=r});var id=result.SelectedId;
        Check(id>0&&result.Records.Single(x=>x.Id==id).Nome=="Mensile","Creazione e normalizzazione");
        var read=await repo.Execute(200,new(){Action=ServiziAction.Read,RecordId=id});
        Check(read.Records[0].DurataGiorni==30&&read.Records[0].NumeroIngressi==10&&read.Records[0].Prezzo==99.50m&&read.Records[0].Attivo,"Tutti i campi persistiti");
        r.Nome="mensile";await Reject(()=>repo.Execute(200,new(){Action=ServiziAction.Add,Record=r}),"già");
        foreach(var bad in new[]{new ServiziRecord{Nome="A"},new ServiziRecord{Nome=new string('a',26)}})
            await Reject(()=>repo.Execute(200,new(){Action=ServiziAction.Add,Record=bad}),"nome");
        await Reject(()=>repo.Execute(200,new(){Action=ServiziAction.Add,Record=new(){Nome="Negativo",NumeroIngressi=-1}}),"negativi");
        await Reject(()=>repo.Execute(200,new(){Action=ServiziAction.Add,Record=new(){Nome="Negativo",DurataGiorni=-1}}),"negativi");
        foreach(var price in new[]{-1m,1.001m,10000000000000000m})
            await Reject(()=>repo.Execute(200,new(){Action=ServiziAction.Add,Record=new(){Nome="Prezzo",Prezzo=price}}),"prezzo");
        await Reject(()=>repo.Execute(200,new(){Action=ServiziAction.Read,RecordId=-1}),"Identificativo");
        await Reject(()=>repo.Execute(200,new(){Action=ServiziAction.Update,RecordId=id,Record=new(){Id=id+1,Nome="Alterato"}}),"Identificativo");
        await Reject(()=>repo.Execute(200,new(){Action=ServiziAction.Read,RecordId=99999}),"disponibile");
        r.Id=id;r.Nome="Mensile nuovo";r.Attivo=false;r.Prezzo=101;r.DurataGiorni=31;r.NumeroIngressi=11;
        result=await repo.Execute(200,new(){Action=ServiziAction.Update,RecordId=id,Record=r});
        Check(result.Records.Single(x=>x.Id==id) is {Attivo:false,DurataGiorni:31,NumeroIngressi:11,Prezzo:101},"Modifica completa");
        await using(var db=new AppDbContext(options)){
            db.Abbonamenti.Add(new(){Nome="Utilizzato",TipoAbbonamentoId=id,PersonId1=-1,DataInizio=DateTime.Today,DataFine=DateTime.Today.AddDays(30)});await db.SaveChangesAsync();
        }
        await Reject(()=>repo.Execute(200,new(){Action=ServiziAction.Delete,RecordId=id}),"utilizzato");
        result=await repo.Execute(200,new(){Action=ServiziAction.Add,Record=new(){Nome="Eliminabile"}});var remove=result.SelectedId;
        result=await repo.Execute(200,new(){Action=ServiziAction.Delete,RecordId=remove});
        Check(!result.Records.Any(x=>x.Id==remove)&&result.Records.Any(x=>x.Id==id),"Eliminazione selettiva");
        var client=new Client(); using var cancel=new CancellationTokenSource();
        await new ServiziRepository(client,200).Execute(new(){Action=ServiziAction.Load},cancel.Token);
        Check(client.Request is {Operation:"servizi",OperatorId:200,Servizi.Action:ServiziAction.Load}&&client.Token==cancel.Token,"Client inoltra richiesta e annullamento");
        var json=JsonSerializer.Serialize(new DatabaseHostRequest{Operation="servizi",OperatorId=200,Servizi=new(){Action=ServiziAction.Update,RecordId=id,Record=r}},DatabaseProtocolJsonContext.Default.DatabaseHostRequest);
        var decoded=JsonSerializer.Deserialize(json,DatabaseProtocolJsonContext.Default.DatabaseHostRequest);
        Check(decoded?.Servizi?.Record?.DurataGiorni==31&&decoded.OperatorId==200,"Protocollo richiesta generato");
        json=JsonSerializer.Serialize(new DatabaseHostResponse{Success=true,Servizi=result},DatabaseProtocolJsonContext.Default.DatabaseHostResponse);
        Check(JsonSerializer.Deserialize(json,DatabaseProtocolJsonContext.Default.DatabaseHostResponse)?.Servizi?.Records.Single().Nome=="Mensile nuovo","Protocollo risposta generato");
    }
    private static void Wait(Task task){var until=DateTime.UtcNow.AddSeconds(20);while(!task.IsCompleted){Dispatcher.UIThread.RunJobs();Thread.Sleep(5);if(DateTime.UtcNow>until)throw new TimeoutException();}task.GetAwaiter().GetResult();Dispatcher.UIThread.RunJobs();}
    private static void Command(ReactiveCommand<RxVoid,RxVoid> command){var done=new TaskCompletionSource();using var sub=command.Execute().SubscribePrimitives(_=>{},ex=>done.TrySetException(ex),()=>done.TrySetResult());Wait(done.Task);}
    private static void Shot(Window w,string name){Dispatcher.UIThread.RunJobs();using var frame=w.CaptureRenderedFrame();var dir=Path.Combine(AppContext.BaseDirectory,"..","..","..","previews");Directory.CreateDirectory(dir);frame!.Save(Path.Combine(dir,name+".png"),Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);}
    public static void UI()
    {
        Models.Entity.Global.GlobalValuesC.MySetting=new(){IDOPERATORE=200};
        foreach(var admin in new[]{true,false}){
            var menu=new Menu.ViewModels.MenuViewModel(new MenuRepository(admin));using var activation=menu.Activator.Activate();Dispatcher.UIThread.RunJobs();
            var destination="";using var navigation=menu.Navigation.SubscribePrimitives(x=>destination=x);Command(menu.ServiziCommand);
            Check(admin?destination=="servizi":destination=="","Menu rispetta il permesso amministratore");
        }
        Models.Entity.Global.GlobalValuesC.MySetting=null;
        var fake=new Fake();var vm=new ServiziViewModel(null,fake);var view=new ServiziView{ViewModel=vm};
        var window=new Window{Width=1400,Height=900,Content=view};window.Show();Dispatcher.UIThread.RunJobs();
        Check(vm.Records.Count==2&&vm.GroupEnabled,"Caricamento attivazione");
        vm.SearchText="mens";Check(vm.Records.Count==1,"Filtro");vm.SearchText="";vm.SortBy="Prezzo";Check(vm.Records[0].Nome=="Mensile","Ordine numerico");
        Shot(window,"servizi-elenco");Command(vm.AddCommand);Check(vm.HasEditor&&vm.IsEditable,"Nuovo editor");
        vm.Nome="Settimanale";vm.NumeroIngressi=3;vm.DurataGiorni=7;vm.Prezzo=25;vm.Attivo=true;Shot(window,"servizi-nuovo");Command(vm.SaveCommand);
        Check(!vm.HasEditor&&vm.Records.Count==3&&vm.Selected?.Nome=="Settimanale","Inserimento e selezione");
        Command(vm.UpdCommand);vm.Nome="";Command(vm.SaveCommand);Check(vm.HasEditor&&vm.InfoLabel.Contains("Verifica"),"Validazione mantiene editor");
        vm.Nome="Settimanale aggiornato";vm.Prezzo=26;Command(vm.SaveCommand);Check(vm.Selected?.Prezzo==26,"Modifica");
        Command(vm.DelCommand);Check(vm.HasEditor&&!vm.IsEditable,"Conferma eliminazione sola lettura");Command(vm.CancelCommand);Check(vm.Records.Count==3,"Annullamento senza scritture");
        Command(vm.DelCommand);Command(vm.SaveCommand);Check(vm.Records.Count==2&&!vm.HasEditor,"Eliminazione UI");
        window.Width=900;window.Height=760;Command(vm.AddCommand);Dispatcher.UIThread.RunJobs();Shot(window,"servizi-compatto");
        Check(!view.FindControl<Grid>("ListPanel")!.IsVisible&&Grid.GetColumn(view.FindControl<Border>("EditorPanel")!)==0,"Layout compatto");
        Command(vm.CancelCommand);var navigated=false;using var back=vm.ServiziToMenu.SubscribePrimitives(_=>navigated=true);Command(vm.MenuCommand);Check(navigated,"Ritorno menu");window.Close();
        var pending=new Fake{Pending=new()};var closing=new ServiziViewModel(null,pending);var lateWindow=new Window{Content=new ServiziView{ViewModel=closing}};lateWindow.Show();Dispatcher.UIThread.RunJobs();lateWindow.Close();
        pending.Pending.SetResult(new(){Records=[new(){Id=100,Nome="Risposta tardiva"}]});Dispatcher.UIThread.RunJobs();Check(closing.Records.Count==0,"Risposte tardive ignorate dopo chiusura");
        var failing=new ServiziViewModel(null,new Fake{Fail=true});var failureWindow=new Window{Content=new ServiziView{ViewModel=failing}};failureWindow.Show();Dispatcher.UIThread.RunJobs();var canReturn=false;using var failureBack=failing.ServiziToMenu.SubscribePrimitives(_=>canReturn=true);Command(failing.MenuCommand);Check(canReturn&&failing.InfoLabel.Contains("Errore"),"Errore caricamento e ritorno menu");failureWindow.Close();
    }
    private sealed class Client:IDatabaseHostClient
    {
        public DatabaseHostRequest? Request;public CancellationToken Token;
        public Task<DatabaseHostResponse> SendAsync(DatabaseHostRequest r,CancellationToken ct=default){Request=r;Token=ct;return Task.FromResult(new DatabaseHostResponse{Success=true,Servizi=new()});}
    }
    private sealed class MenuRepository(bool admin):Menu.Core.Repository.IMenuRepository
    {
        public Task<MenuState> Execute(int id,string operation,CancellationToken ct=default)=>Task.FromResult(new MenuState{Stations=[new(){Id=200,Name="Postazione",TypeId=admin?1:2}]});
    }
    private sealed class Fake:IServiziRepository
    {
        private readonly List<ServiziRecord> rows=[new(){Id=1,Nome="Mensile",Prezzo=99.5m,NumeroIngressi=10,DurataGiorni=30,Attivo=true},new(){Id=2,Nome="Annuale",Prezzo=500,NumeroIngressi=100,DurataGiorni=365,Attivo=false}];
        public TaskCompletionSource<ServiziResult>? Pending;public bool Fail;
        public Task<ServiziResult> Execute(ServiziRequest r,CancellationToken ct=default){
            if(Pending is not null)return Pending.Task;if(Fail)throw new DatabaseHostException("Errore di prova");
            if(r.Action==ServiziAction.Read)return Task.FromResult(new ServiziResult{Records=[rows.Single(x=>x.Id==r.RecordId).Copy()]});
            var id=0;
            if(r.Action==ServiziAction.Add){var n=r.Record!.Copy();n.Id=rows.Max(x=>x.Id)+1;rows.Add(n);id=n.Id;}
            if(r.Action==ServiziAction.Update){rows.RemoveAll(x=>x.Id==r.RecordId);rows.Add(r.Record!.Copy());id=r.RecordId;}
            if(r.Action==ServiziAction.Delete)rows.RemoveAll(x=>x.Id==r.RecordId);
            return Task.FromResult(new ServiziResult{Records=rows.Select(x=>x.Copy()).ToArray(),SelectedId=id});
        }
    }
}