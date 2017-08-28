﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
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

        public string LastRequestRaw { get; private set; }
        public string LastResponseRaw { get; private set; }
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
            finally
            {
                LastRequestRaw = _rpcCommon.LastRequest;
                LastResponseRaw = _rpcCommon.LastResponse;
            }
        }

        /// <summary>
        /// Gets the specified model from Odoo.
        /// </summary>
        /// <typeparam name="T">Type used for mapping.</typeparam>
        /// <param name="modelName">The Odoo model name for the requested model.</param>
        /// <param name="id">The ID for the requested model.</param>
        /// <returns>A new instance of <see cref="T"/>, filled with the returned data.</returns>
        public T GetModel<T>(string modelName, int id) where T: class
        {
            try
            {
                var context = new { lang = Language };

                var propertyList = typeof(T)
                    .GetProperties()
                    .Where(x => x.GetCustomAttribute<IgnoreDataMemberAttribute>() == null)
                    .Select(x => x.GetCustomAttribute<DataMemberAttribute>()?.Name ?? x.Name)
                    .ToArray();

                var result = (T)_rpcObject.execute_kw<T>(
                    Database,
                    _uid,
                    Password,
                    modelName,
                    "read",
                    new int[] { id },
                    new { fields = propertyList, context = context });

                return result;
            }
            finally
            {
                LastRequestRaw = _rpcObject.LastRequest;
                LastResponseRaw = _rpcObject.LastResponse;
            }
        }

        public Dictionary<string, object> GetDictionary(string modelName, int id, string[] fields)
        {
            try
            {
                var context = new { lang = Language };

                var result = (Dictionary<string, object>)_rpcObject.execute_kw<Dictionary<string, object>>(
                    Database,
                    _uid,
                    Password,
                    modelName,
                    "read",
                    new int[] { id },
                    new { fields, context = context });

                return result;
            }
            finally
            {
                LastRequestRaw = _rpcObject.LastRequest;
                LastResponseRaw = _rpcObject.LastResponse;
            }
        }

        /// <summary>
        /// Searches the specified model by a single property.
        /// </summary>
        /// <typeparam name="T">The class that represents the model.</typeparam>
        /// <typeparam name="TProp">The property type to be queried.</typeparam>
        /// <param name="modelName">The Odoo model name.</param>
        /// <param name="propertySelector">The property selector for the value.</param>
        /// <param name="value">The actual value to be queried.</param>
        /// <returns></returns>
        public int[] SearchModelByField<T, TProp>(string modelName, Expression<Func<T, TProp>> propertySelector, TProp value)
        {
            try
            {
                var prop = ((PropertyInfo)((MemberExpression)propertySelector.Body).Member);
                var propAtt = prop.GetCustomAttribute<DataMemberAttribute>();
                string propName = propAtt == null ? prop.Name : propAtt.Name;

                var context = new { lang = Language };

                var result = _rpcObject.execute_kw<int[]>(
                    Database,
                    _uid,
                    Password,
                    modelName,
                    "search",
                    new object[] { new object[] { new object[] { propName, "=", value } } },
                    new { context = context });

                return result;
            }
            finally
            {
                LastRequestRaw = _rpcObject.LastRequest;
                LastResponseRaw = _rpcObject.LastResponse;
            }
        }

        /// <summary>
        /// Creates the specified model in Odoo.
        /// </summary>
        /// <typeparam name="T">Type used for mapping.</typeparam>
        /// <param name="modelName">The Odoo model name for the model to be created.</param>
        /// <param name="model">The actual model with the data.</param>
        /// <returns></returns>
        public int CreateModel<T>(string modelName, T model, bool create_sync_job) where T: class
        {
            try
            {
                object context = null;
                if (create_sync_job == false)
                    context = new { lang = Language, create_sync_job = create_sync_job };
                else
                    context = new { lang = Language };

                var result = (int)_rpcObject.execute_kw<int>(
                    Database,
                    _uid,
                    Password,
                    modelName,
                    "create",
                    new T[] { model },
                    new { context = context });

                return result;
            }
            finally
            {
                LastRequestRaw = _rpcObject.LastRequest;
                LastResponseRaw = _rpcObject.LastResponse;
            }
        }

        public int CreateModel(string modelName, object model, bool create_sync_job)
        {
            try
            {
                object context = null;
                if (create_sync_job == false)
                    context = new { lang = Language, create_sync_job = create_sync_job };
                else
                    context = new { lang = Language };

                var result = (int)_rpcObject.execute_kw<int>(
                    Database,
                    _uid,
                    Password,
                    modelName,
                    "create",
                    new object[] { model },
                    new { context = context });

                return result;
            }
            finally
            {
                LastRequestRaw = _rpcObject.LastRequest;
                LastResponseRaw = _rpcObject.LastResponse;
            }
        }


        /// <summary>
        /// Updates the specified model in Odoo.
        /// </summary>
        /// <typeparam name="T">Type used for mapping.</typeparam>
        /// <param name="modelName">The Odoo model name for the model to be updated.</param>
        /// <param name="model">The actual model with the data.</param>
        public bool UpdateModel<T>(string modelName, T model, int fsoId, bool create_sync_job) where T : class
        {
            if (model == null)
                throw new ArgumentNullException(nameof(model));

            try
            {
                object context = null;
                if (create_sync_job == false)
                    context = new { lang = Language, create_sync_job = create_sync_job };
                else
                    context = new { lang = Language };

                var result = _rpcObject.execute_kw<int>(
                    Database,
                    _uid,
                    Password,
                    modelName,
                    "write",
                    new object[] { new int[] { fsoId }, model },
                    new { context = context });

                return result != 0;
            }
            finally
            {
                LastRequestRaw = _rpcObject.LastRequest;
                LastResponseRaw = _rpcObject.LastResponse;
            }
        }

        public bool UpdateModel(string modelName, object model, int fsoId, bool create_sync_job)
        {
            if (model == null)
                throw new ArgumentNullException(nameof(model));

            try
            {
                object context = null;
                if (create_sync_job == false)
                    context = new { lang = Language, create_sync_job = create_sync_job };
                else
                    context = new { lang = Language };

                var result = _rpcObject.execute_kw<int>(
                    Database,
                    _uid,
                    Password,
                    modelName,
                    "write",
                    new object[] { new int[] { fsoId }, model },
                    new { context = context });

                return result != 0;
            }
            finally
            {
                LastRequestRaw = _rpcObject.LastRequest;
                LastResponseRaw = _rpcObject.LastResponse;
            }
        }
        #endregion
    }
}
