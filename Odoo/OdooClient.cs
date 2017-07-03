using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using XmlRpc;

namespace Odoo
{
    public class OdooClient
    {
        #region Members
        private dynamic _rpcCommon;
        private dynamic _rpcObject;
        private int _uid;
        #endregion

        #region Properties
        public string Address { get; set; }
        public string Database { get; set; }
        public string User { get; set; }
        public string Password { get; set; }
        public string Language { get; set; }
        #endregion

        #region Constructors
        public OdooClient(string address, string database, string language = "de_DE")
        {
            Address = address;
            Database = database;
            Language = language;

            _rpcCommon = new XmlRpcRawService("common", Address, false);
            _rpcObject = new XmlRpcRawService("object", Address, true);
        }
        #endregion

        #region Methods
        /// <summary>
        /// Authorizes the specified user and internally stores the user ID.
        /// </summary>
        /// <param name="user">The user for the client.</param>
        /// <param name="password">The password for the client.</param>
        public void Authenticate(string user, string password)
        {
            User = user;
            Password = password;

            var context = new { lang = Language };

            try
            {
                _uid = _rpcCommon.authenticate<int>(Database, User, Password, context);
            }
            catch (Exception ex)
            {
                throw new UnauthorizedAccessException("Authentication failed.", ex);
            }
        }

        /// <summary>
        /// Gets the specified model from Odoo.
        /// </summary>
        /// <typeparam name="T">Type to map the data to.</typeparam>
        /// <param name="modelName">The Odoo model name for the requested model.</param>
        /// <param name="id">The ID for the requested model.</param>
        /// <returns>A new instance of <see cref="T"/>, filled with the returned data.</returns>
        public T GetModel<T>(string modelName, int id) where T: class
        {
            // Apparently can't send context to model queries
            var context = new { lang = Language };

            var propertyList = typeof(T)
                .GetProperties()
                .Where(x => x.GetCustomAttribute<IgnoreDataMemberAttribute>() == null)
                .Select(x => x.GetCustomAttribute<DataMemberAttribute>()?.Name ?? x.Name)
                .ToArray();

            var result = (T)_rpcObject.execute_kw<T>(Database, _uid, Password, modelName, "read", new int[] { id }, new { fields = propertyList, context = context });

            return result;
        }

        public int CreateModel<T>(string modelName, T model) where T: class
        {
            var result = (int)_rpcObject.execute_kw<int>(Database, _uid, Password, modelName, "create", new T[] { model });
            return result;
        }
        #endregion


    }
}
