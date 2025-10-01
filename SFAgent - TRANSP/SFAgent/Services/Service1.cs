using System;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;
using SFAgent.Salesforce;
using SFAgent.Sap;
using SFAgent.Utils;
using Newtonsoft.Json;
using System.Text.RegularExpressions;
using System.Net.Mail;
using System.Collections.Generic;

namespace SFAgent.Services
{
    public partial class Service1 : ServiceBase
    {
        private SalesforceAuth _auth;
        private SalesforceApi _api;
        private Timer _timer;

        public Service1()
        {
            InitializeComponent();
            _auth = new SalesforceAuth();
            _api = new SalesforceApi();
        }

        protected override void OnStart(string[] args)
        {
            Logger.InitLog();

            if (!System.Diagnostics.EventLog.SourceExists("SFAgent"))
                System.Diagnostics.EventLog.CreateEventSource("SFAgent", "Application");

            // Primeira execução imediata
            Task.Run(async () =>
            {
                try
                {
                    await ProcessarTransportadoras();
                }
                catch (Exception ex)
                {
                    Logger.Log($"Erro inicial no OnStart: {ex.Message}");
                }
            });

            // Executa depois a cada 5 minutos
            _timer = new Timer(async _ =>
            {
                try
                {
                    await ProcessarTransportadoras();
                }
                catch (Exception ex)
                {
                    Logger.Log($"Erro no Timer: {ex.Message}");
                }
            }, null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
        }

        protected override void OnStop()
        {
            _timer?.Dispose();
            Logger.Log("Serviço parado.");
        }

        // ---------- Helpers ----------
        private static bool IsDbNull(object v) => v == null || v == DBNull.Value;
        private static string S(object v) => IsDbNull(v) ? null : v.ToString();

        private static string Trunc(string s, int max)
            => string.IsNullOrEmpty(s) ? s : (s.Length <= max ? s : s.Substring(0, max));

        private static string Digits(string s)
            => string.IsNullOrEmpty(s) ? null : Regex.Replace(s, "[^0-9]", "");

        private static string Uf2(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            s = s.Trim().ToUpperInvariant();
            // às vezes vem 'PR ' ou 'PR.' ou 'PR-'; pega só letras
            s = Regex.Replace(s, @"[^A-Z]", "");
            return s.Length >= 2 ? s.Substring(0, 2) : null;
        }

        // País em alpha-2 (bem simples: se começar com BR/BRA => BR; se já for 2 letras => mantém)
        private static string Country2(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            s = s.Trim().ToUpperInvariant();
            if (s.StartsWith("BR")) return "BR";     // BR ou BRA
            if (s.Length == 2) return s;            // já está em alpha-2
            return null;                            // evita erro em picklist restrita
        }

        // Para picklist “Sim/Não”
        private static string SimNaoFromYN(object v)
        {
            var s = v?.ToString()?.Trim().ToUpperInvariant();
            if (string.IsNullOrEmpty(s)) return "Não";
            return (s == "Y" || s == "S" || s == "SIM" || s == "1" || s == "TRUE") ? "Sim" : "Não";
        }

        // Para picklist “S/N”
        private static string SOrNFromYN(object v, string def = "N")
        {
            var s = v?.ToString()?.Trim().ToUpperInvariant();
            if (string.IsNullOrEmpty(s)) return def;
            return (s == "Y" || s == "S" || s == "SIM" || s == "1" || s == "TRUE") ? "S" : "N";
        }

        private static string FirstValidEmail(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;

            // Divide por vírgula, ponto e vírgula, barras ou espaços
            var parts = Regex.Split(raw, @"[,\;\s/]+");
            foreach (var p in parts)
            {
                var candidate = p?.Trim();
                if (string.IsNullOrEmpty(candidate)) continue;
                try
                {
                    var mail = new MailAddress(candidate);
                    // garante que não veio "Nome <email@dominio>"
                    if (mail.Address.Equals(candidate, StringComparison.OrdinalIgnoreCase))
                        return mail.Address;
                }
                catch { /* ignora inválidos */ }
            }
            return null; // nada válido
        }

        // ---------- /Helpers ----------

        private async Task ProcessarTransportadoras()
        {
            try
            {
                var token = await _auth.GetValidToken();
                var sap = new SapConnector("HANADB:30015", "SBO_ACOS_TESTE", "B1ADMIN", "S4P@2Q60_tm2");

                var sql = @"CALL SP_TRANSPORTADORAS_SF()";

                var rows = sap.ExecuteQuery(sql);

                // Calculo foreach sapExts (Verificado quantos foram adicionados)
                var sapExts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                int insertCount = 0;
                int updateCount = 0;
                int errorCount = 0;

                foreach (var r in rows)
                {
                    var ext = S(r["CardCode"]);
                    if (!string.IsNullOrWhiteSpace(ext))
                        sapExts.Add(ext);
                }

                foreach (var r in rows)
                {
                    var idExterno = S(r["CardCode"]); // ExternalId
                    if (string.IsNullOrWhiteSpace(idExterno))
                    {
                        Logger.Log("Transportadora ignorada: CardCode vazio.");
                        continue;
                    }

                    SalesforceApi.UpsertResult up = null; // <- tipado

                    try
                    {
                        // Campos base
                        var name = Trunc(S(r["CardName"]), 80);
                        var emailRaw = S(r["E_Mail"]);
                        var email = FirstValidEmail(emailRaw);
                        if (!string.IsNullOrEmpty(emailRaw) && email == null)
                            Logger.Log($"E-mail inválido descartado para {idExterno}: \"{emailRaw}\"");
                        var phone1 = Trunc(Digits(S(r["Phone1"])), 40);
                        var phone2 = Trunc(Digits(S(r["Phone2"])), 40);
                        var foreignName = Trunc(S(r["CardFName"]), 255);

                        // CNPJ/CPF (prioridade: U_SX_CNPJ -> VATRegNum -> LicTradNum)
                        var cnpj = S(r["U_SX_CNPJ"]);
                        if (string.IsNullOrWhiteSpace(cnpj)) cnpj = S(r["VATRegNum"]);
                        if (string.IsNullOrWhiteSpace(cnpj)) cnpj = S(r["LicTradNum"]);
                        cnpj = Trunc(cnpj, 18);

                        // Cód. Transportadora (U_SX_Transp se houver, senão o CardCode)
                        var codTransp = S(r["U_SX_Transp"]);
                        if (string.IsNullOrWhiteSpace(codTransp)) codTransp = idExterno;
                        codTransp = Trunc(codTransp, 80);

                        // Destinatário padrão
                        var destPadrao = Trunc(S(r["ShipToDef"]), 50);

                        // Endereços
                        var streetMain = Trunc(S(r["Address"]), 254);
                        var streetEntrega = Trunc(S(r["U_LG_EndEntrega"]), 254);
                        if (string.IsNullOrWhiteSpace(streetEntrega)) streetEntrega = streetMain;

                        var city = Trunc(S(r["City"]), 100);
                        var zip = Trunc(S(r["ZipCode"]), 20);
                        var uf1 = Uf2(S(r["State1"])); // Estado do destinatário
                        var uf2 = Uf2(S(r["State2"])); // Recebedor NF
                        var country = Country2(S(r["Country"])) ?? "BR";

                        // Inativo (picklist Sim/Não): ativo => "Não"; inativo => "Sim"
                        var validForYN = S(r["validFor"]);   // "Y" / "N"
                        var frozenForYN = S(r["frozenFor"]);  // "Y" / "N"
                        var ativo = string.Equals(validForYN, "Y", StringComparison.OrdinalIgnoreCase)
                                 && !string.Equals(frozenForYN, "Y", StringComparison.OrdinalIgnoreCase);
                        var inativoPick = ativo ? "Não" : "Sim";

                        // Tipo PN: picklist "S"/"N" (a partir de U_SX_IntegracaoPN)
                        var tipoPN = SOrNFromYN(S(r["U_SX_IntegracaoPN"]), "N");

                        var body = new
                        {
                            // Identificação básica
                            Name = name,
                            CA_CPFCNPJ__c = cnpj,
                            CA_CodTransportadora__c = codTransp,
                            CA_DestinatarioPadrao__c = destPadrao,
                            CA_Email__c = email,

                            // Telefones
                            CA_Telefone1__c = phone1,
                            CA_Telefone2__c = phone2,

                            // Nome estrangeiro
                            CA_NomeEstrangeiro__c = foreignName,

                            // Inativo (Sim/Não) e Tipo PN (S/N)
                            CA_Inativo__c = inativoPick,
                            CA_TipoPN__c = tipoPN,

                            // Estados auxiliares
                            CA_EstadoDestinatario__c = uf1,
                            CA_EstadoRecebedorNF__c = uf2,

                            // Endereço de Cobrança
                            CA_EnderecoCobranca__Street__s = streetMain,
                            CA_EnderecoCobranca__City__s = city,
                            CA_EnderecoCobranca__PostalCode__s = zip,
                            CA_EnderecoCobranca__StateCode__s = uf1,
                            CA_EnderecoCobranca__CountryCode__s = country,

                            // Endereço de Entrega
                            CA_EnderecoEntrega__Street__s = streetEntrega,
                            CA_EnderecoEntrega__City__s = city,
                            CA_EnderecoEntrega__PostalCode__s = zip,
                            CA_EnderecoEntrega__StateCode__s = uf1,
                            CA_EnderecoEntrega__CountryCode__s = country,

                            // Endereço de Faturamento
                            CA_EnderecoFaturamento__Street__s = streetMain,
                            CA_EnderecoFaturamento__City__s = city,
                            CA_EnderecoFaturamento__PostalCode__s = zip,
                            CA_EnderecoFaturamento__StateCode__s = uf1,
                            CA_EnderecoFaturamento__CountryCode__s = country
                        };

                        //(COD ANTIGO DE ERRO)
                        //var result = await _api.UpsertTransportadora(token, cardCode, body);
                        //var rowJson = JsonConvert.SerializeObject(r);
                        //Logger.Log(
                        //    $"SUCCESS - {result.Outcome} | METHOD={result.Method} | ExternalId={cardCode} | " +
                        //    $"SFID={result.SalesforceId ?? "-"} | Response={result.RawBody} | Row={rowJson}"
                        //);

                        up = await _api.UpsertTransportadora(token, idExterno, body);
                        if (up.Outcome == "POST" || up.Outcome == "INSERT")
                            insertCount++;
                        else if (up.Outcome == "PATCH" || up.Outcome == "UPDATE")
                            updateCount++;

                        Logger.Log(
                            $"METHOD={up.Method} SF Transportadora {up.Outcome} | ExternalId={idExterno} | Status={up.StatusCode}"
                        );
                    }
                    catch (Exception exItem)
                    {
                        errorCount++;

                        var rowJson = JsonConvert.SerializeObject(r);
                        Logger.Log(
                            $"ERRO METHOD={up?.Method ?? "N/A"} SF Transportadora | ExternalId={idExterno} | Erro={exItem.Message} | Row={rowJson}",
                            asError: true
                        );
                    }
                }

                Logger.Log($"Sync Transportadora finalizado. | Inseridos={insertCount} | Atualizados={updateCount} | Erros={errorCount} | Total SAP={sapExts.Count}.");
            }
            catch (Exception ex)
            {
                Logger.Log($"ERRO geral no processamento (OCRD): {ex.Message}", asError: true);
            }
        }
    }
}