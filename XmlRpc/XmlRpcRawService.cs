using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Globalization;
using System.Net.Http;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Linq;

namespace XmlRpc
{
    public class XmlRpcRawService : DynamicObject
    {
        #region Properties
        public string RequestUri { get; set; }
        public string Address { get; set; }
        public bool DateTimeAsString { get; set; }
        public string LastRequest { get; set; }
        public string LastRepsonse { get; set; }
        #endregion

        public XmlRpcRawService(string requestUri, string address, bool datetimeAsString)
        {
            RequestUri = requestUri;
            Address = address;
            DateTimeAsString = datetimeAsString;
        }

        public override bool TryInvokeMember(InvokeMemberBinder binder, object[] args, out object result)
        {
            try
            {
                HttpClient client = new HttpClient()
                {
                    BaseAddress = new Uri(Address)
                };

                var member = binder.GetType().GetField("_typeArguments",
                    BindingFlags.Instance
                    | BindingFlags.Public
                    | BindingFlags.NonPublic
                    | BindingFlags.Static);

                var typeArgs = (List<Type>)member.GetValue(binder);

                StringBuilder content = new StringBuilder();
                content.AppendLine($"<methodCall><methodName>{binder.Name}</methodName><params>");

                foreach (var arg in args)
                {
                    content.Append("<param>");
                    content.Append(GetXmlRpcValueNode(arg));
                    content.AppendLine("</param>");
                }

                content.AppendLine("</params></methodCall>");

                if (DateTimeAsString)
                    content.Replace("dateTime.iso8601", "string");

                // Set the last request property before executing
                LastRequest = content.ToString();

                HttpResponseMessage response = null;
                try
                {
                    response = client
                        .PostAsync(RequestUri, new StringContent(content.ToString(), Encoding.UTF8, "application/xml"))
                        .Result;
                }
                finally
                {
                    // Always set the last response, if there actually is an response
                    if (response != null)
                        LastRepsonse = response.Content.ReadAsStringAsync().Result;
                }

                if (typeArgs.Count > 0)
                {
                    XmlDocument doc = new XmlDocument();
                    doc.LoadXml(response.Content.ReadAsStringAsync().Result);

                    result = GetXmlRpcResult(typeArgs[0], doc["methodResponse"]["params"]["param"]["value"]);
                }
                else
                {
                    // If no type arguments were found, return raw request string
                    result = response.Content.ReadAsStringAsync().Result;
                }

                //result = int.Parse(doc["methodResponse"]["params"]["param"]["value"]["int"].InnerText);
            }
            catch (Exception ex)
            {
                result = ex.ToString();
                throw;
            }

            return true;
        }

        private object GetXmlRpcResult(Type t, XmlElement e)
        {
            if (t.GetTypeInfo().IsPrimitive || t == typeof(string) || (
                t.GetTypeInfo().IsGenericType
                && t.GetGenericTypeDefinition() == typeof(Nullable<>)
                && t.GetGenericArguments().Any(x => x.GetTypeInfo().IsValueType && x.GetTypeInfo().IsPrimitive)
                ))
            {
                // For primitives or strings, just parse a single value using InnerText,
                // to omit any nested nodes inside the passed node

                // If the node type is bool while the expected type is different,
                // and the node value is "0", return null. This is done in order
                // to deal with pythons "boolean null" values
                if (e == null || (e.FirstChild.LocalName == "boolean" && (t != typeof(bool) && t != typeof(bool?)) && e.InnerText == "0"))
                    return null;

                if (!string.IsNullOrEmpty(e.InnerText) && !t.GetTypeInfo().IsGenericType)
                    return Convert.ChangeType(e.InnerText, t);
                else if (e != null && !string.IsNullOrEmpty(e.InnerText) && t.GetGenericTypeDefinition() == typeof(Nullable<>))
                    return Convert.ChangeType(e.InnerText, t.GetGenericArguments()[0]);
                else
                    return null;
            }
            else if (t == typeof(DateTime) || t == typeof(DateTime?))
            {
                // If the expected value is of date/time, parse the value exactly
                // as long the value is longer than 1 character, to filter pythons
                // "boolean null" values
                if (e != null)
                    if (e.InnerText.Length <= 1)
                        return null;
                    else
                        return DateTime.ParseExact(e.InnerText, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
                else
                    return null;
            }
            else if (t.IsArray && (t.GetElementType().GetTypeInfo().IsPrimitive || t.GetElementType() == typeof(string)))
            {
                // Create and parse primitive type arrays
                var nodes = e["array"].ChildNodes.OfType<XmlNode>().Where(x => !string.IsNullOrEmpty(x.InnerText)).ToList();
                var arr = Array.CreateInstance(t.GetElementType(), nodes.Count);

                int i = 0;
                foreach (XmlElement node in nodes)
                {
                    arr.SetValue(GetXmlRpcResult(t.GetElementType(), node), i);
                }

                return arr;
            }
            else if (t.GetTypeInfo().IsClass && !t.IsArray && t != typeof(string))
            {
                // Create and pass a single complex type
                var resultObject = Activator.CreateInstance(t);

                var properties = t.GetProperties();

                foreach (var prop in properties)
                {
                    var attIgnore = prop.GetCustomAttribute<IgnoreDataMemberAttribute>();

                    if (attIgnore == null)
                    {
                        var name = prop.Name;
                        var att = prop.GetCustomAttribute<DataMemberAttribute>();

                        if (att != null)
                            name = att.Name;

                        XmlElement destElement = null;
                        foreach (XmlNode childNode in e.FirstChild.ChildNodes)
                        {
                            if (childNode.FirstChild.LocalName == "name" && childNode.FirstChild.InnerText == name)
                            {
                                destElement = (XmlElement)childNode.ChildNodes[1];
                                break;
                            }
                        }

                        try
                        {
                            prop.SetValue(resultObject, GetXmlRpcResult(prop.PropertyType, destElement));
                        }
                        catch (Exception ex)
                        {
                            new Exception($"Property: {prop.Name}", ex);
                        }
                    }
                }

                return resultObject;
            }
            else
            {
                // Unknown thing, can't parse
                throw new NotSupportedException($"The XML node\n\n\"{e.OuterXml}\"\n\ncould not be parsed as {t.FullName}.");
            }
        }

        private string GetXmlRpcValueNode(object arg)
        {
            StringBuilder content = new StringBuilder();
            string paramType = "";

            if (arg != null)
            {
                Type t = arg.GetType();

                paramType = GetXmlRpcType(arg);

                content.Append($"<{paramType}>");

                if (t.GetTypeInfo().IsClass && t != typeof(string) && !t.IsArray)
                {
                    var properties = t.GetProperties();
                    foreach (var prop in properties)
                    {
                        var attIgnore = prop.GetCustomAttribute<IgnoreDataMemberAttribute>();

                        if (attIgnore == null)
                        {
                            var att = prop.GetCustomAttribute<DataMemberAttribute>();

                            object propValue = prop.GetValue(arg);

                            if (propValue != null)
                            {
                                string propName = prop.Name;

                                if (att != null)
                                    propName = att.Name;

                                string propType = GetXmlRpcType(propValue);

                                content.Append("<member>");
                                content.Append($"<name>{propName}</name><value>{GetXmlRpcValueNode(propValue)}</value>");
                                content.AppendLine("</member>");
                            }
                        }
                    }
                }
                else if (t.GetTypeInfo().IsClass && t.IsArray)
                {
                    foreach (var subItem in arg as Array)
                    {
                        content.Append(GetXmlRpcValueNode(subItem));
                    }
                }
                else
                {
                    content.Append(GetXmlRpcValue(paramType, arg));
                }

                content.Append($"</{paramType}>");
            }

            return content.ToString();
        }

        private string GetXmlRpcValue(string rpcType, object value)
        {
            string result = "";

            switch (rpcType)
            {
                case "dateTime.iso8601": result = ((DateTime)value).ToString("o"); break;
                default: result = Convert.ToString(value); break;
            }

            return result;
        }

        private string GetXmlRpcType(object arg)
        {
            Type t;

            t = arg as Type;

            if (t == null)
                t = arg.GetType();

            if (t == typeof(int))
                return "int";

            if (t == typeof(DateTime) || t == typeof(DateTime?))
                return "dateTime.iso8601";

            if (t.GetTypeInfo().IsClass && !t.IsArray && t != typeof(string))
                return "struct";

            if (t.IsArray)
                return "array";

            return "string";
        }
    }
}
