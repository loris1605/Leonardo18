using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Models.Context;
namespace Leonardo18.DatabaseHost.Mirror;

public sealed record MirrorSnapshot(string SourceKey, DateTime CapturedUtc, List<MirrorSnapshotEngine.Table> Tables, Dictionary<string,DataTable> Data, Dictionary<string,string> Digests, string Fingerprint)
{
 public long RowCount => Data.Values.Sum(t=>(long)t.Rows.Count);
}
public sealed class MirrorSnapshotEngine
{
 public const string Server="leonardo18-mirror-20261019.database.windows.net", Database="UlisseMirror", Marker="__LeonardoMirrorState";
 public static string SourceKey(string connectionString)
 {
  var b=new SqlConnectionStringBuilder(connectionString);
  if(!string.Equals(b.InitialCatalog,"ulisse",StringComparison.OrdinalIgnoreCase)||b.DataSource.Contains(".database.windows.net",StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Il mirror richiede il database locale Ulisse.");
  return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(b.DataSource.ToUpperInvariant()+"|ULISSE")));
 }
 public async Task<MirrorSnapshot> CaptureAsync(string connectionString,CancellationToken ct=default)
 {
  var source=SourceKey(connectionString);
  var settings=new SqlConnectionStringBuilder(connectionString){ConnectTimeout=5,ApplicationName="Leonardo18 Mirror - lettura locale",Pooling=false};
  await using var local=new SqlConnection(settings.ConnectionString);await local.OpenAsync(ct);
  await using(var model=new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(local).Options))
   if((await model.Database.GetPendingMigrationsAsync(ct)).Any())throw new InvalidOperationException("Aggiornare lo schema locale prima della copia.");
  if(Convert.ToInt32(await Scalar(local,null,$"SELECT COUNT(*) FROM sys.tables WHERE name=N'{Marker}'"))!=0)throw new InvalidOperationException("Origine identificata come mirror: copia rifiutata.");
  var tables=await Tables(local,null);
  var data=new Dictionary<string,DataTable>();var captured=DateTime.UtcNow;
  await using(var tx=(SqlTransaction)await local.BeginTransactionAsync(IsolationLevel.Serializable,ct))
  {
   await Exec(local,tx,"SET LOCK_TIMEOUT 250;");
   foreach(var t in tables)
   {
    ct.ThrowIfCancellationRequested();
    await using var cmd=new SqlCommand($"SELECT {string.Join(",",t.Columns.Select(c=>Quote(c.Name)))} FROM {t.Sql} WITH (TABLOCK,HOLDLOCK)",local,tx){CommandTimeout=3};
    using var cancel=ct.Register(()=>cmd.Cancel());
    await using var reader=await cmd.ExecuteReaderAsync(ct);var dt=new DataTable();dt.Load(reader);data.Add(t.Sql,dt);
   }
   captured=DateTime.SpecifyKind(Convert.ToDateTime(await Scalar(local,tx,"SELECT SYSUTCDATETIME()")),DateTimeKind.Utc);
   await tx.CommitAsync(ct);
  }
  var digests=data.ToDictionary(kv=>kv.Key,kv=>Digest(kv.Value));
  var hash=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n",digests.Select(x=>x.Key+"="+x.Value)))));
  return new(source,captured,tables,data,digests,hash);
 }
 public async Task ApplyAsync(MirrorSnapshot snapshot,SqlConnection remote,CancellationToken ct=default)
 {
  if(remote.State!=ConnectionState.Open||remote.Database!=Database||!remote.DataSource.Contains(Server,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Destinazione mirror inattesa.");
  await Exec(remote,null,"DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource=N'Leonardo18.UlisseMirror',@LockMode='Exclusive',@LockOwner='Session',@LockTimeout=0; IF @r<0 THROW 51000,'Altra sincronizzazione in corso.',1;");
  if(Convert.ToInt32(await Scalar(remote,null,$"SELECT COUNT(*) FROM sys.tables WHERE name=N'{Marker}' AND schema_id=SCHEMA_ID('dbo')"))!=1)throw new InvalidOperationException("Database cloud non inizializzato come mirror.");
  if(!string.Equals(Convert.ToString(await Scalar(remote,null,$"SELECT SourceKey FROM [dbo].[{Marker}] WHERE Id=1")),snapshot.SourceKey,StringComparison.Ordinal))throw new InvalidOperationException("Mirror associato a un'altra origine.");
  var previousSnapshot=await Scalar(remote,null,$"SELECT LastSnapshotUtc FROM [dbo].[{Marker}] WHERE Id=1");
  if(previousSnapshot is DateTime previous && previous>snapshot.CapturedUtc)throw new InvalidOperationException("Una copia più recente è già presente sul mirror. Ripetere la lettura locale.");
  await using(var model=new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(remote).Options))
  {
   model.Database.SetCommandTimeout(180);
   if((await model.Database.GetPendingMigrationsAsync(ct)).Any())await model.Database.MigrateAsync(ct);
  }
  var target=await Tables(remote,null);
  if(!snapshot.Tables.Select(t=>t.Sql).SequenceEqual(target.Select(t=>t.Sql)))throw new InvalidOperationException("Le tabelle del mirror non coincidono con quelle locali.");
  foreach(var t in snapshot.Tables)if(!t.Columns.SequenceEqual(target.Single(x=>x.Sql==t.Sql).Columns))throw new InvalidOperationException("Schema diverso: "+t.Sql);
  await using var tx=(SqlTransaction)await remote.BeginTransactionAsync(IsolationLevel.Serializable,ct);
  try
  {
   foreach(var t in snapshot.Tables){ct.ThrowIfCancellationRequested();await Exec(remote,tx,$"ALTER TABLE {t.Sql} NOCHECK CONSTRAINT ALL;");}
   foreach(var t in snapshot.Tables){ct.ThrowIfCancellationRequested();await Exec(remote,tx,$"DELETE FROM {t.Sql};");}
   foreach(var t in snapshot.Tables)
   {
    using var bulk=new SqlBulkCopy(remote,SqlBulkCopyOptions.KeepIdentity|SqlBulkCopyOptions.KeepNulls,tx){DestinationTableName=t.Sql,BulkCopyTimeout=120,BatchSize=1000};
    foreach(var c in t.Columns)bulk.ColumnMappings.Add(c.Name,c.Name);
    await bulk.WriteToServerAsync(snapshot.Data[t.Sql],ct);
   }
   foreach(var t in snapshot.Tables){ct.ThrowIfCancellationRequested();await Exec(remote,tx,$"ALTER TABLE {t.Sql} WITH CHECK CHECK CONSTRAINT ALL;");}
   foreach(var t in snapshot.Tables){ct.ThrowIfCancellationRequested();if(Digest(await ReadTable(remote,tx,t))!=snapshot.Digests[t.Sql])throw new InvalidOperationException("Verifica contenuti fallita: "+t.Sql);}
   await using var stamp=new SqlCommand($"UPDATE [dbo].[{Marker}] SET LastSnapshotUtc=@snapshot,LastCompletedUtc=SYSUTCDATETIME(),TableCount=@tables,[RowCount]=@rows,ManifestHash=@hash WHERE Id=1 AND SourceKey=@source",remote,tx);
   stamp.Parameters.Add("@snapshot",SqlDbType.DateTime2).Value=snapshot.CapturedUtc;stamp.Parameters.AddWithValue("@tables",snapshot.Tables.Count);stamp.Parameters.AddWithValue("@rows",snapshot.RowCount);stamp.Parameters.AddWithValue("@hash",snapshot.Fingerprint);stamp.Parameters.AddWithValue("@source",snapshot.SourceKey);
   if(await stamp.ExecuteNonQueryAsync(ct)!=1)throw new InvalidOperationException("Identità mirror modificata.");
   await tx.CommitAsync(ct);
  }
  catch{await tx.RollbackAsync(CancellationToken.None);throw;}
 }
static string Quote(string name)=>"["+name.Replace("]","]]")+"]";
static async Task<object?> Scalar(SqlConnection c,SqlTransaction? tx,string sql){await using var cmd=new SqlCommand(sql,c,tx){CommandTimeout=180};return await cmd.ExecuteScalarAsync();}
static async Task Exec(SqlConnection c,SqlTransaction? tx,string sql){await using var cmd=new SqlCommand(sql,c,tx){CommandTimeout=180};await cmd.ExecuteNonQueryAsync();}
private static async Task<DataTable> ReadTable(SqlConnection c,SqlTransaction tx,Table t){await using var cmd=new SqlCommand($"SELECT {string.Join(",",t.Columns.Select(x=>Quote(x.Name)))} FROM {t.Sql}",c,tx){CommandTimeout=180};await using var r=await cmd.ExecuteReaderAsync();var dt=new DataTable();dt.Load(r);return dt;}
private static async Task<List<Table>> Tables(SqlConnection c,SqlTransaction? tx)
{
 var result=new List<Table>();
 await using var cmd=new SqlCommand("SELECT s.name,t.name,c.name,ty.name,c.max_length,c.precision,c.scale,c.is_nullable,c.is_identity,c.is_computed,c.generated_always_type FROM sys.tables t JOIN sys.schemas s ON s.schema_id=t.schema_id JOIN sys.columns c ON c.object_id=t.object_id JOIN sys.types ty ON ty.user_type_id=c.user_type_id WHERE t.is_ms_shipped=0 ORDER BY s.name,t.name,c.column_id",c,tx);
 await using var r=await cmd.ExecuteReaderAsync();
 while(await r.ReadAsync())
 {
  var schema=r.GetString(0);var name=r.GetString(1);
  if(name=="__LeonardoMirrorState")continue;
  if(r.GetBoolean(9)||r.GetByte(10)!=0||r.GetString(3) is "timestamp" or "rowversion")throw new InvalidOperationException("Colonna generata non supportata: "+schema+"."+name);
  var t=result.LastOrDefault();if(t is null||t.Schema!=schema||t.Name!=name){t=new(schema,name,[]);result.Add(t);}
  t.Columns.Add(new(r.GetString(2),r.GetString(3),r.GetInt16(4),r.GetByte(5),r.GetByte(6),r.GetBoolean(7),r.GetBoolean(8)));
 }
 return result;
}
public static string Digest(DataTable table)
{
 var rows=new List<string>(table.Rows.Count);
 foreach(DataRow row in table.Rows)
 {
  using var stream=new MemoryStream();using var writer=new BinaryWriter(stream,Encoding.UTF8,true);
  foreach(var value in row.ItemArray)
  {
   if(value is null or DBNull){writer.Write((byte)0);continue;}
   writer.Write((byte)1);
   switch(value)
   {
    case byte[] bytes:writer.Write(bytes.Length);writer.Write(bytes);break;
    case DateTime time:writer.Write(time.Ticks);break;
    case DateTimeOffset time:writer.Write(time.Ticks);writer.Write(time.Offset.Ticks);break;
    case TimeSpan duration:writer.Write(duration.Ticks);break;
    case double number:writer.Write(number);break;
    case float number:writer.Write(number);break;
    default:writer.Write(Convert.ToString(value,CultureInfo.InvariantCulture)??"");break;
   }
  }
  writer.Flush();rows.Add(Convert.ToHexString(SHA256.HashData(stream.ToArray())));
 }
 rows.Sort(StringComparer.Ordinal);return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n",rows))));
}
public record Column(string Name,string Type,short Length,byte Precision,byte Scale,bool Nullable,bool Identity);
public record Table(string Schema,string Name,List<Column> Columns){public string Sql=>QuoteName(Schema)+"."+QuoteName(Name);static string QuoteName(string n)=>"["+n.Replace("]","]]")+"]";}

}
