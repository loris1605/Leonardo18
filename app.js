//http://127.0.0.1:5000/update?codice_socio=77000540&numero_tessera=71111111&stato=attivo&tipo_tessera_id=1&circolo_id=19&data_scadenza=20160502&nome=PAULO&cognome=DE+OLIVEIRA&born_date=19640813&data_rilascio=20150812

var http = require('http');
var url = require('url');
var db = require('odbc')();
var cn = "Driver={SQL Server};Server=DESKTOP-O1LQE7B;Database=ulisse;Uid=LORISADMIN;Pwd=Flight22#;";

db.open(cn, function (err) {
    if (err) return console.log("Errore connessione DB: " + err);
    console.log("Connesso al database SQL Server.");
});

var PORT = process.env.PORT || 5000;

var server = http.createServer(function (req, res) {
    var q = url.parse(req.url, true).query;

    if (req.url.indexOf('/update') !== -1) {
        
        var id_socio = q['codice_socio'] || '0';
        var numero = q['numero_tessera'] || '0';
        var cognome = (q['cognome'] || '').replace(/'/g, "''");
        var nome = (q['nome'] || '').replace(/'/g, "''");
        var nato = q['born_date'] || '';
        var scade = q['data_scadenza'] || '';
        var cod_motivo = 0;

        console.log("Inserimento socio: " + id_socio);

        // Costruiamo la query manualmente convertendo i valori dove necessario
        // Usiamo GETDATE() per la data così non sbagliamo formato
        var sql = "INSERT INTO Strisciate (DataOra, CodiceSocio, NumeroTessera, Cognome, Nome, Natoil, Scadenza, CodMotivo) " +
                  "VALUES (0, " + 
                  "'" + id_socio + "', " + 
                  "'" + numero + "', " + 
                  "'" + cognome + "', " + 
                  "'" + nome + "', " + 
                  "'" + nato + "', " + 
                  "'" + scade + "', " + 
                  cod_motivo + ")"; 

        db.query(sql, function (err5, data5) {
            if (err5) {
                console.log("Errore SQL: " + err5);
                res.writeHead(500);
                res.end('Errore: ' + err5);
            } else {
                console.log("Inserimento riuscito!");
                res.writeHead(200, {'Content-Type': 'text/plain'});
                res.end('OK');
            }
        });

    } else {
        res.writeHead(404);
        res.end('Not Found');
    }
});

server.listen(PORT, "127.0.0.1", function() {
    console.log("Server in ascolto su http://127.0.0.1:" + PORT);
});
