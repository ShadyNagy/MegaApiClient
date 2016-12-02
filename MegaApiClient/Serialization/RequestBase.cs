namespace CG.Web.MegaApiClient.Serialization
{
  using Newtonsoft.Json;

  internal abstract class RequestBase
  {
    protected RequestBase(string action)
    {
      this.Action = action;
    }

    [JsonProperty("a")]
    public string Action { get; private set; }
  }
}
