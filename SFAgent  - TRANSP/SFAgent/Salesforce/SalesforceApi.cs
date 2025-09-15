using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using SFAgent.Config;
using System.Text;
using System;
using static System.Net.WebRequestMethods;

namespace SFAgent.Salesforce
{
    public class SalesforceApi
    {
        private static readonly HttpClient _http = new HttpClient();

        public class UpsertResult
        {
            public string Method { get; set; } = "PATCH";
            public string Outcome { get; set; } // "INSERT" | "UPDATE" | "SUCCESS"
            public int StatusCode { get; set; }
            public string SalesforceId { get; set; } // quando 201 vem no body
            public string RawBody { get; set; }  // Response - SF
        }

        internal class SalesforceUpsertResponse
        {
            public string id { get; set; }
            public bool success { get; set; }
            public bool created { get; set; }
            public object[] errors { get; set; }
        }

        public async Task<UpsertResult> UpsertTransportadora(string token, string idExterno, object condicao)
        {
            var externalPath = $"{ConfigUrls.ApiCondicaoBase}/{ConfigUrls.ApiCondicaoExternalField}/{Uri.EscapeDataString(idExterno)}";
            var json = JsonConvert.SerializeObject(condicao);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var req = new HttpRequestMessage(new HttpMethod("PATCH"), externalPath);
            req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            req.Content = content;

            var resp = await _http.SendAsync(req);
            var body = await resp.Content.ReadAsStringAsync();

            var result = new UpsertResult
            {
                Method = "PATCH",
                StatusCode = (int)resp.StatusCode,
                RawBody = body
            };

            if (resp.IsSuccessStatusCode)
            {
                // 201 = INSERT, 204 = UPDATE
                if (result.StatusCode == 201)
                {
                    result.Outcome = "POST";
                    try
                    {
                        var parsed = JsonConvert.DeserializeObject<SalesforceUpsertResponse>(body);
                        result.SalesforceId = parsed?.id;
                    }
                    catch { /* se falhar parse, ignora */ }
                }
                else if (result.StatusCode == 204)
                {
                    result.Outcome = "PATCH";
                }
                else
                {
                    result.Outcome = "SUCCESS";
                }

                return result;
            }

            throw new Exception($"Erro no UPSERT Transportadora (HTTP {result.StatusCode}): {body}");
        }
    }
}

