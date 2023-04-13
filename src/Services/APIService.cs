using System.Net;
using System.Net.Http.Headers;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;

namespace TestEnvironment.Setup;

//---------------------------------------------------------------------------------------------------//
//Helper Classes
//---------------------------------------------------------------------------------------------------//
public class RequestData
{
    public string uri { get; set; } = default!;
    public string endpoint { get; set; } = default!;
    public HttpMethod httpMethod { get; set; } = default!;
    public Dictionary<string, string> headers { get; set; } = null!;
    public string contentType { get; set; } = "application/json";
    public string body = null!;
    public Stream streamBody = null!;
    public string parameters = null!;
    public int timeout = 120;
}

public class FileData
{
    public string name = "";
    public string fileName = "";
    public byte[] fileBytes = null!;
}

//---------------------------------------------------------------------------------------------------//

public static class APIService
{
    //---------------------------------------------------------------------------------------------------//
    //Requests
    //---------------------------------------------------------------------------------------------------//
    public static async Task<HttpResponseMessage> SendRequest(RequestData requestData)
    {
        using (HttpClient client = new HttpClient())
        {
            try
            {
                client.Timeout = TimeSpan.FromSeconds(requestData.timeout);
                ServicePointManager.ServerCertificateValidationCallback += ValidateCertificate!;
                HttpRequestMessage request = CreateRequest(client, requestData);
                if (requestData.body != null)
                {
                    request.Content = new StringContent(requestData.body, Encoding.UTF8);
                    request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(
                        requestData.contentType
                    );
                }

                HttpResponseMessage result = await client.SendAsync(request);
                return result;
            }
            catch
            {
                return default!;
            }
        }
    }

    //---------------------------------------------------------------------------------------------------//

    //---------------------------------------------------------------------------------------------------//
    //Responses
    //---------------------------------------------------------------------------------------------------//
    public static async Task<T> Content<T>(HttpResponseMessage response)
    {
        string stringContent = await response.Content.ReadAsStringAsync();
        T content = JsonSerializer.Deserialize<T>(stringContent)!;
        return content;
    }

    public static async Task<string> Content(HttpResponseMessage response)
    {
        string content = await response.Content.ReadAsStringAsync();
        return content;
    }

    //---------------------------------------------------------------------------------------------------//

    //---------------------------------------------------------------------------------------------------//
    //Helper Function
    //---------------------------------------------------------------------------------------------------//
    public static HttpRequestMessage CreateRequest(HttpClient client, RequestData requestData)
    {
        client.BaseAddress = new Uri(requestData.uri);
        if (requestData.parameters != null)
            requestData.endpoint += String.Format("?{0}", requestData.parameters);

        HttpRequestMessage request = new HttpRequestMessage(
            requestData.httpMethod,
            requestData.endpoint
        );
        if (requestData.headers != null)
        {
            foreach (KeyValuePair<string, string> headerPair in requestData.headers)
            {
                if (!request.Headers.Contains(headerPair.Key))
                {
                    request.Headers.Add(headerPair.Key, headerPair.Value);
                }
            }
        }
        return request;
    }

    //---------------------------------------------------------------------------------------------------//

    //---------------------------------------------------------------------------------------------------//
    //Certificate Validation
    //---------------------------------------------------------------------------------------------------//
    public static bool ValidateCertificate(
        object sender,
        X509Certificate cert,
        X509Chain chain,
        SslPolicyErrors errors
    )
    {
        if (errors != SslPolicyErrors.None)
        {
            Console.WriteLine(String.Format("Certificate Error: {0}", errors.ToString()));
            Console.WriteLine(String.Format("Chain Policy: {0}", chain.ChainPolicy));
            for (int i = 0; i < chain.ChainStatus.Length; i++)
            {
                Console.WriteLine(chain.ChainStatus[i].ToString());
            }
            return false;
        }

        // Long Term: Check cert.GetCertHashString() for valid hash, we need to detect new certificates when old ones expire
        // string certHash = cert.GetCertHashString();
        return true;
    }

    public static bool ValidateSelfSignedCertificate(
        object sender,
        X509Certificate cert,
        X509Chain chain,
        SslPolicyErrors errors
    )
    {
        // Long Term: Check cert.GetCertHashString() for valid hash, we need to detect new certificates when old ones expire
        // string certHash = cert.GetCertHashString();
        return true;
    }
    //---------------------------------------------------------------------------------------------------//
}
