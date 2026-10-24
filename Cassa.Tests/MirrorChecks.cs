using System.Data;
using System.Text.Json;
using AppServices.Database;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Leonardo18.DatabaseHost.Mirror;
using Menu.Core.Repository;
using Menu.ViewModels;
using Menu.Views;
using ReactiveUI;
using ReactiveUI.Primitives;
internal static class MirrorChecks
{
 public static int Count;
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);Count++;}
 public static async Task Background()
 {
  const string source="Server=.;Database=ulisse;Integrated Security=true;TrustServerCertificate=true";
  var folder=Path.Combine(Path.GetTempPath(),"Leonardo18MirrorChecks-"+Guid.NewGuid().ToString("N"));
  var fake=new Operations(MirrorSnapshotEngine.SourceKey(source));
  await using(var coordinator=new MirrorCoordinator(folder,fake,false))
  {
   Check(!coordinator.Status().Enabled,"Nessun invio prima del collegamento");
   coordinator.Observe(source);await coordinator.RunOnceAsync();Check(fake.Captures==0&&fake.Applies==0,"Disabilitato non legge né invia dati");
   await coordinator.ConnectAsync(source,(_,_,_)=>Task.CompletedTask);Check(coordinator.Status().Enabled&&fake.Connections==1,"Collegamento abilita soltanto dopo autenticazione");
   await coordinator.RunOnceAsync();var completed=coordinator.Status().LastCompletedUtc;
   Check(fake.Applies==1&&completed is not null&&!coordinator.Status().Pending,"Prima copia completata");
   await coordinator.RunOnceAsync();Check(fake.Applies==1&&!coordinator.Status().Pending,"Dati identici non riaprono connessione cloud");
   fake.Fingerprint="changed";await coordinator.RunOnceAsync();Check(fake.Applies==1&&coordinator.Status().Pending&&coordinator.Status().NextAttemptUtc>completed,"Modifiche raggruppate rispettando l'intervallo");
   await coordinator.RunOnceAsync(true);Check(fake.Applies==2&&!coordinator.Status().Pending,"Sincronizza subito supera l'attesa minima");
   completed=coordinator.Status().LastCompletedUtc;fake.Fingerprint="offline";fake.Fail=true;
   await coordinator.RunOnceAsync(true);Check(coordinator.Status().Pending&&!coordinator.Status().Busy&&coordinator.Status().LastCompletedUtc==completed,"Errore conserva l'ultima copia confermata");
   var attempts=fake.Applies;await coordinator.RunOnceAsync();Check(fake.Applies==attempts,"Errore applica attesa prima del nuovo tentativo");
   fake.Fail=false;await coordinator.RunOnceAsync(true);Check(!coordinator.Status().Pending&&fake.Applies==attempts+1,"Ripresa dopo disconnessione trasferisce modifiche ancora pendenti");
   fake.HasAccount=false;await coordinator.RunOnceAsync(true);Check(coordinator.Status().NeedsAuthentication&&!coordinator.Status().Busy,"Accesso scaduto non apre finestre e richiede rinnovo");fake.HasAccount=true;
   Check(!coordinator.Status("Server=.;Database=altro;Integrated Security=true").Enabled,"Database diverso non usa il mirror");
  }
  await using(var restarted=new MirrorCoordinator(folder,fake,false))
  {
   restarted.Observe(source);var before=fake.Applies;await restarted.RunOnceAsync();
   Check(restarted.Status().Enabled&&restarted.Status().LastCompletedUtc is not null&&fake.Applies==before,"Riavvio conserva collegamento e impronta senza invio duplicato");
   fake.Fingerprint="after-restart";await restarted.RunOnceAsync(true);Check(fake.Applies==before+1,"Dopo riavvio le modifiche nuove sono copiate");
  }
  Check(!MirrorTiming.CanUpload(DateTime.UtcNow,DateTime.UtcNow,false)&&MirrorTiming.CanUpload(DateTime.UtcNow,DateTime.UtcNow,true),"Intervallo rispettato e comando manuale consentito");
  Check(MirrorTiming.RetryDelay(1)==TimeSpan.FromMinutes(1)&&MirrorTiming.RetryDelay(2)==TimeSpan.FromMinutes(2)&&MirrorTiming.RetryDelay(3)==TimeSpan.FromMinutes(5)&&MirrorTiming.RetryDelay(50)==TimeSpan.FromMinutes(15),"Attesa progressiva limitata a quindici minuti");
  var table=new DataTable();table.Columns.Add("Id",typeof(int));table.Columns.Add("Text",typeof(string));table.Rows.Add(1,"A|B");table.Rows.Add(2,DBNull.Value);var hash=MirrorSnapshotEngine.Digest(table);
  var reordered=table.Clone();reordered.ImportRow(table.Rows[1]);reordered.ImportRow(table.Rows[0]);Check(MirrorSnapshotEngine.Digest(reordered)==hash,"Verifica indipendente dall'ordine delle righe");
  reordered.Rows[0][1]="";Check(MirrorSnapshotEngine.Digest(reordered)!=hash,"La verifica distingue null e stringa vuota");
  reordered.Rows[0][1]=DBNull.Value;reordered.Rows[1][1]="diverso";Check(MirrorSnapshotEngine.Digest(reordered)!=hash,"La verifica rileva differenze a parità di numero righe");
  try{MirrorSnapshotEngine.SourceKey("Server=x.database.windows.net;Database=ulisse;Integrated Security=true");throw new Exception("Origine cloud accettata");}catch(InvalidOperationException){Count++;}
  var json=JsonSerializer.Serialize(new DatabaseHostResponse{Success=true,Mirror=new(){Enabled=true,Pending=true,LastCompletedUtc=DateTime.UtcNow,TableCount=35,RowCount=112}},DatabaseProtocolJsonContext.Default.DatabaseHostResponse);
  Check(JsonSerializer.Deserialize(json,DatabaseProtocolJsonContext.Default.DatabaseHostResponse)?.Mirror is {Enabled:true,Pending:true,TableCount:35,RowCount:112},"Protocollo Native AOT conserva lo stato mirror");
  // Delete only files created by these tests, never a database or application configuration.
  foreach(var file in Directory.GetFiles(folder))File.Delete(file);Directory.Delete(folder);
 }
 private sealed class Operations(string sourceKey):IMirrorOperations
 {
  public bool HasAccount{get;set;}=true;public int Connections,Captures,Applies;public bool Fail;public string Fingerprint="initial";
  public Task ConnectAsync(Func<string,string,CancellationToken,Task> callback,CancellationToken ct){Connections++;return Task.CompletedTask;}
  public Task<MirrorSnapshot> CaptureAsync(string source,CancellationToken ct){Captures++;return Task.FromResult(new MirrorSnapshot(sourceKey,DateTime.UtcNow,[],new(),new(),Fingerprint));}
  public Task ApplyAsync(MirrorSnapshot snapshot,CancellationToken ct){Applies++;if(Fail)throw new IOException("offline simulato");return Task.CompletedTask;}
 }
 static void Pump(Func<bool> done){var until=DateTime.UtcNow.AddSeconds(15);while(!done()){Dispatcher.UIThread.RunJobs();Thread.Sleep(1);if(DateTime.UtcNow>until)throw new TimeoutException("UI mirror");}}
 static void Command(ReactiveCommand<RxVoid,RxVoid> cmd){bool done=false;using var sub=cmd.Execute().SubscribePrimitives(_=>{},ex=>throw ex,()=>done=true);Pump(()=>done);Dispatcher.UIThread.RunJobs();}
 public static void UI()
 {
  Models.Entity.Global.GlobalValuesC.MySetting=new(){IDOPERATORE=100,NOMEOPERATORE="Operatore prova",POSTAZIONI=[]};
  var client=new Client();var vm=new MenuViewModel(new MenuFake(),new Screen(),client);var view=new MenuView{ViewModel=vm};var window=new Window{Width=1100,Height=900,Content=view};window.Show();Pump(()=>vm.IsMenuReady&&vm.MirrorMessage.Contains("aggiornato"));
  Check(vm.MirrorVisible&&vm.MirrorMessage.Contains("ultima copia"),"Menu mostra stato e ultima sincronizzazione");
  Command(vm.SincronizzaMirrorCommand);Check(client.Last=="mirror-sync"&&vm.MirrorMessage.Contains("richiesta"),"Pulsante richiede una copia senza cambiare giornata o connessione");
  Command(vm.CollegaMirrorCommand);Check(client.Last=="mirror-connect"&&vm.HasMirrorCode&&vm.MirrorCode.Contains("TESTCODE"),"Menu mostra il codice Microsoft per il rinnovo");
  Check(MenuViewModel.IsMicrosoftDeviceUrl("https://microsoft.com/devicelogin")&&!MenuViewModel.IsMicrosoftDeviceUrl("https://evil.example/device")&&!MenuViewModel.IsMicrosoftDeviceUrl("https://microsoft.com.evil.example/devicelogin")&&!MenuViewModel.IsMicrosoftDeviceUrl("http://microsoft.com/devicelogin"),"Il pulsante apre soltanto l'indirizzo Microsoft previsto");
  var folder=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"..","..","..","previews"));Directory.CreateDirectory(folder);using(var frame=window.CaptureRenderedFrame())frame!.Save(Path.Combine(folder,"menu-mirror.png"),Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
  window.Width=900;window.Height=650;Dispatcher.UIThread.RunJobs();using(var frame=window.CaptureRenderedFrame())frame!.Save(Path.Combine(folder,"menu-mirror-900.png"),Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
  client.Fail=true;Command(vm.SincronizzaMirrorCommand);Check(vm.IsMenuReady&&vm.MirrorMessage.Contains("locale resta operativo"),"Errore mirror non blocca il menu della cassa");
  window.Close();Dispatcher.UIThread.RunJobs();
 }
 private sealed class Client:IMirrorClient
 {
  public string Last="";public bool Fail;
  public Task<MirrorStatus> ExecuteAsync(string operation,int id,CancellationToken ct=default)
  {
   Last=operation;if(Fail)throw new IOException("offline");
   return Task.FromResult(new MirrorStatus{Enabled=true,LastCompletedUtc=DateTime.UtcNow,Message=operation=="mirror-sync"?"Sincronizzazione richiesta":"Mirror aggiornato",AuthenticationCode=operation=="mirror-connect"?"TESTCODE":"",AuthenticationUrl="https://microsoft.com/devicelogin"});
  }
 }
 private sealed class MenuFake:IMenuRepository
 {
  public Task<MenuState> Execute(int id,string operation,CancellationToken ct=default)=>Task.FromResult(new MenuState{Stations=[new(){Id=1,Name="Amministrazione",TypeId=1},new(){Id=2,Name="Cassa",TypeId=2}]});
  public Task<MenuState> OpenDay(int id,MenuAperturaRequest request,CancellationToken ct=default)=>Execute(id,"menu-load",ct);
 }
 private sealed class Screen:ReactiveObject,IScreen{public RoutingState Router{get;}=new();}
}
