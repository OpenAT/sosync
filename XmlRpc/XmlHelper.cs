using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;

namespace XmlRpc
{
    public static class XmlHelper
    {
        public static string ToXmlString(string s)
        {
            XmlDocument doc = new XmlDocument();
            XmlNode node = doc.CreateElement("root");
            node.InnerText = s;
            return node.InnerXml;
        }
    }
}
