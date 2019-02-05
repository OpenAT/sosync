using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml.Serialization;

namespace WebSosync.Common
{
    public class SerializationService : IDisposable
    {
        #region Members
        private Dictionary<Type, XmlSerializer> _cache;
        private MemoryStream _ms;
        private StreamReader _sr;
        #endregion

        #region Constructors
        public SerializationService()
        {
            _cache = new Dictionary<Type, XmlSerializer>();
            _ms = new MemoryStream();
            _sr = new StreamReader(_ms, Encoding.UTF8, true, 2048);
        }

        public void Dispose()
        {
            _sr.Dispose();
            _ms.Dispose();
        }
        #endregion

        #region Methods
        public string ToXML(object o, Type[] extraTypes = null)
        {
            if (o == null)
                throw new InvalidOperationException($"{nameof(o)} cannot be null.");

            lock(_cache)
            {
                if (!_cache.ContainsKey(o.GetType()))
                    _cache.Add(o.GetType(), new XmlSerializer(o.GetType(), extraTypes));

                _ms.SetLength(0);

                XmlSerializer serializer = _cache[o.GetType()];
                serializer.Serialize(_ms, o);
                _ms.Seek(0, SeekOrigin.Begin);
                return _sr.ReadToEnd();
            }
        }
        #endregion
    }
}
