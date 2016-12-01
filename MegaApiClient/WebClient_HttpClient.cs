namespace CG.Web.MegaApiClient
{
  using System;
  using System.IO;
  using System.Reflection;
  using System.Text;
  using System.Threading;

  using System.Net.Http;
  using System.Net.Http.Headers;

  #if NETSTANDARD1_6
  using Microsoft.Extensions.PlatformAbstractions;
  #endif

  public class WebClient : IWebClient
  {
    private const int DefaultResponseTimeout = Timeout.Infinite;

    private readonly HttpClient httpClient = new HttpClient();

    public WebClient()
        : this(DefaultResponseTimeout)
    {
      this.BufferSize = MegaApiClient.DefaultBufferSize;
    }

    internal WebClient(int responseTimeout)
    {
      this.httpClient.Timeout = TimeSpan.FromMilliseconds(responseTimeout);
      this.httpClient.DefaultRequestHeaders.UserAgent.Add(this.GenerateUserAgent());
    }

    public int BufferSize { get; set; }

    public string PostRequestJson(Uri url, string jsonData)
    {
      using (MemoryStream jsonStream = new MemoryStream(jsonData.ToBytes()))
      {
        return this.PostRequest(url, jsonStream, "application/json");
      }
    }

    public string PostRequestRaw(Uri url, Stream dataStream)
    {
      return this.PostRequest(url, dataStream, "application/octet-stream");
    }

    public Stream GetRequestRaw(Uri url)
    {
      return this.httpClient.GetStreamAsync(url).Result;
    }

    private string PostRequest(Uri url, Stream dataStream, string contentType)
    {
      using (StreamContent content = new StreamContent(dataStream, this.BufferSize))
      {
        content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        using (HttpResponseMessage response = this.httpClient.PostAsync(url, content).Result)
        {
          using (Stream stream = response.Content.ReadAsStreamAsync().Result)
          {
            using (StreamReader streamReader = new StreamReader(stream, Encoding.UTF8))
            {
              return streamReader.ReadToEnd();
            }
          }
        }
      }
    }

    private ProductInfoHeaderValue GenerateUserAgent()
    {
      string assemblyName;
      string assemblyVersion;
#if NETSTANDARD1_6
      assemblyName = PlatformServices.Default.Application.ApplicationName;
      assemblyVersion = PlatformServices.Default.Application.ApplicationVersion.ToString();
#else
      var assembly = Assembly.GetExecutingAssembly().GetName();
      assemblyName = assembly.Name;
      assemblyVersion = assembly.Version.ToString(3);
#endif

      return new ProductInfoHeaderValue(assemblyName, assemblyVersion);
    }
  }
}
