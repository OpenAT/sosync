﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml.Serialization;

namespace WebSosync.Common
{
    public class SerializationService
    {
        #region Constructors
        public SerializationService()
        { }
        #endregion

        #region Methods
        public string ToXML(object o)
        {
            if (o == null)
                throw new InvalidOperationException($"{nameof(o)} cannot be null.");

            using (var ms = new MemoryStream())
            using (var sr = new StreamReader(ms))
            {
                XmlSerializer serializer = new XmlSerializer(o.GetType());
                serializer.Serialize(ms, o);
                ms.Seek(0, SeekOrigin.Begin);
                return sr.ReadToEnd();
            }
        }
        #endregion
    }
}