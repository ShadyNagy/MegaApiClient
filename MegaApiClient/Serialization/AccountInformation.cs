namespace CG.Web.MegaApiClient.Serialization
{
  using Newtonsoft.Json;

  internal class AccountInformationRequest : RequestBase
  {
    public AccountInformationRequest()
      : base("uq")
    {
    }

    [JsonProperty("strg")]
    public int Storage
    {
      get { return 1; }
    }

    [JsonProperty("xfer")]
    public int Transfer
    {
      get { return 0; }
    }

    [JsonProperty("pro")]
    public int AccountType
    {
      get { return 0; }
    }
  }

  internal class AccountInformationResponse : IAccountInformation
  {
    [JsonProperty("mstrg")]
    public long TotalQuota { get; private set; }

    [JsonProperty("cstrg")]
    public long UsedQuota { get; private set; }
  }
}
