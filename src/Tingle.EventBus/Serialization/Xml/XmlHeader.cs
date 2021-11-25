using System.Collections.Generic;
using System.Xml.Serialization;

namespace Tingle.EventBus.Serialization.Xml;

///
[XmlType("Header")]
public struct XmlHeader
{
    ///
    public XmlHeader(string key, string value) : this() { Key = key; Value = value; }

    ///
    public string Key { get; set; }

    ///
    public string Value { get; set; }

    /// <summary>
    /// Convert <see cref="XmlHeader"/> to <see cref="KeyValuePair{TKey, TValue}"/>.
    /// </summary>
    /// <param name="header"></param>
    public static implicit operator KeyValuePair<string, string>(XmlHeader header)
    {
        return new KeyValuePair<string, string>(header.Key, header.Value);
    }

    /// <summary>
    /// Convert <see cref="KeyValuePair{TKey, TValue}"/> to <see cref="XmlHeader"/>.
    /// </summary>
    /// <param name="pair"></param>
    public static implicit operator XmlHeader(KeyValuePair<string, string> pair)
    {
        return new XmlHeader(pair.Key, pair.Value);
    }
}
