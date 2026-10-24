namespace AppServices.Database;
public sealed class MirrorStatus
{
 public bool Enabled {get;set;}
 public bool Busy {get;set;}
 public bool NeedsAuthentication {get;set;}
 public bool Pending {get;set;}
 public string ErrorCode {get;set;}="";
 public string Message {get;set;}="Mirror non collegato";
 public string AuthenticationCode {get;set;}="";
 public string AuthenticationUrl {get;set;}="";
 public DateTime? LastCompletedUtc {get;set;}
 public DateTime? NextAttemptUtc {get;set;}
 public int TableCount {get;set;}
 public long RowCount {get;set;}
}
