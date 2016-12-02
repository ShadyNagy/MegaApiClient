namespace CG.Web.MegaApiClient.Serialization
{
  using System;
  using System.Collections.Generic;
  using System.Diagnostics;
  using System.Linq;
  using Newtonsoft.Json;

  internal class ShareNodeRequest : RequestBase
  {
    public ShareNodeRequest(INode node, byte[] masterKey, IEnumerable<INode> nodes)
      : base("s2")
    {
      this.Id = node.Id;
      this.Options = new object[] { new { r = 0, u = "EXP" } };

      INodeCrypto nodeCrypto = (INodeCrypto)node;
      byte[] uncryptedSharedKey = nodeCrypto.SharedKey;
      if (uncryptedSharedKey == null)
      {
        uncryptedSharedKey = Crypto.CreateAesKey();
      }

      this.SharedKey = Crypto.EncryptKey(uncryptedSharedKey, masterKey).ToBase64();

      if (nodeCrypto.SharedKey == null)
      {
        this.Share = new ShareData(node.Id);

        this.Share.AddItem(node.Id, nodeCrypto.FullKey, uncryptedSharedKey);

        // Add all children
        IEnumerable<INode> allChildren = this.GetRecursiveChildren(nodes.ToArray(), node);
        foreach (var child in allChildren)
        {
          this.Share.AddItem(child.Id, ((INodeCrypto)child).FullKey, uncryptedSharedKey);
        }
      }

      byte[] handle = (node.Id + node.Id).ToBytes();
      this.HandleAuth = Crypto.EncryptKey(handle, masterKey).ToBase64();
    }

    private IEnumerable<INode> GetRecursiveChildren(INode[] nodes, INode parent)
    {
      foreach (var node in nodes.Where(x => x.Type == NodeType.Directory || x.Type == NodeType.File))
      {
        string parentId = node.Id;
        do
        {
          parentId = nodes.FirstOrDefault(x => x.Id == parentId)?.ParentId;
          if (parentId == parent.Id)
          {
            yield return node;
            break;
          }
        } while (parentId != null);
      }
    }

    [JsonProperty("n")]
    public string Id { get; private set; }

    [JsonProperty("ha")]
    public string HandleAuth { get; private set; }

    [JsonProperty("s")]
    public object[] Options { get; private set; }

    [JsonProperty("cr")]
    public ShareData Share { get; private set; }

    [JsonProperty("ok")]
    public string SharedKey { get; private set; }
  }


  #region ShareData

  [JsonConverter(typeof(ShareDataConverter))]
  internal class ShareData
  {
    private IList<ShareDataItem> items;

    public ShareData(string nodeId)
    {
      this.NodeId = nodeId;
      this.items = new List<ShareDataItem>();
    }

    public string NodeId { get; private set; }

    public IEnumerable<ShareDataItem> Items { get { return this.items; } }

    public void AddItem(string nodeId, byte[] data, byte[] key)
    {
      ShareDataItem item = new ShareDataItem
      {
        NodeId = nodeId,
        Data = data,
        Key = key
      };

      this.items.Add(item);
    }

    public class ShareDataItem
    {
      public string NodeId { get; set; }

      public byte[] Data { get; set; }

      public byte[] Key { get; set; }
    }
  }

  internal class ShareDataConverter : JsonConverter
  {
    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
    {
      ShareData data = value as ShareData;
      if (data == null)
      {
        throw new ArgumentException("invalid data to serialize");
      }

      writer.WriteStartArray();

      writer.WriteStartArray();
      writer.WriteValue(data.NodeId);
      writer.WriteEndArray();

      writer.WriteStartArray();
      foreach (var item in data.Items)
      {
        writer.WriteValue(item.NodeId);
      }
      writer.WriteEndArray();

      writer.WriteStartArray();
      int counter = 0;
      foreach (var item in data.Items)
      {
        writer.WriteValue(0);
        writer.WriteValue(counter++);
        writer.WriteValue(Crypto.EncryptKey(item.Data, item.Key).ToBase64());
      }
      writer.WriteEndArray();

      writer.WriteEndArray();
    }

    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
    {
      throw new NotImplementedException();
    }

    public override bool CanConvert(Type objectType)
    {
      return objectType == typeof(ShareData);
    }
  }

  [DebuggerDisplay("Id: {Id} / Key: {Key}")]
  internal class SharedKey
  {
    public SharedKey(string id, string key)
    {
      this.Id = id;
      this.Key = key;
    }

    [JsonProperty("h")]
    public string Id { get; private set; }

    [JsonProperty("k")]
    public string Key { get; private set; }
  }

  #endregion
}
