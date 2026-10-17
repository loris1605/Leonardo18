namespace Models.Entity.Global
{

    public static class Enums
    {
        public enum Postazioni
        {
            Amministratore = 1,
            Cassa = 2,
            Bar = 3,
            Guardaroba = 4,
            Pulizie = 5,
            Report = 6
        }



        public enum TipoBlocco
        {
            BloccoPoszione = 1,
            Debito = 2,
            Comunicazioni = 4,
            Sospeso = 8,
            Espulso = 16
        }
    }
}
