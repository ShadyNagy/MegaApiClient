namespace CG.Web.MegaApiClient.Serialization
{
  using System;
  using System.Collections.Generic;
  using System.Diagnostics;
  using System.Linq;
  using System.Runtime.Serialization;
  using Newtonsoft.Json;
  using Newtonsoft.Json.Linq;

  #region Node

  [DebuggerDisplay("Type: {Type} - Name: {Name} - Id: {Id}")]
  internal class Node : NodePublic, INode, INodeCrypto
  {
    private static readonly DateTime OriginalDateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0);
    private byte[] masterKey;
    private List<SharedKey> sharedKeys;

    public Node(byte[] masterKey, ref List<SharedKey> sharedKeys)
    {
      this.masterKey = masterKey;
      this.sharedKeys = sharedKeys;
    }

    #region Public properties

    [JsonProperty("h")]
    public string Id { get; private set; }

    [JsonProperty("p")]
    public string ParentId { get; private set; }

    [JsonProperty("u")]
    public string Owner { get; private set; }

    [JsonProperty("su")]
    public string SharingId { get; private set; }

    [JsonProperty("sk")]
    private string SharingKey { get; set; }

    [JsonProperty("fa")]
    public string SerializedFileAttributes { get; private set; }

    [JsonIgnore]
    public DateTime LastModificationDate { get; private set; }

    [JsonIgnore]
    public byte[] Key { get; private set; }

    [JsonIgnore]
    public byte[] FullKey { get; private set; }

    [JsonIgnore]
    public byte[] SharedKey { get; private set; }

    [JsonIgnore]
    public byte[] Iv { get; private set; }

    [JsonIgnore]
    public byte[] MetaMac { get; private set; }

    #endregion

    #region Deserialization

    [JsonProperty("ts")]
    private long SerializedLastModificationDate { get; set; }

    [JsonProperty("a")]
    private string SerializedAttributes { get; set; }

    [JsonProperty("k")]
    private string SerializedKey { get; set; }

    [OnDeserialized]
#if NETSTANDARD1_6
    public void OnDeserialized()
#else
    public void OnDeserialized(StreamingContext context)
#endif
    {
      // Add key from incoming sharing.
      if (this.SharingKey != null && this.sharedKeys.Any(x => x.Id == this.Id) == false)
      {
        this.sharedKeys.Add(new SharedKey(this.Id, this.SharingKey));
      }

      // Deserialize node
      this.LastModificationDate = OriginalDateTime.AddSeconds(this.SerializedLastModificationDate).ToLocalTime();

      if (this.Type == NodeType.File || this.Type == NodeType.Directory)
      {
        // There are cases where the SerializedKey property contains multiple keys separated with /
        // This can occur when a folder is shared and the parent is shared too.
        // Both keys are working so we use the first one
        string serializedKey = this.SerializedKey.Split('/')[0];
        int splitPosition = serializedKey.IndexOf(":");
        byte[] encryptedKey = serializedKey.Substring(splitPosition + 1).FromBase64();

        // If node is shared, we need to retrieve shared masterkey
        if (this.sharedKeys != null)
        {
          string handle = serializedKey.Substring(0, splitPosition);
          SharedKey sharedKey = this.sharedKeys.FirstOrDefault(x => x.Id == handle);
          if (sharedKey != null)
          {
            this.masterKey = Crypto.DecryptKey(sharedKey.Key.FromBase64(), this.masterKey);
            if (this.Type == NodeType.Directory)
            {
              this.SharedKey = this.masterKey;
            }
            else
            {
              this.SharedKey = Crypto.DecryptKey(encryptedKey, this.masterKey);
            }
          }
        }

        this.FullKey = Crypto.DecryptKey(encryptedKey, this.masterKey);

        if (this.Type == NodeType.File)
        {
          byte[] iv, metaMac, fileKey;
          Crypto.GetPartsFromDecryptedKey(this.FullKey, out iv, out metaMac, out fileKey);

          this.Iv = iv;
          this.MetaMac = metaMac;
          this.Key = fileKey;
        }
        else
        {
          this.Key = this.FullKey;
        }

        Attributes attributes = Crypto.DecryptAttributes(this.SerializedAttributes.FromBase64(), this.Key);
        this.Name = attributes.Name;
      }
    }

#endregion

#region Equality

    public bool Equals(INode other)
    {
      return other != null && this.Id == other.Id;
    }

    public override int GetHashCode()
    {
      return this.Id.GetHashCode();
    }

    public override bool Equals(object obj)
    {
      return this.Equals(obj as INode);
    }

#endregion
  }

  internal class NodePublic : INodePublic
  {
    public NodePublic(DownloadUrlResponse downloadResponse, byte[] fileKey)
    {
      Attributes attributes = Crypto.DecryptAttributes(downloadResponse.SerializedAttributes.FromBase64(), fileKey);
      this.Name = attributes.Name;
      this.Size = downloadResponse.Size;
      this.Type = NodeType.File;
    }

    protected NodePublic()
    {
    }

    [JsonIgnore]
    public string Name { get; protected set; }

    [JsonProperty("s")]
    public long Size { get; protected set; }

    [JsonProperty("t")]
    public NodeType Type { get; protected set; }
  }

#endregion

#region Get nodes

  internal class GetNodesRequest : RequestBase
  {
    public GetNodesRequest()
      : base("f")
    {
      this.c = 1;
    }

    public int c { get; private set; }
  }

  internal class GetNodesResponse
  {
    private readonly byte[] masterKey;
    private List<SharedKey> sharedKeys;

    public GetNodesResponse(byte[] masterKey)
    {
      this.masterKey = masterKey;
    }

    public Node[] Nodes { get; private set; }

    [JsonProperty("f")]
    public JRaw NodesSerialized { get; private set; }

    [JsonProperty("ok")]
    public List<SharedKey> SharedKeys
    {
      get { return this.sharedKeys; }
      private set { this.sharedKeys = value; }
    }

    [OnDeserialized]
#if NETSTANDARD1_6
    public void OnDeserialized()
#else
    public void OnDeserialized(StreamingContext context)
#endif

    {
      this.Nodes = JsonConvert.DeserializeObject<Node[]>(this.NodesSerialized.ToString(), new NodeConverter(this.masterKey, ref this.sharedKeys));
    }
  }

#endregion

#region Create nodes

  internal class CreateNodeRequest : RequestBase
  {
    private CreateNodeRequest(INode parentNode, NodeType type, string attributes, string encryptedKey, byte[] key, string completionHandle)
      : base("p")
    {
      this.ParentId = parentNode.Id;
      this.Nodes = new[]
      {
        new CreateNodeRequestData
        {
            Attributes = attributes,
            Key = encryptedKey,
            Type = type,
            CompletionHandle = completionHandle
        }
      };

      INodeCrypto parentNodeCrypto = parentNode as INodeCrypto;
      if (parentNodeCrypto == null)
      {
        throw new ArgumentException("parentNode node must implement INodeCrypto");
      }

      if (parentNodeCrypto.SharedKey != null)
      {
        this.Share = new ShareData(parentNode.Id);
        this.Share.AddItem(completionHandle, key, parentNodeCrypto.SharedKey);
      }
    }

    [JsonProperty("t")]
    public string ParentId { get; private set; }

    [JsonProperty("cr")]
    public ShareData Share { get; private set; }

    [JsonProperty("n")]
    public CreateNodeRequestData[] Nodes { get; private set; }

    public static CreateNodeRequest CreateFileNodeRequest(INode parentNode, string attributes, string encryptedkey, byte[] fileKey, string completionHandle)
    {
      return new CreateNodeRequest(parentNode, NodeType.File, attributes, encryptedkey, fileKey, completionHandle);
    }

    public static CreateNodeRequest CreateFolderNodeRequest(INode parentNode, string attributes, string encryptedkey, byte[] key)
    {
      return new CreateNodeRequest(parentNode, NodeType.Directory, attributes, encryptedkey, key, "xxxxxxxx");
    }

    internal class CreateNodeRequestData
    {
      [JsonProperty("h")]
      public string CompletionHandle { get; set; }

      [JsonProperty("t")]
      public NodeType Type { get; set; }

      [JsonProperty("a")]
      public string Attributes { get; set; }

      [JsonProperty("k")]
      public string Key { get; set; }
    }
  }

#endregion

  internal class GetNodesResponseConverter : JsonConverter
  {
    private readonly byte[] masterKey;

    public GetNodesResponseConverter(byte[] masterKey)
    {
      this.masterKey = masterKey;
    }

    public override bool CanConvert(Type objectType)
    {
      return typeof(GetNodesResponse) == objectType;
    }

    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
    {
      if (reader.TokenType == JsonToken.Null)
      {
        return null;
      }

      JObject jObject = JObject.Load(reader);

      GetNodesResponse target = new GetNodesResponse(this.masterKey);

      JsonReader jObjectReader = jObject.CreateReader();
      jObjectReader.Culture = reader.Culture;
      jObjectReader.DateFormatString = reader.DateFormatString;
      jObjectReader.DateParseHandling = reader.DateParseHandling;
      jObjectReader.DateTimeZoneHandling = reader.DateTimeZoneHandling;
      jObjectReader.FloatParseHandling = reader.FloatParseHandling;
      jObjectReader.MaxDepth = reader.MaxDepth;
      jObjectReader.SupportMultipleContent = reader.SupportMultipleContent;
      serializer.Populate(jObjectReader, target);

      return target;
    }

    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
    {
      throw new NotSupportedException();
    }
  }

  internal class NodeConverter : JsonConverter
  {
    private readonly byte[] masterKey;
    private List<SharedKey> sharedKeys;

    public NodeConverter(byte[] masterKey, ref List<SharedKey> sharedKeys)
    {
      this.masterKey = masterKey;
      this.sharedKeys = sharedKeys;
    }

    public override bool CanConvert(Type objectType)
    {
      return typeof(Node) == objectType;
    }

    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
    {
      if (reader.TokenType == JsonToken.Null)
      {
        return null;
      }

      JObject jObject = JObject.Load(reader);

      Node target = new Node(this.masterKey, ref this.sharedKeys);

      JsonReader jObjectReader = jObject.CreateReader();
      jObjectReader.Culture = reader.Culture;
      jObjectReader.DateFormatString = reader.DateFormatString;
      jObjectReader.DateParseHandling = reader.DateParseHandling;
      jObjectReader.DateTimeZoneHandling = reader.DateTimeZoneHandling;
      jObjectReader.FloatParseHandling = reader.FloatParseHandling;
      jObjectReader.MaxDepth = reader.MaxDepth;
      jObjectReader.SupportMultipleContent = reader.SupportMultipleContent;
      serializer.Populate(jObjectReader, target);

      return target;
    }

    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
    {
      throw new NotSupportedException();
    }
  }
}
