namespace AppServices.Database;

public enum CassaAction { Load, OpenSheet, ListInside, PrepareEntry, FindCard, RegisterEntry, StopStrisciate, AddCharge, SaveGuardaroba, SetPagamento, StornaConto, PrepareAbbonamento, BuyAbbonamento, LoadSospesi, SaldaSospeso, PrepareUscita, RegisterUscita, ActivateFidelity, LoadFidelityHistory }

public sealed class CassaRequest
{
    public CassaAction Action { get; set; }
    public int PostazioneId { get; set; }
    public string Posizione { get; set; } = "";
    public string NumeroTessera { get; set; } = "";
    public int TariffaId { get; set; }
    public int AbbonamentoIngressoId { get; set; }
    public int NumeroIngressoAbbonamento { get; set; }
    public bool Rientro { get; set; }
    public int TipoFidelityId { get; set; }
    public int IngressoFidelityId { get; set; }
    public bool UsaCreditoFidelity { get; set; }
    public int SchedaId { get; set; }
    public int ListinoId { get; set; }
    public int ContoId { get; set; }
    public bool Pagato { get; set; }
    public int Grb1 { get; set; }
    public int Grb2 { get; set; }
    public int Grb3 { get; set; }
    public int Grb4 { get; set; }
    public string Note { get; set; } = "";
    public string StrisciateSessionId { get; set; } = "";
    public string NumeroTessera2 { get; set; } = "";
    public int PersonId1 { get; set; }
    public int PersonId2 { get; set; }
    public int TipoAbbonamentoId { get; set; }
    public decimal PrezzoConfermato { get; set; }
    public int IngressiConfermati { get; set; }
    public int DurataConfermata { get; set; }
    public Guid AcquistoId { get; set; }
    public long SospesoId { get; set; }
    public Guid UscitaId { get; set; }
    public string VersioneConto { get; set; } = "";
}

public sealed class CassaResult
{
    public bool? AbbonamentiDisponibili { get; set; }
    public CassaUscita? Uscita { get; set; }
    public CassaRientro? Rientro { get; set; }
    public CassaFidelity? Fidelity { get; set; }
    public CassaMovimentoFidelity[] MovimentiFidelity { get; set; } = [];
    public CassaTipoFidelity[] TipiFidelity { get; set; } = [];
    public string PostazioneName { get; set; } = "";
    public string StrisciateEndpoint { get; set; } = "";
    public CassaScheda? Scheda { get; set; }
    public CassaScheda[] Schede { get; set; } = [];
    public CassaConto[] Conto { get; set; } = [];
    public CassaVoceSpesa[] VociSpesa { get; set; } = [];
    public CassaIngresso[] Ingressi { get; set; } = [];
    public CassaSocio? Socio { get; set; }
    public string[] PosizioniOccupate { get; set; } = [];
    public CassaTipoAbbonamento[] TipiAbbonamento { get; set; } = [];
    public CassaAcquistoAbbonamento? AcquistoAbbonamento { get; set; }
    public CassaSospeso[] Sospesi { get; set; } = [];
    public CassaAbbonamentoIngresso[] AbbonamentiIngresso { get; set; } = [];
    public long SospesoSaldatoId { get; set; }
    public CassaSocio[] SociConSospesi { get; set; } = [];
}

public sealed class CassaScheda : System.ComponentModel.INotifyPropertyChanged
{
    public int Id { get; set; }
    public int PersonId { get; set; }
    public string Posizione { get; set; } = "";
    public string NumeroTessera { get; set; } = "";
    public string Cognome { get; set; } = "";
    public string Nome { get; set; } = "";
    public int Natoil { get; set; }
    public DateTime CheckinTime { get; set; }
    public DateTime CheckoutTime { get; set; }
    public int Grb1 { get; set; }
    public int Grb2 { get; set; }
    public int Grb3 { get; set; }
    public int Grb4 { get; set; }
    public decimal Consumazione { get; set; }
    public bool Blocco { get; set; }
    public string Note { get; set; } = "";
    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    private int _sospesiAperti;
    private decimal _totaleSospesi;
    private void NotifySospesi(string property)
    {
        PropertyChanged?.Invoke(this,new(property));
        PropertyChanged?.Invoke(this,new(nameof(HasSospesi)));
        PropertyChanged?.Invoke(this,new(nameof(AvvisoSospesi)));
    }
    public int SospesiAperti { get=>_sospesiAperti; set {if(_sospesiAperti==value)return;_sospesiAperti=value;NotifySospesi(nameof(SospesiAperti));} }
    public decimal TotaleSospesi { get=>_totaleSospesi; set {if(_totaleSospesi==value)return;_totaleSospesi=value;NotifySospesi(nameof(TotaleSospesi));} }
    public bool HasSospesi => SospesiAperti > 0;
    public string AvvisoSospesi => HasSospesi ? $"SOSPESI: {SospesiAperti} · {TotaleSospesi:0.00} € da incassare" : "";
    public string Anagrafica => $"{Cognome} {Nome}".Trim();
    public string Nascita => CassaEntryRules.FormatDate(Natoil);
    public bool IngressoAnnullato {get;set;}
    public string AnagraficaConEta
    {
        get
        {
            var today = DateTime.Today;
            if (!DateTime.TryParseExact(Natoil.ToString("D8"), "yyyyMMdd",
                System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var birth)
                || birth > today) return Anagrafica;
            var age = today.Year - birth.Year;
            if (birth.AddYears(age) > today) age--;
            return $"{Anagrafica} · {age} anni";
        }
    }
}

public sealed class CassaConto : System.ComponentModel.INotifyPropertyChanged
{
    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    public int Id { get; set; }
    public string DescSettore { get; set; } = "";
    public string DescPostazione { get; set; } = "";
    public string VoiceDesc { get; set; } = "";
    public decimal VoicePrice { get; set; }
    private bool _pagato;
    public bool Pagato
    {
        get => _pagato;
        set { if (_pagato == value) return; _pagato=value; RefreshPagamento(); }
    }
    public void RefreshPagamento()
    {
        PropertyChanged?.Invoke(this, new(nameof(Pagato)));
        PropertyChanged?.Invoke(this, new(nameof(Stato)));
    }
    public string Note { get; set; } = "";
    public DateTime DataOra { get; set; }
    public string Stato => Pagato ? "Pagato" : "Da pagare";
    private bool _hasScontoFidelity;
    public bool HasScontoFidelity { get=>_hasScontoFidelity; set { _hasScontoFidelity=value;PropertyChanged?.Invoke(this,new(nameof(CanStornare))); } }
    public bool IsIngresso {get;set;}
    public bool IsStornata {get;set;}
    public bool CanStornare => !IsStornata&&(VoicePrice > 0||VoicePrice==0&&(HasScontoFidelity||IsIngresso));
}

public sealed class CassaVoceSpesa
{
    public int ListinoId { get; set; }
    public string Settore { get; set; } = "";
    public string Descrizione { get; set; } = "";
    public decimal Prezzo { get; set; }
    public string Label => $"{Descrizione} · {Prezzo:0.00} €";
}

public sealed class CassaIngresso
{
    public int Id { get; set; }
    public string Nome { get; set; } = "";
    public string Etichetta { get; set; } = "";
    public decimal Prezzo { get; set; }
    public bool FreeDrink { get; set; }
    public string Label => string.IsNullOrWhiteSpace(Etichetta) ? Nome : Etichetta;
}

public sealed class CassaSocio : System.ComponentModel.INotifyPropertyChanged
{
    public int PersonId { get; set; }
    public string NumeroTessera { get; set; } = "";
    public string NumeroSocio { get; set; } = "";
    public string Cognome { get; set; } = "";
    public string Nome { get; set; } = "";
    public int Natoil { get; set; }
    public int ScadenzaTessera { get; set; }
    public bool Blocco { get; set; }
    public string PosizioneOccupata { get; set; } = "";
    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    private int _sospesiAperti;
    private decimal _totaleSospesi;
    private void NotifySospesi(string property)
    {
        PropertyChanged?.Invoke(this,new(property));
        PropertyChanged?.Invoke(this,new(nameof(HasSospesi)));
        PropertyChanged?.Invoke(this,new(nameof(AvvisoSospesi)));
    }
    public int SospesiAperti { get=>_sospesiAperti; set {if(_sospesiAperti==value)return;_sospesiAperti=value;NotifySospesi(nameof(SospesiAperti));} }
    public decimal TotaleSospesi { get=>_totaleSospesi; set {if(_totaleSospesi==value)return;_totaleSospesi=value;NotifySospesi(nameof(TotaleSospesi));} }
    public bool HasSospesi => SospesiAperti > 0;
    public string AvvisoSospesi => HasSospesi ? $"SOSPESI: {SospesiAperti} · {TotaleSospesi:0.00} € da incassare" : "";
    public string Anagrafica => $"{Cognome} {Nome}".Trim();
    public string Nascita => CassaEntryRules.FormatDate(Natoil);
    public string Scadenza => CassaEntryRules.FormatDate(ScadenzaTessera);
}

public static class CassaEntryRules
{
    public static string Validate(CassaSocio? socio, string card, string position, CassaIngresso? ingresso,
        IEnumerable<string> occupied, DateTime today)
    {
        if (socio is null || socio.PersonId == 0 || !string.Equals(socio.NumeroTessera, card.Trim(), StringComparison.Ordinal))
            return "Identifica il socio tramite la tessera.";
        if (socio.HasSospesi) return "Ingresso non consentito: salda prima i sospesi del socio.";
        if (socio.Blocco) return "Tessera non abilitata.";
        if (socio.ScadenzaTessera < today.Year * 10000 + today.Month * 100 + today.Day)
            return "Tessera scaduta.";
        if (!string.IsNullOrWhiteSpace(socio.PosizioneOccupata)) return "Socio già all'interno, posizione " + socio.PosizioneOccupata + ".";
        if (string.IsNullOrWhiteSpace(position)) return "Inserisci la posizione.";
        if (position.Trim().Length > 30) return "La posizione può contenere al massimo 30 caratteri.";
        if (occupied.Contains(position.Trim(), StringComparer.OrdinalIgnoreCase)) return "Posizione già occupata.";
        if (ingresso is null) return "Seleziona una tariffa di ingresso.";
        return "";
    }

    public static string FormatDate(int value) => DateTime.TryParseExact(value.ToString("D8"), "yyyyMMdd",
        System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var date)
        ? date.ToString("dd/MM/yyyy") : "";
}
