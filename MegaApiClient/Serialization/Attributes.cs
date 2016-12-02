namespace CG.Web.MegaApiClient.Serialization
{
  using Newtonsoft.Json;

  internal class Attributes
  {
    public Attributes(string name)
    {
      this.Name = name;
    }

    [JsonProperty("n")]
    public string Name { get; set; }
  }
}
