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
using System.Diagnostics;

namespace XmlRpc
{
    public class XmlRpcRawService : DynamicObject
    {
        #region Properties
        public string RequestUri { get; set; }
        public string Address { get; set; }
        public bool DateTimeAsString { get; set; }
        public string LastRequest { get; set; }
        public string LastResponse { get; set; }

        /// <summary>
        /// Last time from request to response, in milliseconds.
        /// </summary>
        public long LastRpcTime { get; set; }
        #endregion

        public XmlRpcRawService(string requestUri, string address, bool datetimeAsString)
        {
            RequestUri = requestUri;
            Address = address;
            DateTimeAsString = datetimeAsString;
        }

        public override bool TryInvokeMember(InvokeMemberBinder binder, object[] args, out object result)
        {
            Stopwatch s = new Stopwatch();
            s.Start();

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
                        LastResponse = response.Content.ReadAsStringAsync().Result;
                }

                if (typeArgs.Count > 0)
                {
                    try
                    {
                        XmlDocument doc = new XmlDocument();
                        doc.LoadXml(response.Content.ReadAsStringAsync().Result);
                        ThrowOnFaultResponse(doc);
                        result = GetXmlRpcResult(typeArgs[0], doc["methodResponse"]["params"]["param"]["value"]);
                    }
                    catch (Exception ex)
                    {
                        throw new Exception("Could not parse XML-RPC response.", ex);
                    }
                }
                else
                {
                    // If no type arguments were found, return raw request string
                    result = response.Content.ReadAsStringAsync().Result;
                }

                //result = int.Parse(doc["methodResponse"]["params"]["param"]["value"]["int"].InnerText);
                s.Stop();
                LastRpcTime = s.ElapsedMilliseconds;
            }
            catch (Exception ex)
            {
                s.Stop();
                LastRpcTime = s.ElapsedMilliseconds;

                result = ex.ToString();
                throw;
            }

            return true;
        }

        /// <summary>
        /// Throws an exception if the provided XML document contains a XML-RPC
        /// fault result.
        /// </summary>
        /// <param name="doc">XML document to be analyzed</param>
        private void ThrowOnFaultResponse(XmlDocument doc)
        {
            if (doc["methodResponse"].FirstChild.Name.ToLower() == "fault")
            {
                var faultNode = doc["methodResponse"]["fault"]["value"]["struct"];
                string faultCode = "";
                string faultString = "";

                foreach (XmlNode subNode in faultNode.ChildNodes)
                {
                    if (subNode["name"].InnerText.ToLower() == "faultcode")
                        faultCode = subNode["value"]["int"].InnerText;
                    else if (subNode["name"].InnerText.ToLower() == "faultstring")
                        faultString = subNode["value"].InnerText;
                }

                throw new Exception($"XML-RPC returned fault code: {faultCode}\n\n{faultString}");
            }
        }

        private object GetXmlRpcResult(Type t, XmlElement e)
        {
            if (t == typeof(Dictionary<string, object>))
            {
                var result = new Dictionary<string, object>();

                foreach (XmlNode member in e.FirstChild.ChildNodes)
                {
                    if (member.HasChildNodes && member["value"].FirstChild.Name == "array")
                    {
                        var list = new List<object>();

                        foreach (XmlNode arrEntry in member["value"]["array"]["data"])
                            list.Add(arrEntry.InnerText);

                        result.Add(member["name"].InnerText, list);
                    }
                    else if (member.HasChildNodes)
                    {
                        result.Add(member["name"].InnerText, member["value"].InnerText);
                    }
                    else
                    {
                        result.Add("value", member.Value);
                    }
                }

                return result;
            }
            else if (t.GetTypeInfo().IsPrimitive
                || t == typeof(string)
                || t == typeof(object)
                || (t.GetTypeInfo().IsGenericType
                    && t.GetGenericTypeDefinition() == typeof(Nullable<>)
                    && t.GetGenericArguments().Any(x => x.GetTypeInfo().IsValueType && x.GetTypeInfo().IsPrimitive)))
            {
                // For primitives, strings and plain objects, just parse a single value using InnerText,
                // to omit any nested nodes inside the passed node

                // If the node type is bool while the expected type is different,
                // and the node value is "0", return null. This is done in order
                // to deal with pythons "boolean null" values
                if (e == null || (e.FirstChild.LocalName == "boolean" && (t != typeof(bool) && t != typeof(bool?)) && e.InnerText == "0"))
                    return null;

                if (!string.IsNullOrEmpty(e.InnerText) && !t.GetTypeInfo().IsGenericType)
                {
                    if (t == typeof(bool))
                        return Convert.ChangeType(int.Parse(e.InnerText), t);
                    else
                        return Convert.ChangeType(e.InnerText, t);
                }
                else if (e != null && !string.IsNullOrEmpty(e.InnerText) && t.GetGenericTypeDefinition() == typeof(Nullable<>))
                {
                    if (t.GetGenericArguments()[0] == typeof(bool))
                        return Convert.ChangeType(int.Parse(e.InnerText), t.GetGenericArguments()[0]);
                    else
                        return Convert.ChangeType(e.InnerText, t.GetGenericArguments()[0]);
                }
                else
                {
                    return null;
                }
            }
            else if (t == typeof(DateTime) || t == typeof(DateTime?))
            {
                // If the expected value is of date/time, parse the value exactly
                // as long the value is longer than 1 character, to filter pythons
                // "boolean null" values
                if (e != null)
                    if (e.InnerText.Length <= 1)
                    {
                        return null;
                    }
                    else
                    {
                        var formats = new string[]
                        {
                            "yyyy-MM-ddTHH:mm:ss.FFFFFFFZ",
                            "yyyy-MM-ddTHH:mm:ss.FFFFFFF",
                            "yyyy-MM-dd HH:mm:ss.FFFFFFF",
                            "yyyy-MM-dd"
                        };
                        var result = DateTime.ParseExact(
                            e.InnerText,
                            formats,
                            CultureInfo.InvariantCulture,
                            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
                        return result;
                    }
                else
                    return null;
            }
            else if (t.IsArray && (t.GetElementType().GetTypeInfo().IsPrimitive || t.GetElementType() == typeof(string)) || t.GetElementType() == typeof(object))
            {
                // Create and parse primitive type arrays
                var nodes = e["array"]["data"].ChildNodes.OfType<XmlNode>().Where(x => !string.IsNullOrEmpty(x.InnerText)).ToList();
                var arr = Array.CreateInstance(t.GetElementType(), nodes.Count);

                int i = 0;
                foreach (XmlElement node in nodes)
                    arr.SetValue(GetXmlRpcResult(t.GetElementType(), node), i++);

                return arr;
            }
            else if (t.GetTypeInfo().IsClass && !t.IsArray && t != typeof(string))
            {
                // Create and pass a single complex type
                var resultObject = Activator.CreateInstance(t);

                var properties = t.GetProperties();

                foreach (var prop in properties)
                {
                    try
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

                            object value = null;
                            try
                            {
                                value = destElement.InnerText;

                                var isBoolProp = prop.PropertyType == typeof(bool) || prop.PropertyType == typeof(bool?);

                                if (!isBoolProp && Convert.ToString(value) == "0" && destElement.FirstChild.Name == "boolean")
                                    prop.SetValue(resultObject, null);
                                else
                                    prop.SetValue(resultObject, GetXmlRpcResult(prop.PropertyType, destElement));
                            }
                            catch (Exception ex)
                            {
                                throw new Exception($"Could not process property: {prop.Name}, value: \"{Convert.ToString(value)}\"", ex);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        throw new Exception($"Could not set property \"{prop.Name}\"", ex);
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

            try
            {
                if (arg != null)
                {
                    Type t = arg.GetType();

                    paramType = GetXmlRpcType(arg);

                    content.Append($"<{paramType}>");

                    if (t.GetTypeInfo().IsClass && t != typeof(string) && !t.IsArray)
                    {
                        if (t == typeof(Dictionary<string, object>))
                            FillContentFromDictionary((Dictionary<string, object>)arg, content);
                        else
                            FillContentFromProperties(arg, content);
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
            }
            catch (Exception ex)
            {
                throw new Exception($"{nameof(GetXmlRpcValueNode)} Value: {arg}", ex);
            }

            return content.ToString();
        }

        private void FillContentFromProperties(object arg, StringBuilder content)
        {
            Type t = arg.GetType();

            var properties = t.GetProperties();
            foreach (var prop in properties)
            {
                var attIgnore = prop.GetCustomAttribute<IgnoreDataMemberAttribute>();

                if (attIgnore == null)
                {
                    var att = prop.GetCustomAttribute<DataMemberAttribute>();

                    object propValue = prop.GetValue(arg);
                    string propName = prop.Name;

                    if (att != null)
                        propName = att.Name;

                    if (propValue != null)
                    {
                        content.Append("<member>");
                        content.Append($"<name>{propName}</name><value>{GetXmlRpcValueNode(propValue)}</value>");
                        content.AppendLine("</member>");
                    }
                    else
                    {
                        // Null values are represented as boolean false
                        content.Append("<member>");
                        content.Append($"<name>{propName}</name><value><boolean>0</boolean></value>");
                        content.AppendLine("</member>");
                    }
                }
            }
        }

        private void FillContentFromDictionary(Dictionary<string, object> arg, StringBuilder content)
        {
            foreach (var entry in arg)
            {
                content.Append("<member>");

                // In Odoo, null values are represented as boolean false
                if (entry.Value == null)
                    content.Append($"<name>{entry.Key}</name><value><boolean>0</boolean></value>");
                else
                    content.Append($"<name>{entry.Key}</name><value>{GetXmlRpcValueNode(entry.Value)}</value>");

                content.AppendLine("</member>");
            }
        }

        private string GetXmlRpcValue(string rpcType, object value)
        {
            string result = "";

            switch (rpcType)
            {
                case "dateTime.iso8601":
                    var date = (DateTime)value;
                    result = date.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ");
                    break;

                case "boolean":
                    // Empty strings are treated as boolean false
                    if (string.IsNullOrEmpty(value as string))
                        result = "0";
                    else
                        result = ((bool)value ? 1 : 0).ToString();
                    break;

                default:
                    result = XmlHelper.ToXmlString(Convert.ToString(value));
                    break;
            }

            return result;
        }

        private string GetXmlRpcType(object arg)
        {
            Type t;

            t = arg as Type;

            if (t == null)
                t = arg.GetType();

            if (t == typeof(bool))
                return "boolean";

            if (t == typeof(int))
                return "int";

            if (t == typeof(DateTime) || t == typeof(DateTime?))
                return "dateTime.iso8601";

            if (t.GetTypeInfo().IsClass && !t.IsArray && t != typeof(string))
                return "struct";

            if (t.IsArray)
                return "array";

            // Empty or null strings have to be treated as boolean false
            if (t == typeof(string) && String.IsNullOrEmpty((string)arg))
                return "boolean";

            return "string";
        }
    }
}
