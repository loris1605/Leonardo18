using AppServices.Database;
using Azure.Identity;
using Microsoft.Data.SqlClient;
using System.Security.Cryptography;
using System.Text.Json;
namespace Leonardo18.DatabaseHost.Mirror;

public interface IMirrorOperations
{
 bool HasAccount {get;}
 Task ConnectAsync(Func<string,string,CancellationToken,Task> callback,CancellationToken ct);
 Task<MirrorSnapshot> CaptureAsync(string source,CancellationToken ct);
 Task ApplyAsync(MirrorSnapshot snapshot,CancellationToken ct);
}
public sealed class MirrorOperations(string folder):IMirrorOperations
{
 private readonly MirrorIdentity _identity=new(folder);
 private readonly MirrorSnapshotEngine _engine=new();
 public bool HasAccount=>_identity.HasAccount;
 public Task ConnectAsync(Func<string,string,CancellationToken,Task> callback,CancellationToken ct)=>_identity.ConnectAsync((code,token)=>callback(code.VerificationUri.ToString(),code.UserCode,token),ct);
 public Task<MirrorSnapshot> CaptureAsync(string source,CancellationToken ct)=>_engine.CaptureAsync(source,ct);
 public async Task ApplyAsync(MirrorSnapshot snapshot,CancellationToken ct)
 {
  var token=await _identity.TokenAsync(ct);
  var b=new SqlConnectionStringBuilder{DataSource="tcp:"+MirrorSnapshotEngine.Server+",1433",InitialCatalog=MirrorSnapshotEngine.Database,Encrypt=true,TrustServerCertificate=false,Pooling=false,ConnectTimeout=30,ApplicationName="Leonardo18 Mirror"};
  await using var remote=new SqlConnection(b.ConnectionString){AccessToken=token};
  await remote.OpenAsync(ct);using var cancel=ct.Register(()=>remote.Close());
  await _engine.ApplyAsync(snapshot,remote,ct);
 }
}
public sealed class MirrorSavedState
{
 public bool Enabled {get;set;}
 public string SourceKey {get;set;}="";
 public string LastFingerprint {get;set;}="";
 public DateTime? LastCompletedUtc {get;set;}
 public DateTime? LastSnapshotUtc {get;set;}
 public int TableCount {get;set;}
 public long RowCount {get;set;}
}
public static class MirrorTiming
{
 public static readonly TimeSpan MinimumInterval=TimeSpan.FromMinutes(5);
 public static TimeSpan RetryDelay(int failures)=>TimeSpan.FromMinutes(failures switch{<=1=>1,2=>2,3=>5,_=>15});
 public static bool CanUpload(DateTime now,DateTime? last,bool force)=>force||last is null||now-last>=MinimumInterval;
}
public sealed class MirrorCoordinator:IAsyncDisposable
{
 public static string DefaultFolder=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Leonardo18","Mirror");
 private readonly string _folder;
 private readonly IMirrorOperations _operations;
 private readonly bool _automatic;
 private readonly object _stateLock=new();
 private readonly SemaphoreSlim _run=new(1,1),_wake=new(0,1);
 private readonly CancellationTokenSource _stop=new();
 private MirrorSavedState _saved;
 private MirrorStatus _status;
 private string? _source;
 private Task? _loop,_connecting;
 private int _force,_failures;
 private bool _disposed;
 public MirrorCoordinator(string? folder=null,IMirrorOperations? operations=null,bool automatic=true)
 {
  _folder=folder??DefaultFolder;_operations=operations??new MirrorOperations(_folder);_automatic=automatic;
  try{_saved=JsonSerializer.Deserialize<MirrorSavedState>(File.ReadAllText(Path.Combine(_folder,"state.json")))??new();}catch(Exception ex)when(ex is IOException or JsonException or UnauthorizedAccessException){_saved=new();}
  _status=new(){Enabled=_saved.Enabled,NeedsAuthentication=_saved.Enabled&&!_operations.HasAccount,LastCompletedUtc=_saved.LastCompletedUtc,TableCount=_saved.TableCount,RowCount=_saved.RowCount,Message=_saved.Enabled?"Mirror collegato · verifica aggiornamenti in attesa":"Mirror non collegato"};
 }
 public MirrorStatus Status(string? source=null)
 {
  lock(_stateLock)
  {
   if(source is not null&&!AcceptsSource(source))return new(){Message="Mirror disponibile solo per il database locale Ulisse configurato."};
   return new(){ErrorCode=_status.ErrorCode,Enabled=_status.Enabled,Busy=_status.Busy,NeedsAuthentication=_status.NeedsAuthentication,Pending=_status.Pending,Message=_status.Message,AuthenticationCode=_status.AuthenticationCode,AuthenticationUrl=_status.AuthenticationUrl,LastCompletedUtc=_status.LastCompletedUtc,NextAttemptUtc=_status.NextAttemptUtc,TableCount=_status.TableCount,RowCount=_status.RowCount};
  }
 }
 private bool AcceptsSource(string source)
 {
  try{var key=MirrorSnapshotEngine.SourceKey(source);return string.IsNullOrEmpty(_saved.SourceKey)||key==_saved.SourceKey;}catch(ArgumentException){return false;}catch(InvalidOperationException){return false;}
 }
 public void Observe(string source)
 {
  lock(_stateLock)
  {
   if(_disposed||!_saved.Enabled||!AcceptsSource(source))return;
   _source=source;
   if(_automatic&&_loop is null)_loop=Task.Run(LoopAsync);
  }
 }
 public async Task<MirrorStatus> BeginConnectAsync(string source)
 {
  var announced=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
  lock(_stateLock)
  {
   if(!AcceptsSource(source))throw new InvalidOperationException("Origine non valida per il mirror.");
   if(_connecting is {IsCompleted:false})return Status(source);
   _status.Busy=true;_status.Message="Collegamento Microsoft in corso...";
   _connecting=Task.Run(async()=>
   {
    try
    {
     await ConnectAsync(source,(url,code,_)=>{lock(_stateLock){_status.AuthenticationUrl=url;_status.AuthenticationCode=code;_status.NeedsAuthentication=true;_status.Message="Completa l'accesso Microsoft con il codice indicato.";}announced.TrySetResult();return Task.CompletedTask;},_stop.Token);
    }
    catch(OperationCanceledException){}
    catch(Exception){lock(_stateLock){_status.Busy=false;_status.NeedsAuthentication=true;_status.Message="Accesso Microsoft non completato. Premi COLLEGA MIRROR per riprovare.";_status.AuthenticationCode="";_status.AuthenticationUrl="";}}
    finally{announced.TrySetResult();}
   });
  }
  await Task.WhenAny(announced.Task,Task.Delay(TimeSpan.FromSeconds(8),_stop.Token));
  return Status(source);
 }
 public async Task ConnectAsync(string source,Func<string,string,CancellationToken,Task> callback,CancellationToken ct=default)
 {
  if(!AcceptsSource(source))throw new InvalidOperationException("Origine non valida per il mirror.");
  await _operations.ConnectAsync(callback,ct);
  lock(_stateLock)
  {
   _saved.Enabled=true;_saved.SourceKey=MirrorSnapshotEngine.SourceKey(source);Save();
   _source=source;_status.Enabled=true;_status.Busy=false;_status.NeedsAuthentication=false;_status.AuthenticationCode="";_status.AuthenticationUrl="";_status.Message="Mirror collegato · prima sincronizzazione in attesa";_status.NextAttemptUtc=null;
  }
  Observe(source);RequestSync(source);
 }
 public MirrorStatus RequestSync(string source)
 {
  if(!AcceptsSource(source))throw new InvalidOperationException("Origine non valida per il mirror.");
  Observe(source);
  lock(_stateLock)
  {
   if(!_saved.Enabled)return Status(source);
   Interlocked.Exchange(ref _force,1);_status.Pending=true;_status.NextAttemptUtc=null;
   if(!_status.Busy)_status.Message="Sincronizzazione mirror richiesta";
  }
  try{_wake.Release();}catch(SemaphoreFullException){}
  return Status(source);
 }
 private async Task LoopAsync()
 {
  try
  {
   while(!_stop.IsCancellationRequested)
   {
    await RunOnceAsync(Interlocked.Exchange(ref _force,0)==1,_stop.Token);
    await _wake.WaitAsync(TimeSpan.FromSeconds(60),_stop.Token);
   }
  }
  catch(OperationCanceledException){}
 }
 public async Task RunOnceAsync(bool force=false,CancellationToken ct=default)
 {
  if(!await _run.WaitAsync(0,ct))return;
  FileStream? processLock=null;
  try
  {
   string source;
   lock(_stateLock)
   {
    if(!_saved.Enabled||_source is null||_connecting is {IsCompleted:false}||!force&&_status.NextAttemptUtc>DateTime.UtcNow)return;
    source=_source;
    if(!_operations.HasAccount){_status.NeedsAuthentication=true;_status.Message="Rinnova il collegamento Microsoft dal menu.";return;}
    _status.Busy=true;_status.Message="Verifica aggiornamenti locali...";
   }
   Directory.CreateDirectory(_folder);
   try{processLock=new FileStream(Path.Combine(_folder,"sync.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);}
   catch(IOException){lock(_stateLock){_status.Message="Un'altra istanza di Leonardo18 sta verificando il mirror.";}return;}
   var snapshot=await _operations.CaptureAsync(source,ct);
   lock(_stateLock)
   {
    if(snapshot.SourceKey!=_saved.SourceKey)throw new InvalidOperationException("Origine mirror diversa.");
    if(!force&&snapshot.Fingerprint==_saved.LastFingerprint){_status.Pending=false;_status.Message="Mirror aggiornato";_status.NeedsAuthentication=false;_status.NextAttemptUtc=null;_failures=0;return;}
    _status.Pending=true;
    if(!MirrorTiming.CanUpload(DateTime.UtcNow,_saved.LastCompletedUtc,force)){_status.Message="Modifiche in attesa della prossima sincronizzazione";_status.NextAttemptUtc=_saved.LastCompletedUtc+MirrorTiming.MinimumInterval;return;}
    _status.Message="Sincronizzazione mirror in corso...";
   }
   await _operations.ApplyAsync(snapshot,ct);
   lock(_stateLock)
   {
    _saved.LastCompletedUtc=DateTime.UtcNow;_saved.LastSnapshotUtc=snapshot.CapturedUtc;_saved.LastFingerprint=snapshot.Fingerprint;_saved.TableCount=snapshot.Tables.Count;_saved.RowCount=snapshot.RowCount;Save();
    _status.LastCompletedUtc=_saved.LastCompletedUtc;_status.TableCount=_saved.TableCount;_status.RowCount=_saved.RowCount;_status.Pending=false;_status.NeedsAuthentication=false;_status.NextAttemptUtc=null;_status.Message="Mirror aggiornato";_status.ErrorCode="";_failures=0;
   }
  }
  catch(OperationCanceledException)when(ct.IsCancellationRequested){throw;}
  catch(Exception ex)
  {
   lock(_stateLock)
   {
    _status.ErrorCode=ex is SqlException sql?"sql_"+sql.Number:ex.GetType().Name+(ex is InvalidOperationException?": "+ex.Message:"");
    _status.Pending=true;_status.NextAttemptUtc=DateTime.UtcNow+MirrorTiming.RetryDelay(++_failures);
    _status.NeedsAuthentication=ex is AuthenticationRequiredException or AuthenticationFailedException or CryptographicException;
    _status.Message=_status.NeedsAuthentication?"Accesso Microsoft da rinnovare · usa COLLEGA MIRROR":ex is SqlException{Number:40615}?"IP non autorizzato da Azure · aggiorna il firewall del mirror":"Mirror non aggiornato · nuovo tentativo automatico. La cassa lavora sul database locale.";
   }
  }
  finally{processLock?.Dispose();lock(_stateLock)_status.Busy=false;_run.Release();}
 }
 private void Save()
 {
  Directory.CreateDirectory(_folder);var file=Path.Combine(_folder,"state.json");var temp=file+"."+Guid.NewGuid().ToString("N")+".tmp";
  File.WriteAllText(temp,JsonSerializer.Serialize(_saved));File.Move(temp,file,true);
 }
 public async ValueTask DisposeAsync()
 {
  lock(_stateLock){if(_disposed)return;_disposed=true;}
  await _stop.CancelAsync();
  try{await Task.WhenAll(new[]{_loop,_connecting}.OfType<Task>()).WaitAsync(TimeSpan.FromSeconds(5));}catch(Exception ex)when(ex is OperationCanceledException or TimeoutException){}
 }
}
