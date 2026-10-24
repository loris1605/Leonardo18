using Azure.Core;
using Azure.Identity;
using System.Security.Cryptography;
namespace Leonardo18.DatabaseHost.Mirror;

public sealed class MirrorIdentity
{
 public const string ClientId="4ae481fc-5ded-4037-970d-bfe74526bc51",TenantId="9e6daf77-a159-47a4-a2f8-a8cc6b03bd48";
 private readonly string _folder;
 private readonly SemaphoreSlim _gate=new(1,1);
 public MirrorIdentity(string folder)=>_folder=folder;
 private string AccountFile=>Path.Combine(_folder,"account.dpapi");
 public bool HasAccount=>File.Exists(AccountFile);
 private DeviceCodeCredential Create(AuthenticationRecord? record,Func<DeviceCodeInfo,CancellationToken,Task>? callback=null)=>new(new DeviceCodeCredentialOptions
 {
  ClientId=ClientId,TenantId=TenantId,AuthenticationRecord=record,DisableAutomaticAuthentication=true,
  TokenCachePersistenceOptions=new(){Name="Leonardo18.Mirror",UnsafeAllowUnencryptedStorage=false},
  DeviceCodeCallback=callback??((_,_)=>throw new AuthenticationRequiredException("Rinnova il collegamento Microsoft dal menu Mirror.",new TokenRequestContext(["https://database.windows.net/.default"])))
 });
 public async Task ConnectAsync(Func<DeviceCodeInfo,CancellationToken,Task> callback,CancellationToken ct=default)
 {
  if(!OperatingSystem.IsWindows())throw new PlatformNotSupportedException("Il collegamento mirror richiede la protezione credenziali di Windows.");
  await _gate.WaitAsync(ct);
  try
  {
   var credential=Create(null,callback);
   var record=await credential.AuthenticateAsync(new TokenRequestContext(["https://database.windows.net/.default"]),ct);
   using var stream=new MemoryStream();await record.SerializeAsync(stream,ct);
   var plain=stream.ToArray();
   try{Directory.CreateDirectory(_folder);var tmp=AccountFile+".tmp";await File.WriteAllBytesAsync(tmp,ProtectedData.Protect(plain,null,DataProtectionScope.CurrentUser),ct);File.Move(tmp,AccountFile,true);}
   finally{CryptographicOperations.ZeroMemory(plain);}
  }
  finally{_gate.Release();}
 }
 public async Task<string> TokenAsync(CancellationToken ct=default)
 {
  if(!OperatingSystem.IsWindows())throw new PlatformNotSupportedException("Il collegamento mirror richiede Windows.");
  if(!HasAccount)throw new AuthenticationRequiredException("Collega il mirror con l'account Microsoft.",new TokenRequestContext(["https://database.windows.net/.default"]));
  var plain=ProtectedData.Unprotect(await File.ReadAllBytesAsync(AccountFile,ct),null,DataProtectionScope.CurrentUser);
  try
  {
   using var stream=new MemoryStream(plain);
   var record=await AuthenticationRecord.DeserializeAsync(stream,ct);
   var token=await Create(record).GetTokenAsync(new TokenRequestContext(["https://database.windows.net/.default"]),ct);
   return token.Token;
  }
  finally{CryptographicOperations.ZeroMemory(plain);}
 }
}
