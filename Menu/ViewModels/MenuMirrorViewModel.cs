using AppServices.Database;
using Models.Entity.Global;
using ReactiveUI;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Signals;
using System.Diagnostics;
namespace Menu.ViewModels;

public sealed partial class MenuViewModel
{
 private IMirrorClient? _mirrorClient;
 private MirrorStatus _mirror=new();
 private bool _mirrorRequestBusy;
 private readonly StateSignal<bool> _mirrorReady=new(false),_mirrorConnectReady=new(false),_mirrorBrowserReady=new(false);
 public ReactiveCommand<RxVoid,RxVoid> SincronizzaMirrorCommand {get;private set;}=null!;
 public ReactiveCommand<RxVoid,RxVoid> CollegaMirrorCommand {get;private set;}=null!;
 public ReactiveCommand<RxVoid,RxVoid> ApriAccessoMirrorCommand {get;private set;}=null!;
 public bool MirrorVisible=>_mirrorClient is not null;
 public bool HasMirrorCode=>!string.IsNullOrEmpty(_mirror.AuthenticationCode);
 public string MirrorCode=>"Codice Microsoft: "+_mirror.AuthenticationCode;
 public string MirrorMessage=>_mirror.Message+(_mirror.LastCompletedUtc is {} last?" · ultima copia "+last.ToLocalTime().ToString("dd/MM/yyyy HH:mm"):"");
 public bool MirrorNeedsAttention=>_mirror.NeedsAuthentication||_mirror.Pending;
 public bool MirrorBusy=>_mirror.Busy||_mirrorRequestBusy;
 private void InitializeMirror(IMirrorClient? client)
 {
  _mirrorClient=client;
  SincronizzaMirrorCommand=ReactiveCommand.CreateFromTask(()=>MirrorOperation("mirror-sync"),_mirrorReady);
  CollegaMirrorCommand=ReactiveCommand.CreateFromTask(()=>MirrorOperation("mirror-connect"),_mirrorConnectReady);
  ApriAccessoMirrorCommand=ReactiveCommand.Create(()=>
  {
   if(!IsMicrosoftDeviceUrl(_mirror.AuthenticationUrl))return;
   try{Process.Start(new ProcessStartInfo(_mirror.AuthenticationUrl){UseShellExecute=true});}
   catch{_mirror.Message="Apri microsoft.com/devicelogin e inserisci il codice indicato.";RefreshMirror();}
  },_mirrorBrowserReady);
  foreach(var cmd in new[]{SincronizzaMirrorCommand,CollegaMirrorCommand,ApriAccessoMirrorCommand})_subscriptions.Add(cmd.ThrownExceptions.SubscribePrimitives(_=>{_mirror.Message="Mirror non disponibile. Il database locale resta operativo.";RefreshMirror();}));
 }
 public static bool IsMicrosoftDeviceUrl(string url)=>Uri.TryCreate(url,UriKind.Absolute,out var uri)&&uri.Scheme=="https"&&uri.Query.Length==0&&uri.Fragment.Length==0&&uri.UserInfo.Length==0&&uri.IsDefaultPort&&((uri.Host=="microsoft.com"||uri.Host=="www.microsoft.com")&&uri.AbsolutePath=="/devicelogin"||uri.Host=="login.microsoft.com"&&uri.AbsolutePath=="/device");
 private async Task PollMirror()
 {
  if(_mirrorClient is null)return;
  var ct=Token;
  try
  {
   while(!_isClosing)
   {
    if(!_mirrorRequestBusy)await MirrorOperation("mirror-status");
    if(_isClosing)break;
    await Task.Delay(TimeSpan.FromSeconds(10),ct);
   }
  }
  catch(OperationCanceledException){}
 }
 private async Task MirrorOperation(string operation)
 {
  if(_mirrorClient is null||_isClosing||_mirrorRequestBusy)return;
  _mirrorRequestBusy=true;RefreshMirror();
  try
  {
   var result=await _mirrorClient.ExecuteAsync(operation,GlobalValuesC.MySetting?.IDOPERATORE??-1,Token);
   if(!_isClosing)_mirror=result;
  }
  catch(OperationCanceledException){}
  catch(Exception){if(!_isClosing)_mirror=new(){Enabled=_mirror.Enabled,LastCompletedUtc=_mirror.LastCompletedUtc,Pending=true,Message="Mirror non disponibile. Il database locale resta operativo."};}
  finally{_mirrorRequestBusy=false;if(!_isClosing)RefreshMirror();}
 }
 private void RefreshMirror()
 {
  var ready=MirrorVisible&&_loaded&&!_busy&&!_isClosing&&!IsOpeningCash&&!_mirrorRequestBusy;
  _mirrorReady.Value=ready&&_mirror.Enabled&&!_mirror.Busy;
  _mirrorConnectReady.Value=ready&&_admin&&!_mirror.Busy;
  _mirrorBrowserReady.Value=ready&&HasMirrorCode&&IsMicrosoftDeviceUrl(_mirror.AuthenticationUrl);
  foreach(var name in new[]{nameof(MirrorVisible),nameof(HasMirrorCode),nameof(MirrorCode),nameof(MirrorMessage),nameof(MirrorNeedsAttention),nameof(MirrorBusy)})this.RaisePropertyChanged(name);
 }
 private void DisposeMirror()
 {
  SincronizzaMirrorCommand.Dispose();CollegaMirrorCommand.Dispose();ApriAccessoMirrorCommand.Dispose();
  _mirrorReady.Dispose();_mirrorConnectReady.Dispose();_mirrorBrowserReady.Dispose();
 }
}
