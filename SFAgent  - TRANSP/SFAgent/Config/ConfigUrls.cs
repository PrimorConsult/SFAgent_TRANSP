namespace SFAgent.Config
{
    public static class ConfigUrls
    {
        public static string AuthUrl =
            "https://acos-continente--homolog.sandbox.my.salesforce.com/services/oauth2/token";

        // Base do objeto (coleção) para POST
        public static string ApiCondicaoBase =
            "https://acos-continente--homolog.sandbox.my.salesforce.com/services/data/v60.0/sobjects/CA_Transportadora__c";

        // Nome do campo de External ID usado no upsert (Editavel / Dinamico)
        public static string ApiCondicaoExternalField = "CA_IdExterno__c";
    }
}
